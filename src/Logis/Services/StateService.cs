namespace Logis.Services;

/// <summary>
/// Manages transitions and intent detection for the session state machine.
/// Uses Source Generated Regex for Native AOT compatibility.
/// </summary>
public partial class StateService
{
    /// <summary>
    /// Detects the user's intent from their input to suggest a state transition.
    /// Prioritizes Edit intent over simple research mentions.
    /// </summary>
    public SessionState DetectIntent(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return SessionState.Idle;

        // Edit intent takes precedence. If the user wants to change code, 
        // we check for those keywords first.
        if (EditKeywordsRegex().IsMatch(input))
        {
            return SessionState.Edit;
        }

        // If the user mentions a file extension or directory-like path, research is likely.
        if (FileMentionRegex().IsMatch(input) || ResearchKeywordsRegex().IsMatch(input))
        {
            return SessionState.Research;
        }

        return SessionState.Research; // Default to research if ambiguous.
    }

    /// <summary>
    /// Handles the transition logic for a session based on new input.
    /// Implements "Context Gating" to ensure files are researched before being edited.
    /// </summary>
    /// <param name="session">The active session.</param>
    /// <param name="userInput">The latest user input.</param>
    /// <param name="trace">Optional callback for diagnostic telemetry.</param>
    public void Transition(Session session, string userInput, Action<string>? trace = null)
    {
        var detected = DetectIntent(userInput);

        // Transition from Review back to Edit if the user provides feedback instead of confirmation.
        if (session.State == SessionState.Review && !IsConfirmation(userInput))
        {
            session.State = SessionState.Edit;
            return;
        }

        // Only handle Research/Edit transitions if we are currently Idle or Researching.
        if (session.State is SessionState.Idle or SessionState.Research)
        {
            if (detected == SessionState.Edit)
            {
                // CONTEXT GATE: Extract all potential files mentioned in the prompt.
                // We normalize slashes to ensure cross-platform consistency.
                var mentionedFiles = FileMentionRegex().Matches(userInput)
                    .Select(m => m.Value.Replace("/", "\\")) 
                    .Where(v => v.Contains('.')) 
                    .ToList();

                // If any mentioned file is NOT in the focused list, force Research.
                bool missingContext = mentionedFiles.Any(f => 
                    !session.Context.FocusedFiles.Any(focused => 
                    {
                        var normFocused = focused.Replace("/", "\\");
                        return normFocused.EndsWith(f, StringComparison.OrdinalIgnoreCase) || 
                               f.EndsWith(normFocused, StringComparison.OrdinalIgnoreCase);
                    }));

                // If NO files were mentioned but nothing is in focus, also force Research.
                if (mentionedFiles.Count == 0 && session.Context.FocusedFiles.Count == 0)
                {
                    missingContext = true;
                }

                if (missingContext)
                {
                    session.State = SessionState.Research;
                    trace?.Invoke($"Context Gate: Missing files. Mentions: {string.Join(", ", mentionedFiles)}. Focus: {string.Join(", ", session.Context.FocusedFiles)}.");
                }
                else
                {
                    session.State = SessionState.Edit;
                    trace?.Invoke("Context Gate: PASSED. Transitioning to EDIT.");
                }
            }
            else if (session.State == SessionState.Idle)
            {
                // Default transition from Idle.
                session.State = detected;
            }
        }
    }

    private bool IsConfirmation(string input)
    {
        var lowered = input.Trim().ToLowerInvariant();
        return lowered is "yes" or "y" or "ok" or "confirm" or "apply" or "proceed" or "go ahead" or "do it";
    }

    [GeneratedRegex(@"(?i)\b[\w\-\.\\/]+\.[a-z0-9]{1,5}\b")]
    private static partial Regex FileMentionRegex();

    [GeneratedRegex(@"(?i)\b(find|search|show|where|what|how|look|read|list|explore)\b")]
    private static partial Regex ResearchKeywordsRegex();

    [GeneratedRegex(@"(?i)\b(fix|add|change|refactor|update|modify|remove|delete|create|edit|write|apply|proceed|implement|do it|go ahead)\b")]
    private static partial Regex EditKeywordsRegex();
}
