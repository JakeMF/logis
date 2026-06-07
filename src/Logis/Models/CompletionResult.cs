namespace Logis.Models;

public record CompletionResult(
    LogisRequest Request,
    string Content,
    string RawResponse,
    LogisUsage Usage,
    string FinishReason,
    string File,
    string Task
);
