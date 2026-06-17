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
    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        // Reset state for new input
        _input.Clear();
        _cursorIndex = 0;
        _historyIndex = -1;
        _savedPartial = string.Empty;

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
                string result = _input.ToString();
                if (string.IsNullOrWhiteSpace(result)) continue;

                // Move cursor to the end of the line and down past the status line before returning
                Console.SetCursorPosition(0, Console.CursorTop + 1);
                Console.WriteLine();
                
                // Add to history if unique
                if (_history.Count == 0 || _history[^1] != result)
                {
                    _history.Add(result);
                }

                return result;
            }
            
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C)
            {
                // Clean exit is handled by the CancellationTokenSource in Program.cs,
                // but we return null here to signal the loop to terminate.
                return null;
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

                _input.Insert(_cursorIndex, key.KeyChar);
                _cursorIndex++;
                Redraw();
            }
        }

        return null;
    }

    private bool HandleNavigation(ConsoleKeyInfo key)
    {
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
            _input.Clear();
            _cursorIndex = 0;
            return true;
        }

        // Ctrl+K: Delete to End
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.K)
        {
            if (_cursorIndex < _input.Length)
            {
                _input.Remove(_cursorIndex, _input.Length - _cursorIndex);
            }
            return true;
        }

        switch (key.Key)
        {
            case ConsoleKey.Backspace:
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
    /// Performs a full redraw of the input bar and status line.
    /// Anchors the UI at the bottom of the current terminal view.
    /// </summary>
    private void Redraw()
    {
        // Redraw uses relative moves (\r and \n) instead of absolute coordinates.
        // This ensures the input bar "floats" correctly at the bottom of the terminal 
        // even if new LLM output pushes the view up.

        int maxWidth = Console.WindowWidth;
        int originalTop = Console.CursorTop;

        // 1. Draw Input Line
        Console.Write("\r"); // Move to start of line
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("> ");
        Console.ResetColor();
        
        Console.Write(_input.ToString());
        
        // Clear trailing characters from previous longer inputs
        int currentLineLen = 2 + _input.Length;
        if (currentLineLen < maxWidth)
        {
            Console.Write(new string(' ', maxWidth - currentLineLen));
        }

        // 2. Draw Status Line (always one line below input)
        // Ensure we don't trigger a scroll-up by writing to the very last char of the window
        Console.Write("\n\r");
        _statusLine.Write(_session, maxWidth - 1);

        // 3. Restore Cursor Position
        // Move back up to the input line
        Console.SetCursorPosition(2 + _cursorIndex, originalTop);
    }
}
