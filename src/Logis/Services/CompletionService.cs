using Logis.Tools;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Spectre.Console;

namespace Logis.Services;

/// <summary>
/// Orchestrates the interaction with the AI model, handling prompt construction, 
/// API communication, and tool execution.
/// </summary>
public class CompletionService
{
    /// <summary>
    /// Sends the current session state to the model and returns the result.
    /// Handles tool scoping, history reconstruction, and tool result spilling.
    /// Uses in-memory session history and implements tool-driven focus tracking.
    /// </summary>
    /// <param name="session">The active session.</param>
    /// <param name="sessionService">Service to persist session history.</param>
    /// <param name="contextService">Service to assemble workspace context.</param>
    /// <param name="config">The provider configuration.</param>
    /// <param name="options">Runtime options.</param>
    /// <param name="skillService">Service to provide state-specific instructions.</param>
    /// <param name="statusContext">Optional UI status context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<CompletionResult> CompleteAsync(
        Session session,
        SessionService sessionService,
        ContextService contextService,
        Config config,
        LogisOptions options,
        SkillService skillService,
        StatusContext? statusContext = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve Provider
        string providerId = session.Provider ?? config.DefaultProvider;
        if (!config.Providers.TryGetValue(providerId, out var providerConfig))
        {
            throw new InvalidOperationException($"Provider '{providerId}' not found.");
        }

        // 2. Initialize Clients
        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(providerConfig.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(providerConfig.BaseUrl);
        }

