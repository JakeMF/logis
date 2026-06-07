namespace Logis.Models;

public record CompletionResult(
    ChatRequest Request,
    string Content,
    string RawResponse,
    Usage Usage,
    string FinishReason,
    string File,
    string Task
);
