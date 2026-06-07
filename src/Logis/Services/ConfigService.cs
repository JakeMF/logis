namespace Logis.Services;

/// <summary>
/// Manages application configuration, including loading from disk and providing 
/// hardcoded defaults when no configuration is found.
/// </summary>
public class ConfigService
{
    private const string ConfigFileName = "logis.json";

    /// <summary>
    /// Loads the configuration from the local logis.json file.
    /// Falls back to hardcoded defaults if the file is missing.
    /// </summary>
    /// <returns>A validated <see cref="Config"/> record.</returns>
    /// <exception cref="Exception">Thrown if the config exists but is malformed or inaccessible.</exception>
    public Config LoadConfig()
    {
        // Silently fall back to defaults if no config file is present
        if (!File.Exists(ConfigFileName))
        {
            return DefaultConfig();
        }

        try
        {
            string json = File.ReadAllText(ConfigFileName);
            
            // NOTE: Using the source-generated context here is critical for Native AOT compatibility.
            // This avoids reflection-based deserialization at runtime.
            var config = JsonSerializer.Deserialize(json, LogisJsonContext.Default.Config);
            
            return config ?? throw new JsonException("Parsed config was null.");
        }
        catch (JsonException ex)
        {
            // Fail loudly if the file exists but is broken
            throw new Exception($"Error parsing {ConfigFileName}: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new Exception($"Unexpected error loading configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Returns the hardcoded fallback configuration.
    /// </summary>
    private Config DefaultConfig()
    {
        return new Config(
            DefaultProvider: "ollama",
            Providers: new Dictionary<string, Provider>
            {
                ["ollama"] = new Provider(
                    BaseUrl: "http://localhost:11434/v1",
                    Model: "qwen3.5:4b",
                    ApiKey: "",
                    Temperature: 0.3,
                    MaxTokens: 4096
                )
            },
            LogDir: "logs",
            Verbose: false
        );
    }
}
