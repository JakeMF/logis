using Logis.Models;

namespace Logis.UI;

/// <summary>
/// Defines a command that can be executed directly by the user via the input bar,
/// bypassing the LLM completion loop.
/// </summary>
public interface ISlashCommand
{
    /// <summary>
    /// The name of the command (e.g., "help", "clear").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A brief description of what the command does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes the command logic.
    /// </summary>
    /// <param name="args">The raw argument string following the command name.</param>
    /// <param name="session">The active session context.</param>
    void Execute(string args, Session session);
}

/// <summary>
/// A registry for managing and routing slash commands.
/// </summary>
public class CommandRegistry
{
    private readonly Dictionary<string, ISlashCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a command in the registry.
    /// </summary>
    public void Register(ISlashCommand command)
    {
        _commands[command.Name] = command;
    }

    /// <summary>
    /// Attempts to route and execute a command based on user input.
    /// </summary>
    /// <param name="input">The raw user input starting with '/'.</param>
    /// <param name="session">The active session context.</param>
    /// <returns>True if the command was found and executed; otherwise false.</returns>
    public bool TryExecute(string input, Session session)
    {
        if (string.IsNullOrWhiteSpace(input) || !input.StartsWith('/')) return false;

        // Skip the leading '/' and split the command name from its arguments.
        // Routing happens here to ensure "administrative" commands never leak 
        // into the LLM's conversation history or trigger the model loop.
        var parts = input[1..].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        string commandName = parts[0];
        string args = parts.Length > 1 ? parts[1] : string.Empty;

        if (_commands.TryGetValue(commandName, out var command))
        {
            command.Execute(args, session);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns all registered commands.
    /// </summary>
    public IEnumerable<ISlashCommand> GetCommands() => _commands.Values;
}
