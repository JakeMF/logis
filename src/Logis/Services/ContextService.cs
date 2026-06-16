namespace Logis.Services;

/// <summary>
/// Assembles coarse context for the model turns, including proactive file loading
/// and staleness detection. Uses AOT-safe hashing and regex.
/// </summary>
public partial class ContextService
{
    private readonly WorkspaceService _workspace;
    private readonly SessionService _sessionService;

    public ContextService(WorkspaceService workspace, SessionService sessionService)
    {
        _workspace = workspace;
        _sessionService = sessionService;
    }

    /// <summary>
    /// Scans user input for file mentions. Note: Files are only added to 
    /// FocusedFiles after the model successfully reads them via a tool call.
    /// </summary>
    public void ScanForFileMentions(Session session, string input)
    {
        // Metadata scanning can still happen here if needed, 
        // but focusing is now tool-driven for authority.
    }

    /// <summary>
    /// Assembles the full content of all focused files into a single context block.
    /// Performs staleness checks against the session index.
    /// </summary>
    public async Task<string> AssembleContextAsync(Session session, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Active Workspace Context");
        sb.AppendLine($"Root: {session.Context.WorkspaceRoot}");
        sb.AppendLine();

        foreach (var relativePath in session.Context.FocusedFiles)
        {
            string fullPath = Path.Combine(session.Context.WorkspaceRoot, relativePath);
            if (!File.Exists(fullPath)) continue;

            string content = await _workspace.ReadFileAsync(fullPath, ct);
            string currentHash = CalculateHash(content);
            string? lastHash = _sessionService.GetFileHash(session.Id, relativePath);

            if (lastHash != null && lastHash != currentHash)
            {
                sb.AppendLine($"### FILE: {relativePath} [MODIFIED EXTERNALLY]");
            }
            else
            {
                sb.AppendLine($"### FILE: {relativePath}");
            }

            sb.AppendLine("```");
            sb.AppendLine(content);
            sb.AppendLine("```");
            sb.AppendLine();

            // Update the index with the fresh hash if it's new
            if (lastHash != currentHash)
            {
                _sessionService.RecordFileLoad(session.Id, relativePath, currentHash, "proactive-load");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Calculates a SHA256 hash of a string in an AOT-safe manner.
    /// </summary>
    private static string CalculateHash(string input)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(input);
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes);
    }

    [GeneratedRegex(@"(?i)\b[\w\-\.\\/]+\.[a-z0-9]{1,5}\b")]
    private static partial Regex FilePathRegex();
}
