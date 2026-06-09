namespace Logis.Models;

/// <summary>
/// Represents the persistent application configuration loaded from 'logis.json'.
/// </summary>
public record Config(
    [property: JsonPropertyName("default_provider")] string DefaultProvider,
    [property: JsonPropertyName("providers")] Dictionary<string, Provider> Providers,
    [property: JsonPropertyName("log_dir")] string LogDir,
    [property: JsonPropertyName("verbose")] bool Verbose,
    [property: JsonPropertyName("max_tool_iterations")] int MaxToolIterations = 10
);

/// <summary>
/// Defines the connection and model settings for a specific AI provider (e.g., Ollama, OpenAI).
/// </summary>
public record Provider(
    [property: JsonPropertyName("base_url")] string BaseUrl,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("api_key")] string ApiKey,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("max_tokens")] int MaxTokens,
    [property: JsonPropertyName("context_window")] int ContextWindow = 4096
);
