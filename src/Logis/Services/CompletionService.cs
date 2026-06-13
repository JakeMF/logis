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
    private const string WholeSystemPrompt = 
        "You are a helpful assistant that edits code based on user instructions.\n" +
        "You have access to tools to explore the workspace and read other files if needed.\n" +
        "IMPORTANT: You already have the content of the target file. DO NOT call 'ReadFile' on the target file.\n" +
        "When you are ready to propose changes, return the complete modified file only.\n" +
        "Do not wrap your final response in markdown code fences or backticks.\n" +
        "Return only the raw file content with the requested changes applied.";

    private const string DiffSystemPrompt = 
        "You are a helpful assistant that edits code based on user instructions.\n" +
        "You have access to tools to explore the workspace and read other files if needed.\n" +
        "IMPORTANT: You already have the content of the target file. DO NOT call 'ReadFile' on the target file.\n" +
        "When you are ready to propose changes, return ONLY the specific Search/Replace blocks needed.\n" +
        "Use the following format for EVERY change:\n" +
        "[[SEARCH]]\n" +
        "[exact lines to find]\n" +
        "[[REPLACE]]\n" +
        "[replacement lines]\n" +
        "[[END]]\n" +
        "If you want to replace the whole file, you can omit [[SEARCH]] and return only [[REPLACE]] and [[END]].";

    /// <summary>
    /// Sends the file content and task to the model and returns the result.
    /// Handles tool calls and multi-turn interaction.
    /// </summary>
    /// <param name="filePath">The path to the file (for metadata).</param>
    /// <param name="fileContent">The actual content of the file.</param>
    /// <param name="task">The instruction for the model.</param>
    /// <param name="provider">The provider configuration to use.</param>
    /// <param name="options">Runtime options including max tool iterations.</param>
    /// <param name="statusContext">Optional Spectre StatusContext for UI updates.</param>
    /// <returns>A structured <see cref="CompletionResult"/>.</returns>
    /// <exception cref="TruncationException">Thrown if the model hits its token limit.</exception>
    /// <exception cref="Exception">Thrown for API or parsing errors.</exception>
    public async Task<CompletionResult> CompleteAsync(
        string filePath, 
        string fileContent, 
        string task, 
        string providerId,
        Provider provider,
        LogisOptions options,
        StatusContext? statusContext = null)
    {
        // 1. Initialize Clients
        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(provider.BaseUrl);
        }

        string apiKeyValue = string.IsNullOrWhiteSpace(provider.ApiKey) ? "local-dev" : provider.ApiKey;
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKeyValue), clientOptions);
        
        // Use raw IChatClient without automatic middleware
        using IChatClient chatClient = openAIClient.GetChatClient(provider.Model).AsIChatClient();

        // 2. Prepare Tools and State
        var toolService = new FileSystemTools(filePath); // Tools are now pure
        var functions = new List<AIFunction>
        {
            AIFunctionFactory.Create(toolService.ListDirectory), 
            AIFunctionFactory.Create(toolService.ReadFile)
        };

        string systemPrompt = options.EditFormat == EditFormat.Diff ? DiffSystemPrompt : WholeSystemPrompt;

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, $"Target file path: {filePath}\n\nFile contents:\n{fileContent}\n\nTask instruction:\n{task}")
        };

        var chatOptions = new ChatOptions
        {
            Temperature = (float)provider.Temperature,
            MaxOutputTokens = provider.MaxTokens,
            AdditionalProperties = new() { ["num_ctx"] = provider.ContextWindow },
            Tools = functions.Cast<AITool>().ToList()
        };

        if (options.Debug)
        {
            TraceToolDefinitions(functions);
        }

        ChatResponse? lastResponse = null;
        int iterations = 0;
        int totalToolCalls = 0;
        var toolCallHistory = new HashSet<string>();

        try
        {
            // 3. Manual Orchestration Loop
            while (iterations < options.MaxToolIterations)
            {
                iterations++;
                
                if (options.Debug)
                {
                    TraceIteration(iterations, messages.Count);
                }

                // Hard-Lock Strategy: On the absolute final turn, strip tool access to force a text response.
                if (iterations == options.MaxToolIterations)
                {
                    // Create a tool-less copy of options since ChatOptions is a class
                    chatOptions = new ChatOptions
                    {
                        Temperature = chatOptions.Temperature,
                        MaxOutputTokens = chatOptions.MaxOutputTokens,
                        AdditionalProperties = chatOptions.AdditionalProperties,
                        Tools = null
                    };
                    
                    string hardLockMessage = options.EditFormat == EditFormat.Diff
                        ? "FINAL ATTEMPT: Tool access is now disabled. You MUST return your Search/Replace blocks now. Do not return the whole file."
                        : "FINAL ATTEMPT: Tool access is now disabled. You MUST propose the final modified file contents based on your research now.";
                    messages.Add(new ChatMessage(ChatRole.System, hardLockMessage));

                    if (options.Debug)
                    {
                        AnsiConsole.MarkupLine("[bold red]DEBUG: Hard-Lock Engaged (Tools Stripped)[/]");
                    }
                }

                lastResponse = await chatClient.GetResponseAsync(messages, chatOptions);
                messages.Add(lastResponse.Messages[0]);

                if (lastResponse.FinishReason != ChatFinishReason.ToolCalls)
                {
                    break;
                }

                // Process Tool Calls
                foreach (var content in lastResponse.Messages[0].Contents)
                {
                    if (content is FunctionCallContent toolCall)
                    {
                        totalToolCalls++;
                        var tool = functions.FirstOrDefault(t => t.Name == toolCall.Name);
                        if (tool != null)
                        {
                            string argList = string.Join(", ", toolCall.Arguments?.Select(a => $"{a.Key}: {a.Value}") ?? []);
                            
                            // Simple Loop Detection: Prevent the model from calling the same tool with same args twice.
                            string callKey = $"{toolCall.Name}({argList})";
                            if (toolCallHistory.Contains(callKey))
                            {
                                if (options.Debug)
                                {
                                    AnsiConsole.MarkupLine($"[bold red]DEBUG: Loop Detected -> {Markup.Escape(callKey)}[/]");
                                }
                                
                                messages.Add(new ChatMessage(ChatRole.Tool, "Error: Recursive tool call detected. You already have the result for this exact action. DO NOT repeat calls. Propose your final changes now.") 
                                { 
                                    Contents = [ new FunctionResultContent(toolCall.CallId, "Error: Recursive call.") ] 
                                });
                                continue;
                            }

                            toolCallHistory.Add(callKey);

                            if (options.Debug)
                            {
                                TraceToolCall(toolCall.Name, argList);
                            }
                            else
                            {
                                statusContext?.Status("[grey]Researching...[/]");
                            }
                            
                            object? result = await tool.InvokeAsync(new AIFunctionArguments(toolCall.Arguments));
                            
                            messages.Add(new ChatMessage(ChatRole.Tool, result?.ToString() ?? string.Empty) 
                            { 
                                Contents = [ new FunctionResultContent(toolCall.CallId, result?.ToString()) ] 
                            });
                        }
                    }
                }

                // Loop Hardening: Inject reminder if hitting turn threshold
                if (iterations >= 3)
                {
                    string turnThresholdMessage = options.EditFormat == EditFormat.Diff
                        ? "Reminder: You have performed multiple research turns. Please prioritize returning your Search/Replace blocks now. Do not return the whole file."
                        : "Reminder: You have performed multiple research turns. Please prioritize proposing the final modified file now.";
                    messages.Add(new ChatMessage(ChatRole.System, turnThresholdMessage));
                    messages.Add(new ChatMessage(ChatRole.System, ""));
                }
            }

            if (lastResponse == null) throw new Exception("Model returned no response.");

            if (options.Debug)
            {
                AnsiConsole.MarkupLine($"[yellow]DEBUG: Raw finish reason: {Markup.Escape(lastResponse.FinishReason.ToString() ?? "NULL")}[/]");
                AnsiConsole.MarkupLine($"[yellow]DEBUG: Text: '{Markup.Escape(lastResponse.Text ?? "NULL")}'[/]");
                AnsiConsole.MarkupLine($"[yellow]DEBUG: Content blocks: {lastResponse.Messages[0].Contents.Count}[/]");
                foreach (var c in lastResponse.Messages[0].Contents)
                    AnsiConsole.MarkupLine($"[yellow]  -> {c.GetType().Name}: {Markup.Escape(c.ToString() ?? "")}[/]");
            }

            // 4. Final Processing
            var resultObj = new CompletionResult(
                Request: MapRequest(messages, chatOptions),
                Content: lastResponse.Text ?? string.Empty,
                RawResponse: lastResponse.RawRepresentation?.ToString() ?? "Empty raw response",
                Usage: new LogisUsage(
                    PromptTokens: (int)(lastResponse.Usage?.InputTokenCount ?? 0),
                    CompletionTokens: (int)(lastResponse.Usage?.OutputTokenCount ?? 0),
                    TotalTokens: (int)(lastResponse.Usage?.TotalTokenCount ?? 0)
                ),
                FinishReason: lastResponse.FinishReason?.ToString() ?? "unknown",
                File: filePath,
                Task: task,
                Model: provider.Model,
                ProviderId: providerId,
                EditFormat: options.EditFormat,
                ToolCallCount: totalToolCalls
            );

            if (lastResponse.FinishReason == ChatFinishReason.Length)
            {
                throw new TruncationException(resultObj);
            }

            return resultObj;
        }
        catch (TruncationException) { throw; }
        catch (Exception ex)
        {
            throw new Exception($"Completion failed: {ex.Message}", ex);
        }
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
