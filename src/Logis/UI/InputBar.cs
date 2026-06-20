using System.Text;
using Logis.Models;

namespace Logis.UI;

/// <summary>
/// A high-performance terminal input handler that provides a custom line buffer,
/// cursor navigation, history management, and real-time status display.
/// </summary>
public class InputBar
{
    private readonly Session _session;
    private readonly StatusLine _statusLine;
    private readonly CommandRegistry _registry;
    
    // Input State
    private readonly StringBuilder _input = new();
    private int _cursorIndex = 0;
    
    // History State
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private string _savedPartial = string.Empty;

    // Autocomplete State
    private int _menuSelectionIndex = -1;
    private string _savedQuery = string.Empty;
    private int _lastMenuHeight = 0;

    // UI State
    private int _lastInputLines = -1;
    private int _lastCursorLine = 0;

    public InputBar(Session session, StatusLine statusLine, CommandRegistry registry)
    {
        _session = session;
        _statusLine = statusLine;
        _registry = registry;
    }

    /// <summary>
    /// Reads a line of input from the user, handling all navigation and history shortcuts.
    /// Redraws the input bar and status line on every keystroke.
    /// </summary>
    public async Task<string> ReadLineAsync(CancellationToken ct)
    {
        // Reset state for new input
        _input.Clear();
        _cursorIndex = 0;
        _historyIndex = -1;
        _savedPartial = string.Empty;
        _menuSelectionIndex = -1;
        _savedQuery = string.Empty;
        _lastMenuHeight = 0;
        _lastInputLines = -1;
        _lastCursorLine = 0;

        Redraw();

        while (!ct.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(10, ct); // High frequency polling for 0ms latency feel
                continue;
            }

            var key = Console.ReadKey(intercept: true);

            // 1. Handle Submission & Exit
            if (key.Key == ConsoleKey.Enter)
            {
                if (_menuSelectionIndex != -1)
                {
                    var matches = GetActiveMatches();
                    if (_menuSelectionIndex >= 0 && _menuSelectionIndex < matches.Count)
                    {
                        _input.Clear();
                        _input.Append($"/{matches[_menuSelectionIndex].Name}");
                        _cursorIndex = _input.Length;
                    }
                }

                CommitAutocompleteSelection();
                Redraw(); // Wipe the menu from the screen before submitting

                string result = _input.ToString();
                if (string.IsNullOrWhiteSpace(result)) continue;

                // Clear the UI block (separator, input line, spacer, status line) and print final input in default colors
                int totalLines = _lastInputLines + 4;
                int linesUp = _lastCursorLine + 1;
                int width = 80;
                try { width = Console.WindowWidth; } catch {}
                if (width <= 0) width = 80;

                var clearSb = new StringBuilder();
                clearSb.Append("\x1b[?25l"); // Hide cursor
                
                // Move up to the separator line
                for (int i = 0; i < linesUp; i++) clearSb.Append("\x1b[A");
                
                // Clear all lines of the UI block
                for (int i = 0; i < totalLines; i++)
                {
                    clearSb.Append("\r" + new string(' ', width) + "\n");
                }
                
                // Move back up to the top of the cleared block
                for (int i = 0; i < totalLines; i++) clearSb.Append("\x1b[A");
                
                clearSb.Append("\x1b[?25h"); // Show cursor
                Console.Write(clearSb.ToString());

                Console.Write("> ");
                Console.WriteLine(result);
                
                // Add to history if unique
                if (_history.Count == 0 || _history[^1] != result)
                {
                    _history.Add(result);
                }

                return result;
            }
            
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C)
            {
                // Clean exit is handled by throwing an exception, which aligns with 
                // the /exit command and triggers the graceful shutdown in Program.cs.
                throw new OperationCanceledException("User requested exit.");
            }

            // 1.5. Handle Command Autocomplete Menu Navigation
            if (IsCommandAutocompleteActive())
            {
                var matches = GetActiveMatches();
                if (matches.Count > 0 && (key.Key == ConsoleKey.Tab || key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow))
                {
                    if (_menuSelectionIndex == -1)
                    {
                        _savedQuery = _input.ToString();
                    }

                    bool goBackward = key.Key == ConsoleKey.UpArrow || 
                                     (key.Key == ConsoleKey.Tab && key.Modifiers.HasFlag(ConsoleModifiers.Shift));

                    if (goBackward)
                    {
                        _menuSelectionIndex--;
                        if (_menuSelectionIndex < -1) _menuSelectionIndex = matches.Count - 1;
                    }
                    else
                    {
                        _menuSelectionIndex++;
                        if (_menuSelectionIndex >= matches.Count) _menuSelectionIndex = -1;
                    }

                    // Update input bar text inline
                    _input.Clear();
                    _input.Append(_menuSelectionIndex == -1 ? _savedQuery : $"/{matches[_menuSelectionIndex].Name}");
                    _cursorIndex = _input.Length;

                    Redraw();
                    continue;
                }
            }

