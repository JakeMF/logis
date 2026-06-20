namespace Logis.Services;

/// <summary>
/// Manages state-specific system prompt fragments (Skills) to guide model behavior.
/// </summary>
public class SkillService
{
    /// <summary>
    /// Returns the system prompt fragment associated with the current session state.
    /// </summary>
    /// <param name="state">The current state of the session.</param>
    /// <param name="editFormat">The configured edit format (Whole or Diff).</param>
    /// <param name="focusedFiles">The files currently in the model's focus/memory.</param>
    /// <returns>A string containing behavioral instructions for the model.</returns>
    public string GetSkill(SessionState state, EditFormat editFormat, IEnumerable<string> focusedFiles)
    {
        return state switch
        {
            SessionState.Research => GetResearchSkill(),
            SessionState.Edit => GetEditSkill(editFormat, focusedFiles),
            SessionState.Review => GetReviewSkill(),
            _ => string.Empty
        };
    }

    private string GetResearchSkill()
    {
        return @"## Skill: Research & Exploration
Explore the workspace to understand the relevant code and architecture. 
- Use 'ListDirectory' to find files.
- Use 'ReadFile' to understand file contents.
- Use 'CreateFile' to create a new empty file if you need to add a new file.
- Do NOT propose edits yet; your goal is to gather sufficient context.
- When you have a solid understanding, summarize your findings and wait for instructions.";
    }

    private string GetEditSkill(EditFormat format, IEnumerable<string> focusedFiles)
    {
        var targetList = string.Join(", ", focusedFiles);
        var baseInstruction = $@"## Skill: Precision Editing
You are in EDIT mode. You have already researched and have the context for: [{targetList}].
Your goal is to apply the user's instructions to these files.
- Do NOT use 'ReadFile' on these files again; you already have their content.
- If you need to see another file to finish the task, you may still use tools.
- Maintain existing coding style and conventions.
- Be surgical; avoid unrelated changes.";

        if (format == EditFormat.Diff)
        {
            return baseInstruction + @"
- Propose specific changes using Search/Replace blocks.
- Use the following format for EVERY change:
[[SEARCH]]
[exact lines to find]
[[REPLACE]]
[replacement lines]
[[END]]
- If you want to replace the whole file, you can omit [[SEARCH]] and return only [[REPLACE]] and [[END]].";
        }

        return baseInstruction + @"
- Provide the complete, updated content of the file.
- Do NOT wrap your final response in markdown code fences or backticks.
- Return only the raw file content with the requested changes applied.";
    }

    private string GetReviewSkill()
    {
        return @"## Skill: Explanation & Feedback
The proposed changes are currently being reviewed by the user.
- Explain WHAT you changed and WHY.
- Answer any questions the user has about the implementation.
- If the user provides feedback, acknowledge it and prepare to adjust the implementation.";
    }
}
