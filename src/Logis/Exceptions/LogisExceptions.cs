namespace Logis.Exceptions;

/// <summary>
/// Thrown when an LLM response is cut short due to token limits, 
/// allowing the system to capture and log the partial output.
/// </summary>
public class TruncationException : Exception
{
    /// <summary>
    /// The partial completion result captured before truncation occurred.
    /// </summary>
    public CompletionResult PartialResult { get; }

    public TruncationException(CompletionResult partial)
        : base("Response truncated: model hit token limit mid-output — try a smaller file or shorter task")
    {
        PartialResult = partial;
    }
}
