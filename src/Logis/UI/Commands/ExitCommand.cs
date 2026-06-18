using Logis.Models;

namespace Logis.UI.Commands;

/// <summary>
/// A command that gracefully terminates the interactive session.
/// </summary>
public class ExitCommand : ISlashCommand
{
    public string Name => "exit";
    public string Description => "Gracefully terminates the interactive session.";

    public void Execute(string args, Session session)
    {
        // Throwing OperationCanceledException is an idiomatic way to signal 
        // that the user has requested a stop in a task-based system.
        // This will be caught by the interactive loop in Program.cs.
        throw new OperationCanceledException("User requested exit.");
    }
}
