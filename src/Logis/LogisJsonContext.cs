namespace Logis;

/// <summary>
/// Provides a source-generated JSON serialization context for the application's models.
/// This is required for Native AOT compatibility, as it avoids reflection-based 
/// deserialization at runtime.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Logis.Models.LogisRequest))]
[JsonSerializable(typeof(Logis.Models.LogisMessage))]
[JsonSerializable(typeof(Logis.Models.LogisResponse))]
[JsonSerializable(typeof(Logis.Models.LogisUsage))]
[JsonSerializable(typeof(Logis.Models.Config))]
[JsonSerializable(typeof(Logis.Models.CompletionResult))]
internal partial class LogisJsonContext : JsonSerializerContext { }
