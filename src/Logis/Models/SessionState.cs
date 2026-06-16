namespace Logis.Models;

/// <summary>
/// Defines the coarse-grained states of an interactive session.
/// </summary>
public enum SessionState
{
    /// <summary>
    /// Session is open and awaiting user input.
    /// </summary>
    Idle,

    /// <summary>
    /// Harness or model is gathering context (listing directories, reading files).
    /// </summary>
    Research,

    /// <summary>
    /// Model is producing an edit.
    /// </summary>
    Edit,

    /// <summary>
    /// Edit proposed, awaiting user confirmation or feedback.
    /// </summary>
    Review,

    /// <summary>
    /// Unrecoverable state that requires user intervention.
    /// </summary>
    Error
}
