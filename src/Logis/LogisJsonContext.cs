namespace Logis;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Logis.Models.LogisRequest))]
[JsonSerializable(typeof(Logis.Models.LogisMessage))]
[JsonSerializable(typeof(Logis.Models.LogisResponse))]
[JsonSerializable(typeof(Logis.Models.LogisUsage))]
[JsonSerializable(typeof(Logis.Models.Config))]
[JsonSerializable(typeof(Logis.Models.CompletionResult))]
internal partial class LogisJsonContext : JsonSerializerContext { }
