namespace Logis.Exceptions;

public class TruncationException : Exception
{
    public CompletionResult PartialResult { get; }

    public TruncationException(CompletionResult partial)
        : base("Response truncated: model hit token limit mid-output — try a smaller file or shorter task")
    {
        PartialResult = partial;
    }
}
