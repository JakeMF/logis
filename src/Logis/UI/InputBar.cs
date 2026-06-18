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
    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        // Reset state for new input
        _input.Clear();
        _cursorIndex = 0;
        _historyIndex = -1;
        _savedPartial = string.Empty;
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

        // Ctrl+W: Delete Word Left (Standard shell shortcut)
        if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.W)
        {
            DeleteWordLeft();
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
        int width = Console.WindowWidth;
        if (width <= 0) width = 80;

        string prompt = "> ";
        int promptLen = prompt.Length;
        string content = _input.ToString();
        int totalChars = promptLen + content.Length;

        // --- Step 4.2: Visual Block Configuration ---
        ConsoleColor blockBg = ConsoleColor.DarkGray;
        ConsoleColor separatorColor = ConsoleColor.DarkGray;

        // 0. Reset & Wipe (The "Canvas Prep")
        // If we have a previous frame, we move to its top and CLEAR the entire height.
        // This prevents "ghost" lines when the input wraps/unwraps or is deleted.
        if (_lastInputLines >= 0)
        {
            // Move back to the separator line
            for (int i = 0; i < _lastCursorLine + 1; i++) Console.Write("\x1b[A");
            
            // Wipe the entire vertical span of the previous UI block
            // Height = Separator(1) + Input(_lastInputLines+1) + Spacer(1) + Status(1) = +4
            int totalLinesToClear = _lastInputLines + 4;
            for (int i = 0; i < totalLinesToClear; i++)
            {
                Console.Write("\r" + new string(' ', width) + "\n");
            }

            // Move back up to the fresh start point
            for (int i = 0; i < totalLinesToClear; i++) Console.Write("\x1b[A");
        }

        // 1. Draw Separator (The Fence)
        Console.Write("\r");
        Console.ForegroundColor = separatorColor;
        Console.Write(new string('─', width));
        Console.ResetColor();
        Console.Write("\n\r");

        // 2. Draw Input Block
        // NOTE: We scope the background color ONLY to the input area.
        Console.BackgroundColor = blockBg;
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(prompt);
        
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(content);
        
        // --- Full Width Padding ---
        int remainingInLine = width - (totalChars % width);
        if (remainingInLine > 0 && remainingInLine < width)
        {
            Console.Write(new string(' ', remainingInLine));
        }

        // --- Reset Color & Add Blank Spacer ---
        Console.ResetColor();
        Console.Write("\n\r"); 

        // 3. Draw Status Line
        Console.Write("\n\r");
        _statusLine.Write(_session, width - 1);
        
        // 4. Restore Cursor Position
        // --- Floating Anchor Math ---
        int totalInputLines = totalChars / width;
        int cursorLine = (promptLen + _cursorIndex) / width;
        int cursorCol = (promptLen + _cursorIndex) % width;

        // --- Relative Leapfrog ---
        // Move up over the Status Line (+1) and the Spacer Line (+1) 
        // plus any wrapped input lines.
        int linesToMoveUp = (totalInputLines - cursorLine) + 2;
        
        for (int i = 0; i < linesToMoveUp; i++)
        {
            Console.Write("\x1b[A"); 
        }
        
        // --- Horizontal Jump ---
        Console.Write($"\r\x1b[{cursorCol + 1}G"); 

        // Update state for the next frame
        _lastInputLines = totalInputLines;
        _lastCursorLine = cursorLine;
    }
}
