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
    /// Scans user input for file mentions and adds them to the session's focused files.
    /// </summary>
    public void ScanForFileMentions(Session session, string input)
    {
        var matches = FilePathRegex().Matches(input);
        foreach (Match match in matches)
        {
            string path = match.Value;
            if (!session.Context.FocusedFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                // Verify existence before adding
                if (File.Exists(Path.Combine(session.Context.WorkspaceRoot, path)))
                {
                    session.Context.FocusedFiles.Add(path);
                }
            }
        }
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

    [GeneratedRegex(@"(?i)\b[\w\-\.]+\.(cs|js|py|ts|go|json|md|csproj|xml|yml|yaml)\b")]
    private static partial Regex FilePathRegex();
}
