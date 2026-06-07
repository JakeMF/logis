namespace Logis.Services;

public class CompletionService
{
    public async Task<CompletionResult> CompleteAsync(string file, string content, string task, Provider provider)
    {
        await Task.Yield();
        throw new NotImplementedException();
    }
}
