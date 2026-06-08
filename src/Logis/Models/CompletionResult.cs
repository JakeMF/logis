namespace Logis.Models;

/// <summary>
/// The final output of a completion operation, containing the model's response, 
/// the original request, and all associated metadata for logging.
/// </summary>
public record CompletionResult(
    LogisRequest Request,
    string Content,
    string RawResponse,
    LogisUsage Usage,
    string FinishReason,
    string File,
    string Task
);