        string apiKeyValue = string.IsNullOrWhiteSpace(providerConfig.ApiKey) ? "local-dev" : providerConfig.ApiKey;
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKeyValue), clientOptions);

        // Prioritize the model name set on the session (CLI override), 
        // fallback to the provider's default config.
        string modelName = session.Model ?? providerConfig.Model;
        using IChatClient chatClient = openAIClient.GetChatClient(modelName).AsIChatClient();

        // 3. Prepare System Messages (Injected every turn for current state)
        string skill = skillService.GetSkill(session.State, options.EditFormat, session.Context.FocusedFiles);
        string workspaceContext = await contextService.AssembleContextAsync(session, cancellationToken);
        
        var promptMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, skill),
            new ChatMessage(ChatRole.System, workspaceContext)
        };

        // Prepend system context to the existing conversation history
        promptMessages.AddRange(session.History);

        // 4. Prepare Tools (Scoped by State)
        var toolService = new FileSystemTools(session.Context.FocusedFiles);
        var functions = new List<AIFunction>();
        
        if (session.State is SessionState.Research or SessionState.Edit)
        {
            // Explicit naming ensures consistency between the LLM toolbox and our tracking logic.
            functions.Add(AIFunctionFactory.Create(toolService.ListDirectory, new AIFunctionFactoryOptions { Name = "ListDirectory" }));
            functions.Add(AIFunctionFactory.Create(toolService.ReadFileAsync, new AIFunctionFactoryOptions { Name = "ReadFile" }));
        }

        var chatOptions = new ChatOptions
        {
            Temperature = (float)providerConfig.Temperature,
            MaxOutputTokens = providerConfig.MaxTokens,
            AdditionalProperties = new() { ["num_ctx"] = providerConfig.ContextWindow },
            Tools = functions.Cast<AITool>().ToList()
        };

        ChatResponse? lastResponse = null;
        int iterations = 0;

        // 5. Manual Orchestration Loop
        while (iterations < options.MaxToolIterations)
        {
            iterations++;
            
            lastResponse = await chatClient.GetResponseAsync(promptMessages, chatOptions);
            
            // Record assistant response in memory and on disk
            var assistantTurn = MapToTurn(session, "assistant", lastResponse.Text ?? "[TOOL CALL]", session.State);
            assistantTurn = assistantTurn with { FinishReason = lastResponse.FinishReason?.ToString() };
            
            session.History.Add(lastResponse.Messages[0]);
            sessionService.AppendTurn(session, assistantTurn);
            
            promptMessages.Add(lastResponse.Messages[0]);

            if (lastResponse.FinishReason != ChatFinishReason.ToolCalls)
            {
                break;
            }

            // Process Tool Calls
            foreach (var content in lastResponse.Messages[0].Contents)
            {
                if (content is FunctionCallContent toolCall)
                {
                    var tool = functions.FirstOrDefault(t => t.Name == toolCall.Name);
                    if (tool != null)
                    {
                        statusContext?.Status($"[grey]Executing {toolCall.Name}...[/]");
                        
                        object? result = await tool.InvokeAsync(new AIFunctionArguments(toolCall.Arguments));
                        string resultString = result?.ToString() ?? string.Empty;

                        // Tool Authority Focus Tracking: Only focus files after a successful read.
                        // We check for "ReadFile" which is the explicit name assigned in the factory.
                        if (toolCall.Name == "ReadFile" && 
                            !resultString.StartsWith("Error:") && 
                            toolCall.Arguments is { } args && 
                            args.TryGetValue("path", out var pathObj) && 
                            pathObj?.ToString() is string filePath)
                        {
                            if (!session.Context.FocusedFiles.Contains(filePath, StringComparer.OrdinalIgnoreCase))
                            {
                                session.Context.FocusedFiles.Add(filePath);
                                sessionService.RecordFileLoad(session.Id, filePath, "unknown", toolCall.CallId);
                            }
                        }

                        // Handle Spilling for large results (> 50KB)
                        string? spilledPath = null;
                        if (resultString.Length > 50 * 1024)
                        {
                            string turnId = Guid.NewGuid().ToString("N");
                            spilledPath = Path.Combine("tool-results", $"{turnId}.txt");
                            string fullSpilledPath = Path.Combine(session.SessionPath, spilledPath);
                            await File.WriteAllTextAsync(fullSpilledPath, resultString, cancellationToken);
                            resultString = $"[TRUNCATED - full result in {spilledPath}]\n" + resultString[..500];
                        }

                        // Record tool result in memory and on disk
                        var toolTurn = MapToTurn(session, "tool", resultString, session.State, toolCall.Name, toolCall.CallId);
                        toolTurn = toolTurn with { ToolResultPath = spilledPath };
                        
                        var toolMessage = new ChatMessage(ChatRole.Tool, resultString) 
                        { 
                            Contents = [ new FunctionResultContent(toolCall.CallId, resultString) ] 
                        };

                        session.History.Add(toolMessage);
                        sessionService.AppendTurn(session, toolTurn);
                        promptMessages.Add(toolMessage);
                    }
                }
            }
        }

        if (lastResponse == null) throw new Exception("Model returned no response.");

        return new CompletionResult(
            Request: MapRequest(promptMessages, chatOptions),
            Content: lastResponse.Text ?? string.Empty,
            RawResponse: lastResponse.RawRepresentation?.ToString() ?? "Empty",
            Usage: new LogisUsage(
                (int)(lastResponse.Usage?.InputTokenCount ?? 0),
                (int)(lastResponse.Usage?.OutputTokenCount ?? 0),
                (int)(lastResponse.Usage?.TotalTokenCount ?? 0)
            ),
            FinishReason: lastResponse.FinishReason?.ToString() ?? "unknown",
            File: "session",
            Task: "session",
            Model: providerConfig.Model,
            ProviderId: providerId,
            EditFormat: options.EditFormat,
            ToolCallCount: 0
        );
    }

    private SessionTurn MapToTurn(Session session, string role, string content, SessionState state, string? toolName = null, string? toolCallId = null)
    {
        return new SessionTurn(
            Id: Guid.NewGuid().ToString("N"),
            SessionId: session.Id,
            Role: role.ToLowerInvariant(),
            Content: content,
            ToolName: toolName,
            ToolCallId: toolCallId,
            StateAtTurn: state.ToString(),
            FinishReason: null,
            TokenCount: null,
            Iteration: null,
            IsPinned: false,
            IsSummary: false,
            ToolResultPath: null,
            WorkspaceRoot: session.Context.WorkspaceRoot,
            Timestamp: DateTime.UtcNow
        );
    }

    /// <summary>
    /// Dumps the registered tools to the console for debugging.
    /// </summary>
    private void TraceToolDefinitions(List<AIFunction> functions)
    {
        AnsiConsole.MarkupLine("[bold yellow]=== DEBUG: TOOL DEFINITIONS ===[/]");
        foreach (var tool in functions)
        {
            AnsiConsole.MarkupLine($"[green]Function:[/] {Markup.Escape(tool.Name)}");
            AnsiConsole.MarkupLine($"[grey]Description:[/] {Markup.Escape(tool.Description ?? "No description")}");
        }
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Prints breadcrumbs for the current iteration of the tool loop.
    /// </summary>
    private void TraceIteration(int iteration, int messageCount)
    {
        AnsiConsole.MarkupLine($"[grey]DEBUG: Iteration {iteration}, sending {messageCount} messages...[/]");
    }

    /// <summary>
    /// Prints a persistent record of a tool call to the console.
    /// </summary>
    private void TraceToolCall(string name, string args)
    {
        AnsiConsole.MarkupLine($"[grey]DEBUG: Tool Call -> {name}({Markup.Escape(args)})[/]");
    }

    /// <summary>
    /// Maps the MEA request objects back to our local Logis models for logging.
    /// </summary>
    private LogisRequest MapRequest(List<ChatMessage> messages, ChatOptions options)
    {
        return new LogisRequest(
            Messages: messages.Select(m => new LogisMessage(
                Role: m.Role.ToString(), 
                Content: string.Join("\n", m.Contents.Select(c => c switch 
                {
                    TextContent t => t.Text,
                    FunctionCallContent f => $"[TOOL CALL] {f.Name}({string.Join(", ", (f.Arguments ?? new Dictionary<string, object?>()).Select(a => $"{a.Key}: {a.Value}"))})", 
                    FunctionResultContent r => $"[TOOL RESULT] {r.Result}",
                    _ => c.ToString() ?? string.Empty
                })))).ToList(),
            Temperature: options.Temperature ?? 0.0,
            MaxTokens: options.MaxOutputTokens ?? 0
        );
    }
}
