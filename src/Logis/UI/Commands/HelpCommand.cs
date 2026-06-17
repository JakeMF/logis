using Logis.Models;
using Spectre.Console;

namespace Logis.UI.Commands;

/// <summary>
/// Implements the /help command to list available commands and keyboard shortcuts.
/// </summary>
public class HelpCommand(CommandRegistry registry) : ISlashCommand
{
    public string Name => "help";
    public string Description => "List all available commands and keyboard shortcuts";

    public void Execute(string args, Session session)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]=== Logis Help ===[/]");
        AnsiConsole.WriteLine();

        // 1. Commands
        AnsiConsole.MarkupLine("[bold cyan]Commands:[/]");
        var table = new Table().Border(TableBorder.None).HideHeaders();
        table.AddColumn("Command");
        table.AddColumn("Description");

        foreach (var cmd in registry.GetCommands().OrderBy(c => c.Name))
        {
            table.AddRow($"[green]/{cmd.Name}[/]", $"[grey]{cmd.Description}[/]");
        }
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // 2. Shortcuts
        AnsiConsole.MarkupLine("[bold cyan]Keyboard Shortcuts:[/]");
        var shortcutTable = new Table().Border(TableBorder.None).HideHeaders();
        shortcutTable.AddColumn("Key");
        shortcutTable.AddColumn("Action");

        shortcutTable.AddRow("[yellow]Enter[/]", "[grey]Submit input to the model[/]");
        shortcutTable.AddRow("[yellow]Up/Down[/]", "[grey]Navigate session history[/]");
        shortcutTable.AddRow("[yellow]Left/Right[/]", "[grey]Move cursor[/]");
        shortcutTable.AddRow("[yellow]Home/End[/]", "[grey]Move cursor to start/end[/]");
        shortcutTable.AddRow("[yellow]Ctrl+Left/Right[/]", "[grey]Move cursor by word[/]");
        shortcutTable.AddRow("[yellow]Backspace[/]", "[grey]Delete character before cursor[/]");
        shortcutTable.AddRow("[yellow]Ctrl+U[/]", "[grey]Clear input line[/]");
        shortcutTable.AddRow("[yellow]Ctrl+K[/]", "[grey]Delete from cursor to end[/]");
        shortcutTable.AddRow("[yellow]Ctrl+C[/]", "[grey]Exit session[/]");

        AnsiConsole.Write(shortcutTable);
        AnsiConsole.WriteLine();
    }
}
