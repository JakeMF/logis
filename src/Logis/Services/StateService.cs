namespace Logis.Services;

/// <summary>
/// Manages transitions and intent detection for the session state machine.
/// Uses Source Generated Regex for Native AOT compatibility.
/// </summary>
public partial class StateService
{
    /// <summary>
    /// Detects the user's intent from their input to suggest a state transition.
    /// </summary>
    /// <param name="input">The raw user input string.</param>
    /// <returns>The detected SessionState.</returns>
    public SessionState DetectIntent(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return SessionState.Idle;

        // If the user mentions a file extension or directory-like path, research is likely.
        if (FileMentionRegex().IsMatch(input) || ResearchKeywordsRegex().IsMatch(input))
        {
            return SessionState.Research;
        }

        // Keywords that signal a desire to modify code.
        if (EditKeywordsRegex().IsMatch(input))
        {
            return SessionState.Edit;
        }

        return SessionState.Research; // Default to research if ambiguous.
    }

    /// <summary>
    /// Handles the transition logic for a session based on new input.
    /// </summary>
    /// <param name="session">The active session to transition.</param>
    /// <param name="userInput">The most recent user message.</param>
    public void Transition(Session session, string userInput)
    {
        // If we are currently in Idle, detect the next phase.
        if (session.State == SessionState.Idle)
        {
            session.State = DetectIntent(userInput);
        }
        // If we are in Review and user gives feedback, move back to Edit.
        else if (session.State == SessionState.Review && !IsConfirmation(userInput))
        {
            session.State = SessionState.Edit;
        }
        // Transitions to Review are handled by CompletionService when it sees model output.
    }

    private bool IsConfirmation(string input)
    {
        var lowered = input.Trim().ToLowerInvariant();
        return lowered is "yes" or "y" or "ok" or "confirm" or "apply";
    }

    [GeneratedRegex(@"(?i)\b[\w\-\.]+\.(cs|js|py|ts|go|json|md|csproj|xml|yml|yaml)\b|\\|/")]
    private static partial Regex FileMentionRegex();

    [GeneratedRegex(@"(?i)\b(find|search|show|where|what|how|look|read|list)\b")]
    private static partial Regex ResearchKeywordsRegex();

    [GeneratedRegex(@"(?i)\b(fix|add|change|refactor|update|modify|remove|delete|create)\b")]
    private static partial Regex EditKeywordsRegex();
}
