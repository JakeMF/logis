namespace Logis;

[JsonSerializable(typeof(Logis.Models.ChatRequest))]
[JsonSerializable(typeof(Logis.Models.ChatResponse))]
[JsonSerializable(typeof(Logis.Models.Config))]
internal partial class LogisJsonContext : JsonSerializerContext { }
