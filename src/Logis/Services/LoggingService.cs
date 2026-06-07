namespace Logis.Services;

/// <summary>
/// Handles structured logging of model runs, ensuring "maximum visibility" into 
/// the prompt sent and the raw response received.
/// </summary>
public class LoggingService
{
    /// <summary>
    /// Writes a detailed log of the completion run to the configured log directory.
    /// </summary>
    /// <param name="result">The result of the completion turn.</param>
    /// <param name="config">The current application configuration.</param>
    public void LogRun(CompletionResult result, Config config)
    {
        try
        {
            // Ensure the log directory exists
            if (!Directory.Exists(config.LogDir))
            {
                Directory.CreateDirectory(config.LogDir);
            }

            // Generate a timestamped filename: logis_run_YYYY-MM-DD_HH-mm-ss-filename.log
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string safeFileName = Path.GetFileName(result.File);
            string logFileName = $"logis_run_{timestamp}-{safeFileName}.log";
            string logFilePath = Path.Combine(config.LogDir, logFileName);

            // Build the log content following the design document's structure
            string logContent = FormatLog(result, config);

            File.WriteAllText(logFilePath, logContent);
        }
        catch (Exception ex)
        {
            // Fail loudly if we can't log, as visibility is a core principle
            Console.Error.WriteLine($"Warning: Failed to write log file: {ex.Message}");
        }
    }

    /// <summary>
    /// Formats the completion result into a human-readable structured log.
    /// </summary>
    private string FormatLog(CompletionResult result, Config config)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== LOGIS RUN ===");
        sb.AppendLine($"Timestamp: {DateTime.Now:O}");
        sb.AppendLine($"File:      {result.File}");
        sb.AppendLine($"Task:      {result.Task}");
        sb.AppendLine($"Provider:  {config.DefaultProvider}");
        sb.AppendLine();

        sb.AppendLine("=== REQUEST ===");
        // Serialize the request using the AOT-safe context
        string requestJson = JsonSerializer.Serialize(result.Request, LogisJsonContext.Default.LogisRequest);
        sb.AppendLine(requestJson);
        sb.AppendLine();

        sb.AppendLine("=== RAW RESPONSE ===");
        // Raw response is already a string captured from the provider SDK
        sb.AppendLine(result.RawResponse);
        sb.AppendLine();

        sb.AppendLine("=== STATUS ===");
        sb.AppendLine($"Finish Reason: {result.FinishReason}");
        sb.AppendLine($"Tokens — Prompt: {result.Usage.PromptTokens}, Completion: {result.Usage.CompletionTokens}, Total: {result.Usage.TotalTokens}");

        return sb.ToString();
    }
}