            // 2. Handle Navigation
            if (HandleNavigation(key))
            {
                Redraw();
                continue;
            }

            // 3. Handle Editing
            if (HandleEditing(key))
            {
                Redraw();
                continue;
            }

            // 4. Handle History
            if (HandleHistory(key))
            {
                Redraw();
                continue;
            }

            // 5. Handle Character Input
            if (!char.IsControl(key.KeyChar))
            {
                // If we were in history mode, typing a character "commits" the history entry 
                // as the new partial input.
                if (_historyIndex != -1)
                {
                    _historyIndex = -1;
                    _savedPartial = string.Empty;
                }

                CommitAutocompleteSelection();

                _input.Insert(_cursorIndex, key.KeyChar);
                _cursorIndex++;
                Redraw();
            }
        }

        throw new OperationCanceledException();
    }

    private bool HandleNavigation(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.LeftArrow || key.Key == ConsoleKey.RightArrow || 
            key.Key == ConsoleKey.Home || key.Key == ConsoleKey.End)
        {
            CommitAutocompleteSelection();
        }

        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    MoveCursorWordLeft();
                else if (_cursorIndex > 0) 
                    _cursorIndex--;
                return true;

            case ConsoleKey.RightArrow:
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    MoveCursorWordRight();
                else if (_cursorIndex < _input.Length) 
                    _cursorIndex++;
                return true;

            case ConsoleKey.Home:
                _cursorIndex = 0;
                return true;

            case ConsoleKey.End:
                _cursorIndex = _input.Length;
                return true;

            default:
                return false;
        }
    }

    private bool HandleEditing(ConsoleKeyInfo key)
    {
        // Ctrl+U: Clear Line
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.U)
        {
            CommitAutocompleteSelection();
            _input.Clear();
            _cursorIndex = 0;
            return true;
        }

        // Ctrl+K: Delete to End
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.K)
        {
            CommitAutocompleteSelection();
            if (_cursorIndex < _input.Length)
            {
                _input.Remove(_cursorIndex, _input.Length - _cursorIndex);
            }
            return true;
        }

        // Ctrl+W: Delete Word Left (Standard shell shortcut)
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.W)
        {
            CommitAutocompleteSelection();
            DeleteWordLeft();
            return true;
        }

        switch (key.Key)
        {
            case ConsoleKey.Backspace:
                CommitAutocompleteSelection();
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    DeleteWordLeft();
                }
                else if (_cursorIndex > 0)
                {
                    _input.Remove(_cursorIndex - 1, 1);
                    _cursorIndex--;
                }
                return true;

            case ConsoleKey.Delete:
                CommitAutocompleteSelection();
                if (_cursorIndex < _input.Length)
                {
                    _input.Remove(_cursorIndex, 1);
                }
                return true;

            default:
                return false;
        }
    }

    private bool HandleHistory(ConsoleKeyInfo key)
    {
        if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.DownArrow)
        {
            CommitAutocompleteSelection();
        }

        if (key.Key == ConsoleKey.UpArrow)
        {
            if (_historyIndex == -1)
            {
                if (_history.Count == 0) return true;

                // UX: Save the current partial input so that if the user navigates 
                // through history and then back down, their original draft is preserved.
                _savedPartial = _input.ToString();
                _historyIndex = _history.Count - 1;
            }
            else if (_historyIndex > 0)
            {
                _historyIndex--;
            }
            else
            {
                return true; // Already at oldest
            }

            _input.Clear();
            _input.Append(_history[_historyIndex]);
            _cursorIndex = _input.Length;
            return true;
        }

        if (key.Key == ConsoleKey.DownArrow)
        {
            if (_historyIndex == -1) return true;

            if (_historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                _input.Clear();
                _input.Append(_history[_historyIndex]);
            }
            else
            {
                _historyIndex = -1;
                _input.Clear();
                _input.Append(_savedPartial);
            }
            _cursorIndex = _input.Length;
            return true;
        }

        return false;
    }

    private void MoveCursorWordLeft()
    {
        if (_cursorIndex == 0) return;
        
        // Word Boundary: We currently define a word as any sequence of non-whitespace chars.
        // This ensures paths like 'src/Logis/Program.cs' are jumped in one go.

        // Skip trailing whitespace
        while (_cursorIndex > 0 && char.IsWhiteSpace(_input[_cursorIndex - 1]))
            _cursorIndex--;
        
        // Skip word
        while (_cursorIndex > 0 && !char.IsWhiteSpace(_input[_cursorIndex - 1]))
            _cursorIndex--;
    }

    private void MoveCursorWordRight()
    {
        if (_cursorIndex == _input.Length) return;

        // Skip leading whitespace
        while (_cursorIndex < _input.Length && char.IsWhiteSpace(_input[_cursorIndex]))
            _cursorIndex++;

        // Skip word
        while (_cursorIndex < _input.Length && !char.IsWhiteSpace(_input[_cursorIndex]))
            _cursorIndex++;
    }

    private void DeleteWordLeft()
    {
        int start = _cursorIndex;
        MoveCursorWordLeft();
        _input.Remove(_cursorIndex, start - _cursorIndex);
    }

    /// <summary>
    /// Restores the terminal to a clean state by showing the cursor, 
    /// resetting colors, and moving past the UI block.
    /// </summary>
    public void Cleanup()
    {
        // 1. Show Cursor
        Console.Write("\x1b[?25h");

        // 2. Reset Colors
        Console.ResetColor();

        // 3. Move down past the entire UI block to prevent the shell prompt 
        // from overwriting our last status line or vice versa.
        int linesDown = (_lastInputLines - _lastCursorLine) + 3 + _lastMenuHeight;
        for (int i = 0; i < linesDown; i++) Console.Write("\n");
        Console.Write("\r");
    }

    /// <summary>
    /// Performs a full redraw of the input bar and status line.
    /// Anchors the UI at the bottom of the current terminal view.
    /// </summary>
    private void Redraw()
    {
        int width = Console.WindowWidth;
        if (width <= 0) width = 80;

        string prompt = "> ";
        int promptLen = prompt.Length;
        string content = _input.ToString();
        int totalChars = promptLen + content.Length;

        // --- Single-Buffer Assembly ---
        // Building the entire frame in memory eliminates flicker by sending 
        // a single packet to the terminal emulator.
        var sb = new StringBuilder();

        // 0. Hide Cursor during redraw to prevent "dancing cursor" artifacts
        sb.Append("\x1b[?25l");

        // 1. Reset & Wipe (The "Canvas Prep")
        if (_lastInputLines >= 0)
        {
            // Move back to the separator line from current typing position
            int linesToClearMoveUp = _lastCursorLine + 1;
            for (int i = 0; i < linesToClearMoveUp; i++) sb.Append("\x1b[A");
            
            // Wipe the entire vertical span of the previous UI block (including menu)
            int totalLinesToClear = _lastMenuHeight + _lastInputLines + 4;
            for (int i = 0; i < totalLinesToClear; i++)
            {
                sb.Append("\r" + new string(' ', width) + "\n");
            }

            // Move back up to the fresh start point
            for (int i = 0; i < totalLinesToClear; i++) sb.Append("\x1b[A");
        }

        // 2. Paint Separator (DarkGray ANSI)
        sb.Append("\r\x1b[90m");
        sb.Append(new string('─', width));
        sb.Append("\x1b[0m\n\r");

        // 3. Paint Input Block (White on DarkGray Background)
        sb.Append("\x1b[100m\x1b[97m");
        sb.Append(prompt);
        sb.Append(content);
        
        // Full Width Padding for Background
        int remainingInLine = width - (totalChars % width);
        if (remainingInLine > 0 && remainingInLine < width)
        {
            sb.Append(new string(' ', remainingInLine));
        }
        sb.Append("\x1b[0m\n\r"); // Reset background and go to next line

        // 3.5. Paint Autocomplete Menu (if active)
        var matches = GetActiveMatches();
        int menuHeight = 0;

        if (matches.Count > 0)
        {
            int maxVisible = 5;
            int visibleCount = Math.Min(matches.Count, maxVisible);
            
            int startIndex = 0;
            if (matches.Count > maxVisible)
            {
                if (_menuSelectionIndex == -1)
                {
                    startIndex = 0;
                }
                else
                {
                    startIndex = Math.Clamp(_menuSelectionIndex - (maxVisible / 2), 0, matches.Count - maxVisible);
                }
            }

            int itemsAbove = startIndex;
            int itemsBelow = matches.Count - (startIndex + visibleCount);

            // Render Header
            if (itemsAbove > 0)
            {
                sb.Append($"\r\x1b[90m  Suggestions (▲ {itemsAbove} more):\x1b[0m\n\r");
            }
            else
            {
                sb.Append("\r\x1b[90m  Suggestions:\x1b[0m\n\r");
            }
            menuHeight++;

            // Render Match Lines
            for (int i = 0; i < visibleCount; i++)
            {
                int matchIndex = startIndex + i;
                var match = matches[matchIndex];
                bool isSelected = (matchIndex == _menuSelectionIndex);

                if (isSelected)
                {
                    sb.Append("\r\x1b[93m  → "); // Yellow Arrow
                    sb.Append($"/{match.Name}");
                    sb.Append("\x1b[0m\x1b[90m - "); // DarkGray separator
                    
                    string desc = match.Description;
                    int prefixLen = 6 + match.Name.Length;
                    int descLimit = width - prefixLen - 4;
                    if (descLimit > 0 && desc.Length > descLimit)
                    {
                        desc = desc[..descLimit] + "...";
                    }
                    sb.Append(desc);
                    sb.Append("\x1b[0m\n\r");
                }
                else
                {
                    sb.Append("\r\x1b[90m    \x1b[32m"); // Indent & Green name
                    sb.Append($"/{match.Name}");
                    sb.Append("\x1b[0m\x1b[90m - ");
                    
                    string desc = match.Description;
                    int prefixLen = 6 + match.Name.Length;
                    int descLimit = width - prefixLen - 4;
                    if (descLimit > 0 && desc.Length > descLimit)
                    {
                        desc = desc[..descLimit] + "...";
                    }
                    sb.Append(desc);
                    sb.Append("\x1b[0m\n\r");
                }
                menuHeight++;
            }

            // Render Footer
            if (matches.Count > maxVisible)
            {
                if (itemsBelow > 0)
                {
                    sb.Append($"\r\x1b[90m  (▼ {itemsBelow} more... type to filter)\x1b[0m\n\r");
                }
                else
                {
                    sb.Append("\r\x1b[90m  (type to filter)\x1b[0m\n\r");
                }
                menuHeight++;
            }
        }

        // 4. Paint Spacer & Status
        sb.Append(new string(' ', width)); // Blank spacer
        sb.Append("\n\r");
        sb.Append(_statusLine.RenderAnsi(_session, width - 1));
        
        // 5. Restore Cursor Position
        int totalInputLines = totalChars / width;
        int cursorLine = (promptLen + _cursorIndex) / width;
        int cursorCol = (promptLen + _cursorIndex) % width;

        // Leapfrog move up
        int linesToMoveUp = (totalInputLines - cursorLine) + 2 + menuHeight;
        for (int i = 0; i < linesToMoveUp; i++) sb.Append("\x1b[A");
        
        // Final Horizontal Positioning & Show Cursor
        sb.Append($"\r\x1b[{cursorCol + 1}G"); 
        sb.Append("\x1b[?25h");

        // --- ATOMIC PAINT ---
        Console.Write(sb.ToString());

        // Update state for the next frame
        _lastInputLines = totalInputLines;
        _lastCursorLine = cursorLine;
        _lastMenuHeight = menuHeight;
    }

    private bool IsCommandAutocompleteActive()
    {
        // Do not show autocomplete dropdown while browsing history
        if (_historyIndex != -1) return false;

        // Active if input starts with '/' and there are no spaces up to the cursor
        string textBeforeCursor = _input.ToString(0, _cursorIndex);
        return textBeforeCursor.StartsWith('/') && !textBeforeCursor.Contains(' ');
    }

    private List<ISlashCommand> GetActiveMatches()
    {
        if (!IsCommandAutocompleteActive())
        {
            return new List<ISlashCommand>();
        }

        string textBeforeCursor = _input.ToString(0, _cursorIndex);
        string query = textBeforeCursor[1..];

        // Maintain stable query while cycling through suggestions
        if (_menuSelectionIndex != -1 && !string.IsNullOrEmpty(_savedQuery))
        {
            query = _savedQuery[1..];
        }

        return _registry.GetCommands()
            .Where(c => c.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .ToList();
    }

    private void CommitAutocompleteSelection()
    {
        if (_menuSelectionIndex != -1)
        {
            _menuSelectionIndex = -1;
            _savedQuery = string.Empty;
        }
    }
}
