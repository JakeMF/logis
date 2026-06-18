using Logis.Models;
using System.Text;

namespace Logis.UI;

/// <summary>
/// Defines a single segment of the status line.
/// </summary>
/// <param name="Label">The display label for the segment.</param>
/// <param name="ValueResolver">A function that resolves the live value from the session.</param>
/// <param name="ColorResolver">A function that resolves the color for the segment based on the session.</param>
/// <param name="Priority">Rendering priority (lower = higher priority, dropped last).</param>
public record StatusSegment(
    string Label,
    Func<Session, string> ValueResolver,
    Func<Session, ConsoleColor> ColorResolver,
    int Priority = 0
);

/// <summary>
/// A data-driven renderer for the session status line.
/// </summary>
public class StatusLine
{
    private readonly List<StatusSegment> _segments = new();

    /// <summary>
    /// Adds a segment to the status line.
    /// </summary>
    public void AddSegment(StatusSegment segment)
    {
        _segments.Add(segment);
    }

    /// <summary>
    /// Renders the status line to a string, truncating segments based on terminal width.
    /// </summary>
    /// <param name="session">The active session.</param>
    /// <param name="maxWidth">The current width of the terminal window.</param>
    public string Render(Session session, int maxWidth)
    {
        // 1. Resolve all segments
        var resolved = _segments
            .OrderBy(s => s.Priority)
            .Select(s => new { s.Label, Value = s.ValueResolver(session), s.Priority })
            .ToList();

        // 2. Build segments from highest priority until we hit the width limit
        var sb = new StringBuilder();
        int currentWidth = 0;

        foreach (var segment in resolved)
        {
            string part = $" {segment.Label}: {segment.Value} ";
            if (sb.Length > 0) part = " |" + part;

            if (currentWidth + part.Length > maxWidth) break;

            sb.Append(part);
            currentWidth += part.Length;
        }

        // Fill the rest of the line with spaces to clear any artifacts
        if (currentWidth < maxWidth)
        {
            sb.Append(new string(' ', maxWidth - currentWidth));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes the status line directly to the console using color-coded segments.
    /// Uses raw Console APIs instead of AnsiConsole to ensure zero-latency redraws
    /// and precise cursor management during high-frequency input polling.
    /// This also avoids potential markup-parsing crashes when session data 
    /// contains reserved characters (like square brackets).
    /// </summary>
    /// <param name="session">The active session.</param>
    /// <param name="maxWidth">The current width of the terminal window.</param>
    public void Write(Session session, int maxWidth)
    {
        var resolved = _segments
            .OrderBy(s => s.Priority)
            .ToList();

        int currentWidth = 0;
        bool first = true;

        foreach (var segment in resolved)
        {
            string value = segment.ValueResolver(session);
            ConsoleColor color = segment.ColorResolver(session);
            string labelPart = $" {(first ? "" : "| ")}{segment.Label}: ";
            string valuePart = $"{value} ";

            if (currentWidth + labelPart.Length + valuePart.Length > maxWidth) break;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(labelPart);
            
            Console.ForegroundColor = color;
            Console.Write(valuePart);

            currentWidth += labelPart.Length + valuePart.Length;
            first = false;
        }

        // Clear trailing space on the line
        if (currentWidth < maxWidth)
        {
            Console.Write(new string(' ', maxWidth - currentWidth));
        }
    }
}
