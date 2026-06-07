namespace Logis.Services;

using OpenAI;
using System.ClientModel;

/// <summary>
/// Orchestrates the interaction with the AI model, handling prompt construction, 
/// API communication, and response parsing.
/// </summary>
public class CompletionService
{
    private const string SystemPrompt = 
        "You are a helpful assistant that edits code based on user instructions.\n" +
        "You will be given the contents of a file and a task instruction.\n" +
        "Return the complete modified file only.\n" +
        "Do not wrap your response in markdown code fences or backticks.\n" +
        "Return only the raw file content with the requested changes applied.";

    /// <summary>
    /// Sends the file content and task to the model and returns the result.
    /// </summary>
    /// <param name="filePath">The path to the file (for metadata).</param>
    /// <param name="fileContent">The actual content of the file.</param>
    /// <param name="task">The instruction for the model.</param>
    /// <param name="provider">The provider configuration to use.</param>
    /// <returns>A structured <see cref="CompletionResult"/>.</returns>
    /// <exception cref="TruncationException">Thrown if the model hits its token limit.</exception>
    /// <exception cref="Exception">Thrown for API or parsing errors.</exception>
    public async Task<CompletionResult> CompleteAsync(string filePath, string fileContent, string task, Provider provider)
    {
        // 1. Initialize the top-level OpenAI client
        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(provider.BaseUrl))
        {
            clientOptions.Endpoint = new Uri(provider.BaseUrl);
        }

        // Protect against null/empty strings to avoid validation crash
        string apiKeyValue = string.IsNullOrWhiteSpace(provider.ApiKey) ? "local-dev" : provider.ApiKey;
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKeyValue), clientOptions);
        
        // 2. Get the model-specific client and wrap it in the MEA IChatClient abstraction
        // We use the extension method from Microsoft.Extensions.AI.OpenAI
        using IChatClient chatClient = openAIClient.GetChatClient(provider.Model).AsIChatClient();

        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, SystemPrompt),
            new ChatMessage(ChatRole.User, $"File contents:\n{fileContent}\n\nTask instruction:\n{task}")
        };

        var options = new ChatOptions
        {
            Temperature = (float)provider.Temperature,
            MaxOutputTokens = provider.MaxTokens
        };

        try
        {
            // 3. Execute the request using the unified MEA interface
            ChatResponse response = await chatClient.GetResponseAsync(messages, options);

            // 4. Capture raw representation for "maximum visibility"
            string rawResponseJson = "Raw data unavailable";
            if (response.RawRepresentation != null)
            {
                rawResponseJson = response.RawRepresentation.ToString() ?? "Empty raw response";
            }

            // 5. Map the abstraction response back to our local Logis models
            var result = new CompletionResult(
                Request: MapRequest(messages, options),
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

            // 6. Fail Loudly on truncation
            if (response.FinishReason == ChatFinishReason.Length)
            {
                throw new TruncationException(result);
            }

            if (response.FinishReason != ChatFinishReason.Stop)
            {
                throw new Exception($"Unexpected finish reason: {response.FinishReason}");
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
    /// </summary>
    private LogisRequest MapRequest(List<ChatMessage> messages, ChatOptions options)
    {
        return new LogisRequest(
            Messages: messages.Select(m => new LogisMessage(m.Role.ToString(), m.Text ?? string.Empty)).ToList(),
            Temperature: options.Temperature ?? 0.0,
            MaxTokens: options.MaxOutputTokens ?? 0
        );
    }
}
