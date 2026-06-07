namespace Logis.Models;

public record Config(
    [property: JsonPropertyName("default_provider")] string DefaultProvider,
    [property: JsonPropertyName("providers")] Dictionary<string, Provider> Providers,
    [property: JsonPropertyName("log_dir")] string LogDir,
    [property: JsonPropertyName("verbose")] bool Verbose
);

public record Provider(
    [property: JsonPropertyName("base_url")] string BaseUrl,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("api_key")] string ApiKey,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("max_tokens")] int MaxTokens
);
