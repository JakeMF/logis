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
[JsonSerializable(typeof(Logis.Models.SessionTurn))]
[JsonSerializable(typeof(Logis.Models.SessionState))]
[JsonSerializable(typeof(Logis.Models.Session))]
[JsonSerializable(typeof(Logis.Models.WorkingContext))]
internal partial class LogisJsonContext : JsonSerializerContext { }

/// <summary>
/// Provides a compact (single-line) JSON serialization context for JSONL storage.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Logis.Models.SessionTurn))]
internal partial class LogisCompactContext : JsonSerializerContext { }
