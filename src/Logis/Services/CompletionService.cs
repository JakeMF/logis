using Logis.Tools;
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
    private const string SystemPrompt = 
        "You are a helpful assistant that edits code based on user instructions.\n" +
        "You have access to tools to explore the workspace and read other files if needed.\n" +
        "IMPORTANT: You already have the content of the target file. DO NOT call 'ReadFile' on the target file.\n" +
        "When you are ready to propose changes, return the complete modified file only.\n" +
        "Do not wrap your final response in markdown code fences or backticks.\n" +
        "Return only the raw file content with the requested changes applied.";

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
        Provider provider,
        LogisOptions options,
        StatusContext? statusContext = null)
    {
        // 1. Initialize the top-level OpenAI client
        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(provider.BaseUrl);
        }

        string apiKeyValue = string.IsNullOrWhiteSpace(provider.ApiKey) ? "local-dev" : provider.ApiKey;
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKeyValue), clientOptions);
        
        // 2. Build the ChatClient with Tool Calling Middleware
        var toolService = new FileSystemTools(filePath, statusContext); 
        
        // Use ChatClientBuilder to compose the middleware pipeline
        using IChatClient chatClient = new ChatClientBuilder(openAIClient.GetChatClient(provider.Model).AsIChatClient())
            .UseFunctionInvocation(configure: builder =>
            {
                builder.MaximumIterationsPerRequest = options.MaxToolIterations;
            })
            .Build();

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, SystemPrompt),
            new ChatMessage(ChatRole.User, $"Target file path: {filePath}\n\nFile contents:\n{fileContent}\n\nTask instruction:\n{task}")
        };

        var chatOptions = new ChatOptions
        {
            Temperature = (float)provider.Temperature,
            MaxOutputTokens = provider.MaxTokens,
            
            // Limit context window to prevent VRAM bloat (respects num_ctx in Ollama)
            AdditionalProperties = new() { ["num_ctx"] = provider.ContextWindow },

            // Register tools via AIFunctionFactory which uses the [Description] attributes from FileSystemTools
            Tools = [
                AIFunctionFactory.Create(toolService.ListDirectory), 
                AIFunctionFactory.Create(toolService.ReadFile)
            ]
        };

        if (options.Debug)
        {
            AnsiConsole.MarkupLine("[bold yellow]=== DEBUG: PROMPT MESSAGES ===[/]");
            foreach (var m in messages)
            {
                AnsiConsole.MarkupLine($"[blue]{m.Role}:[/] {Markup.Escape(m.Text ?? "[[Complex/Non-text Content]]")}");
            }

            AnsiConsole.MarkupLine("[bold yellow]=== DEBUG: TOOL DEFINITIONS ===[/]");
            foreach (var tool in chatOptions.Tools)
            {
                // In this version, tools are AIFunctions which expose metadata properties directly
                AnsiConsole.MarkupLine($"[green]Function:[/] {tool.Name}");
                AnsiConsole.MarkupLine($"[grey]Description:[/] {Markup.Escape(tool.Description ?? "No description")}");
            }
            AnsiConsole.WriteLine();
        }

        try
        {
            // 3. Execute the request
            // The FunctionInvocationMiddleware will handle the iterative loop of:
            // Model -> Tool Call -> Tool Execution -> Model -> ... until Stop or MaxIterations
            ChatResponse response = await chatClient.GetResponseAsync(messages, chatOptions);

            // 4. Capture raw representation for audit logging
            string rawResponseJson = response.RawRepresentation?.ToString() ?? "Empty raw response";

            // 5. Map the final response back to Logis models
            var result = new CompletionResult(
                Request: MapRequest(messages, chatOptions),
                Content: response.Text ?? string.Empty,
                RawResponse: rawResponseJson,
                Usage: new LogisUsage(
                    PromptTokens: (int)(response.Usage?.InputTokenCount ?? 0),
                    CompletionTokens: (int)(response.Usage?.OutputTokenCount ?? 0),
                    TotalTokens: (int)(response.Usage?.TotalTokenCount ?? 0)
                ),
                FinishReason: response.FinishReason?.ToString() ?? "unknown",
                File: filePath,
                Task: task
            );

            if (response.FinishReason == ChatFinishReason.Length)
            {
                throw new TruncationException(result);
            }

            return result;
        }
        catch (TruncationException) { throw; }
        catch (Exception ex)
        {
            throw new Exception($"Completion failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Maps the MEA request objects back to our local Logis models for logging.
    /// Captures multi-turn content including tool calls and results.
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
