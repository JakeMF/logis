using Microsoft.Data.Sqlite;

namespace Logis.Services;

/// <summary>
/// Manages the lifecycle, persistence, and indexing of interactive sessions.
/// Sessions are stored as append-only JSONL files for crash-resilience, 
/// with a SQLite database acting as a fast metadata index.
/// </summary>
public class SessionService
{
    private readonly string _baseDir;
    private readonly string _dbPath;
    private readonly LogisOptions _options;

    /// <summary>
    /// Initializes a new instance of the SessionService.
    /// Ensures the session directory and SQLite index are ready.
    /// </summary>
    /// <param name="options">Runtime options including debug/verbose flags.</param>
    public SessionService(LogisOptions options)
    {
        _options = options;
        _baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".logis");
        _dbPath = Path.Combine(_baseDir, "sessions.db");

        // Authority lives in the filesystem; sessions are grouped by workspace slug
        Directory.CreateDirectory(Path.Combine(_baseDir, "sessions"));
        InitializeDatabase();
    }

    /// <summary>
    /// Ensures the SQLite schema is present.
    /// SQLite stores metadata only; no turn content is held in the database.
    /// </summary>
    private void InitializeDatabase()
    {
        // SQLite is treated as a disposable "speed layer" for metadata only.
        // The actual conversation source-of-truth remains in the JSONL files.
        // This allows for easier management of data for user via JSONL files,
        // while improving query performance for the metadata index.
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                display_name TEXT,
                workspace_root TEXT,
                workspace_slug TEXT,
                session_path TEXT,
                started_at TEXT,
                last_active_at TEXT,
                state TEXT,
                provider TEXT,
                model TEXT
            );
            CREATE TABLE IF NOT EXISTS context_files (
                session_id TEXT REFERENCES sessions(id),
                file_path TEXT,
                file_hash TEXT,
                loaded_at TEXT,
                turn_id TEXT,
                PRIMARY KEY (session_id, file_path)
            );";
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Creates a new session for the current workspace.
    /// Sets up the directory structure: sessions/<slug>/<id>/tool-results/
    /// </summary>
    public Session CreateSession(string workspaceRoot)
    {
        var id = Guid.NewGuid().ToString("N");
        var slug = SanitizePath(workspaceRoot);
        
        // Grouping sessions by workspace slug on the filesystem prevents a single folder
        // from becoming a junkyard and enables easy manual browsing of project history.
        var sessionPath = Path.Combine(_baseDir, "sessions", slug, id);
        
        Directory.CreateDirectory(sessionPath);
        Directory.CreateDirectory(Path.Combine(sessionPath, "tool-results"));

        var session = new Session
        {
            Id = id,
            SessionPath = sessionPath,
            StartedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            State = SessionState.Idle,
            Context = new WorkingContext
            {
                WorkspaceRoot = workspaceRoot,
                WorkspaceSlug = slug
            }
        };

        SaveSessionToIndex(session);
        // Authority Sync: Load focused files from the metadata index.
        session.Context.FocusedFiles = GetFocusedFiles(session.Id);
        return session;
    }

    /// <summary>
    /// Retrieves a session by its ID from the metadata index.
    /// </summary>
    public Session? GetSession(string id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM sessions WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        var session = new Session
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            DisplayName = reader.IsDBNull(reader.GetOrdinal("display_name")) ? null : reader.GetString(reader.GetOrdinal("display_name")),
            SessionPath = reader.GetString(reader.GetOrdinal("session_path")),
            StartedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("started_at"))),
            LastActiveAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_active_at"))),
            State = Enum.Parse<SessionState>(reader.GetString(reader.GetOrdinal("state"))),
            Provider = reader.IsDBNull(reader.GetOrdinal("provider")) ? null : reader.GetString(reader.GetOrdinal("provider")),
            Model = reader.IsDBNull(reader.GetOrdinal("model")) ? null : reader.GetString(reader.GetOrdinal("model")),
            Context = new WorkingContext
            {
                WorkspaceRoot = reader.GetString(reader.GetOrdinal("workspace_root")),
                WorkspaceSlug = reader.GetString(reader.GetOrdinal("workspace_slug"))
            }
        };

        // Authority Sync: Load focused files from the metadata index.
        session.Context.FocusedFiles = GetFocusedFiles(session.Id);
        return session;
    }

    /// <summary>
    /// Rebuilds the SQLite index by scanning the session directories.
    /// Reads the first and last line of each JSONL to reconstruct metadata.
    /// </summary>
    public void RebuildIndex()
    {
        if (_options.Debug) Console.WriteLine("Rebuilding session index...");
        
        var sessionsDir = Path.Combine(_baseDir, "sessions");
        if (!Directory.Exists(sessionsDir)) return;

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        // Authority is the filesystem; we can safely wipe the index and start over
        var clearCmd = connection.CreateCommand();
        clearCmd.CommandText = "DELETE FROM sessions; DELETE FROM context_files;";
        clearCmd.ExecuteNonQuery();

        foreach (var slugDir in Directory.GetDirectories(sessionsDir))
        {
            foreach (var sessionDir in Directory.GetDirectories(slugDir))
            {
                var jsonlPath = Path.Combine(sessionDir, "session.jsonl");
                if (!File.Exists(jsonlPath)) continue;

                try
                {
                    // We optimize for speed by reading the file once and grabbing the first/last turns.
                    var lines = File.ReadLines(jsonlPath)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();

                    // Guard: We need at least one valid turn to reconstruct the session metadata.
                    if (lines.Count == 0 || 
                        JsonSerializer.Deserialize(lines[0], LogisJsonContext.Default.SessionTurn) is not { } firstTurn ||
                        JsonSerializer.Deserialize(lines[^1], LogisJsonContext.Default.SessionTurn) is not { } lastTurn)
                    {
                        if (_options.Debug) Console.Error.WriteLine($"Skipping session at {sessionDir}: Invalid or empty turn data.");
                        continue;
                    }

                    var session = new Session
                    {
                        Id = firstTurn.SessionId,
                        SessionPath = sessionDir,
                        StartedAt = firstTurn.Timestamp,
                        LastActiveAt = lastTurn.Timestamp,
                        // Handle potential state string corruption gracefully.
                        State = Enum.TryParse<SessionState>(lastTurn.StateAtTurn, out var state) ? state : SessionState.Idle,
                        Context = new WorkingContext
                        {
                            WorkspaceRoot = firstTurn.WorkspaceRoot ?? "Unknown",
                            WorkspaceSlug = Path.GetFileName(slugDir)
                        }
                    };

                    SaveSessionToIndex(session);
                }
                catch (Exception ex)
                {
                    if (_options.Debug) Console.Error.WriteLine($"Failed to index session at {sessionDir}: {ex.Message}");
                }
            }
        }
        
        if (_options.Debug) Console.WriteLine("Index rebuild complete.");
    }

    /// <summary>
    /// Persists session metadata to the SQLite index.
    /// </summary>
    public void SaveSessionToIndex(Session session)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO sessions (
                id, display_name, workspace_root, workspace_slug, session_path, 
                started_at, last_active_at, state, provider, model
            ) VALUES (
                $id, $display_name, $workspace_root, $workspace_slug, $session_path, 
                $started_at, $last_active_at, $state, $provider, $model
            )";
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$display_name", (object?)session.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$workspace_root", session.Context.WorkspaceRoot);
        command.Parameters.AddWithValue("$workspace_slug", session.Context.WorkspaceSlug);
        command.Parameters.AddWithValue("$session_path", session.SessionPath);
        command.Parameters.AddWithValue("$started_at", session.StartedAt.ToString("O"));
        command.Parameters.AddWithValue("$last_active_at", session.LastActiveAt.ToString("O"));
        command.Parameters.AddWithValue("$state", session.State.ToString());
        command.Parameters.AddWithValue("$provider", (object?)session.Provider ?? DBNull.Value);
        command.Parameters.AddWithValue("$model", (object?)session.Model ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Updates frequently-changing session metadata in the SQLite index.
    /// This is more efficient than SaveSessionToIndex for incremental updates.
    /// </summary>
    public void UpdateSessionMetadata(Session session)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE sessions SET 
                display_name = $display_name,
                last_active_at = $last_active_at,
                state = $state,
                provider = $provider,
                model = $model
            WHERE id = $id";
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$display_name", (object?)session.DisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("$last_active_at", session.LastActiveAt.ToString("O"));
        command.Parameters.AddWithValue("$state", session.State.ToString());
        command.Parameters.AddWithValue("$provider", (object?)session.Provider ?? DBNull.Value);
        command.Parameters.AddWithValue("$model", (object?)session.Model ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Appends a turn to the session's JSONL file.
    /// This is the primary persistence mechanism for the conversation.
    /// </summary>
    public void AppendTurn(Session session, SessionTurn turn)
    {
        var jsonlPath = Path.Combine(session.SessionPath, "session.jsonl");
        var json = JsonSerializer.Serialize(turn, LogisCompactContext.Default.SessionTurn);
        File.AppendAllText(jsonlPath, json + Environment.NewLine);

        session.LastActiveAt = DateTime.UtcNow;
        UpdateSessionMetadata(session);
    }

    /// <summary>
    /// Records a file being loaded into the session context.
    /// Metadata is stored in context_files to track staleness and redundant exploration.
    /// </summary>
    public void RecordFileLoad(string sessionId, string filePath, string hash, string turnId)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO context_files (session_id, file_path, file_hash, loaded_at, turn_id)
            VALUES ($session_id, $file_path, $file_hash, $loaded_at, $turn_id)";
        command.Parameters.AddWithValue("$session_id", sessionId);
        command.Parameters.AddWithValue("$file_path", filePath);
        command.Parameters.AddWithValue("$file_hash", hash);
        command.Parameters.AddWithValue("$loaded_at", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$turn_id", turnId);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Retrieves the last recorded hash for a file in a specific session.
    /// </summary>
    public string? GetFileHash(string sessionId, string filePath)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT file_hash FROM context_files WHERE session_id = $session_id AND file_path = $file_path";
        command.Parameters.AddWithValue("$session_id", sessionId);
        command.Parameters.AddWithValue("$file_path", filePath);

        return command.ExecuteScalar() as string;
    }

    /// <summary>
    /// Retrieves all files previously focused on in this session.
    /// </summary>
    public List<string> GetFocusedFiles(string sessionId)
    {
        var files = new List<string>();
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT file_path FROM context_files WHERE session_id = $session_id";
        command.Parameters.AddWithValue("$session_id", sessionId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            files.Add(reader.GetString(0));
        }
        return files;
    }

    /// <summary>
    /// Populates a session's in-memory history and focused files from its JSONL file.
    /// Should be called once during session initialization or resumption.
    /// </summary>
    public async Task LoadHistoryAsync(Session session, CancellationToken ct)
    {
        var jsonlPath = Path.Combine(session.SessionPath, "session.jsonl");
        if (!File.Exists(jsonlPath)) return;

        var lines = await File.ReadAllLinesAsync(jsonlPath, ct);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var turn = JsonSerializer.Deserialize(line, LogisCompactContext.Default.SessionTurn);
            if (turn == null) continue;

            var content = turn.Content;

            // Authority: If a result was spilled to disk (large output), we must load the full content
            // to ensure the LLM has the complete context during session resumption.
            if (!string.IsNullOrEmpty(turn.ToolResultPath))
            {
                var fullPath = Path.Combine(session.SessionPath, turn.ToolResultPath);
                if (File.Exists(fullPath))
                {
                    content = await File.ReadAllTextAsync(fullPath, ct);
                }
            }

            var role = turn.Role.ToLowerInvariant() switch
            {
                "user" => ChatRole.User,
                "assistant" => ChatRole.Assistant,
                "tool" => ChatRole.Tool,
                _ => ChatRole.System
            };

            // Restore focused files from tool metadata
            if (role == ChatRole.Tool && turn.ToolName == "ReadFileAsync" && !turn.Content.StartsWith("Error:"))
            {
                // In v0.7, we'll rely on the ToolCallId or content to infer the path if not explicit.
                // For now, we restore focus via metadata index during GetSession.
            }

            var message = new ChatMessage(role, content);
            if (role == ChatRole.Tool && !string.IsNullOrEmpty(turn.ToolCallId))
            {
                message.Contents = [ new FunctionResultContent(turn.ToolCallId, content) ];
            }
            else if (role == ChatRole.Assistant && !string.IsNullOrEmpty(turn.ToolCallId))
            {
                // Reconstruct the structured FunctionCallContent for assistant turns.
                // This ensures the LLM sees the Call -> Result pair correctly during resumption.
                message.Contents = [ new FunctionCallContent(turn.ToolCallId, turn.ToolName ?? "Unknown") ];
            }

            session.History.Add(message);
        }
    }

    private string SanitizePath(string path)
    {
        return path.Replace(":", "").Replace("\\", "_").Replace("/", "_").Trim('_');
    }
}
