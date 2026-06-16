namespace Logis.Models;

/// <summary>
/// Represents a persistent interactive conversation session.
/// </summary>
public class Session
{
    /// <summary>
    /// Unique identifier for the session (ULID or UUID).
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Optional display name for the session.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Full path to the session directory on disk.
    /// </summary>
    public string SessionPath { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when the session was created.
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Timestamp of the last activity in the session.
    /// </summary>
    public DateTime LastActiveAt { get; set; }

    /// <summary>
    /// The current working context (focused files, workspace root).
    /// </summary>
    public WorkingContext Context { get; set; } = new();

    /// <summary>
    /// The current coarse-grained state of the session.
    /// </summary>
    public SessionState State { get; set; }

    /// <summary>
    /// The provider used in the last turn.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// The model used in the last turn.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// In-memory record of the conversation history for the current resident process.
    /// The JSONL file remains the durable source of truth across runs.
    /// </summary>
    [JsonIgnore]
    public List<ChatMessage> History { get; } = new();
}

/// <summary>
/// Represents an immutable snapshot of a single exchange in a session.
/// </summary>
public record SessionTurn(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("session_id")] string SessionId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("tool_name")] string? ToolName,
    [property: JsonPropertyName("tool_call_id")] string? ToolCallId,
    [property: JsonPropertyName("state_at_turn")] string StateAtTurn,
    [property: JsonPropertyName("finish_reason")] string? FinishReason,
    [property: JsonPropertyName("token_count")] int? TokenCount,
    [property: JsonPropertyName("iteration")] int? Iteration,
    [property: JsonPropertyName("is_pinned")] bool IsPinned,
    [property: JsonPropertyName("is_summary")] bool IsSummary,
    [property: JsonPropertyName("tool_result_path")] string? ToolResultPath,
    [property: JsonPropertyName("workspace_root")] string? WorkspaceRoot,
    [property: JsonPropertyName("timestamp")] DateTime Timestamp
);

/// <summary>
/// Tracks the files and workspace root associated with a session.
/// </summary>
public class WorkingContext
{
    /// <summary>
    /// List of absolute paths to files the user has focused on.
    /// </summary>
    public List<string> FocusedFiles { get; set; } = new();

    /// <summary>
    /// The root directory of the workspace.
    /// </summary>
    public string WorkspaceRoot { get; set; } = string.Empty;

    /// <summary>
    /// A sanitized version of the workspace path used in the session directory.
    /// </summary>
    public string WorkspaceSlug { get; set; } = string.Empty;
}
