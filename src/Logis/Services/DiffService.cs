using Spectre.Console;
using System.Text;
using System.Text.RegularExpressions;

namespace Logis.Services;

/// <summary>
/// Orchestrates the parsing, applying, and rendering of code changes.
/// Supports both full-file diffing and Search/Replace block patching.
/// </summary>
public class DiffService
{
    private readonly DiffEngine _engine = new();

    /// <summary>
    /// Parses SEARCH/REPLACE blocks from a model response and applies them to the original content.
    /// Also renders the final diff to the console.
    /// </summary>
    /// <param name="original">The original file content.</param>
    /// <param name="response">The model response containing blocks.</param>
    /// <param name="filePath">The path to the file being edited.</param>
    /// <returns>The updated file content.</returns>
    /// <exception cref="Exception">Thrown if a SEARCH block cannot be found or is ambiguous.</exception>
    public string ApplyEdit(string original, string response, string filePath)
    {
        var currentLines = ReadLines(original);
        var blocks = ParseBlocks(response, currentLines.FirstOrDefault() ?? "");

        if (blocks.Count == 0)
        {
            throw new Exception("No valid SEARCH/REPLACE blocks found in the model response. Ensure the model followed the requested format.");
        }

        foreach (var (search, replace) in blocks)
        {
            // Level 3 Fallback: If the search block is the special marker for "Whole File"
            if (search == "__WHOLE_FILE__")
            {
                var final = string.Join("\n", ReadLines(replace));
                RenderDiff(original, final, Path.GetFileName(filePath));
                return final;
            }

            var searchLines = ReadLines(search);
            var replaceLines = ReadLines(replace);

            // Find the search block as a line sequence in currentLines
            int matchIndex = FindLineSequence(currentLines, searchLines);
            if (matchIndex == -1)
                throw new Exception($"SEARCH block not found in {Path.GetFileName(filePath)} (Intent-based search failed).\nSEARCH pattern attempted:\n{search}");

            // Check for a second match — ambiguous, hard fail
            if (FindLineSequence(currentLines, searchLines, matchIndex + 1) != -1)
                throw new Exception($"Ambiguous match: SEARCH block appears more than once in {Path.GetFileName(filePath)}. Aborting to prevent accidental corruption.");

            // Replace the matched lines with the replacement lines (empty replace = deletion)
            var updated = new List<string>();
            updated.AddRange(currentLines.Take(matchIndex));
            updated.AddRange(replaceLines);
            updated.AddRange(currentLines.Skip(matchIndex + searchLines.Length));
            currentLines = updated.ToArray();
        }

        var newContent = string.Join("\n", currentLines);
        RenderDiff(original, newContent, Path.GetFileName(filePath));
        return newContent;
    }

    /// <summary>
    /// Represents a proposed change block.
    /// </summary>
    private record struct DiffBlock(string Search, string Replace);

    /// <summary>
    /// Extracts SEARCH/REPLACE intent from a model response using a marker-agnostic architectural approach.
    /// It identifies all tagged blocks and determines if they represent patches or whole-file replacements.
    /// </summary>
    /// <param name="response">The raw model response.</param>
    /// <param name="originalFirstLine">The first line of the original file, used as an anchor for restart detection.</param>
    private List<DiffBlock> ParseBlocks(string response, string originalFirstLine)
    {
        var patches = new List<DiffBlock>();
        DiffBlock? wholeFile = null;
        
        // 1. Agnostic Extraction: Find all content between [[TAG]] markers
        var tagRegex = new Regex(@"\[\[(?<tag>.*?)\]\]\s*(?<content>.*?)(?=\[\[|$)", RegexOptions.Singleline);
        var matches = tagRegex.Matches(response).Cast<Match>().ToList();

        // 2. Intent Analysis
        for (int i = 0; i < matches.Count; i++)
        {
            var tag = matches[i].Groups["tag"].Value.ToUpper().Trim();
            var content = CleanBlock(matches[i].Groups["content"].Value);

            if (tag == "END" || string.IsNullOrWhiteSpace(content)) continue;

            // Pattern: SEARCH followed by REPLACE (A Patch)
            if (tag == "SEARCH" && (i + 1) < matches.Count && matches[i + 1].Groups["tag"].Value.ToUpper().Trim() == "REPLACE")
            {
                var replaceContent = CleanBlock(matches[i + 1].Groups["content"].Value);
                patches.Add(new DiffBlock(content, replaceContent));
                i++; 
            }
            // Pattern: Lone block (A Whole-File intent)
            else if (tag == "REPLACE" || tag == "SEARCH")
            {
                // Anchor-Based Restart Detection: 
                // If this block starts with the same line as the original file, it's a restart.
                var currentFirstLine = content.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                
                bool isRestart = !string.IsNullOrWhiteSpace(originalFirstLine) && 
                                 Normalize(currentFirstLine) == Normalize(originalFirstLine);

                if (wholeFile == null || isRestart)
                {
                    wholeFile = new DiffBlock("__WHOLE_FILE__", content);
                }
                else
                {
                    // If not a restart, it's a chunk of the previous whole-file block.
                    wholeFile = wholeFile.Value with { Replace = wholeFile.Value.Replace + "\n" + content };
                }
            }
        }

        // 3. Conflict Resolution: A Whole-File replacement always wins over partial patches.
        if (wholeFile != null)
        {
            return new List<DiffBlock> { wholeFile.Value };
        }

        return patches;
    }

    /// <summary>
    /// Cleans a captured block by trimming whitespace and stripping markdown fences/language tags.
    /// </summary>
    private string CleanBlock(string content)
    {
        content = content.Trim();

        // Strip leading markdown fences (e.g., ```python)
        content = Regex.Replace(content, @"^```[a-zA-Z]*\s*", "");
        // Strip trailing markdown fences (e.g., ```)
        content = Regex.Replace(content, @"\s*```$", "");

        return content.Trim();
    }

    /// <summary>
    /// Renders a unified-style diff to the console with hunk grouping.
    /// </summary>
    /// <param name="oldContent">The original text.</param>
    /// <param name="newContent">The modified text.</param>
    /// <param name="fileName">The name of the file for the header.</param>
    public void RenderDiff(string oldContent, string newContent, string fileName)
    {
        var oldLines = ReadLines(oldContent);
        var newLines = ReadLines(newContent);
        var diff = _engine.ComputeDiff(oldLines, newLines);

        var changes = Enumerable.Range(0, diff.Count)
            .Where(i => diff[i].Op != DiffOp.Unchanged)
            .ToList();

        if (changes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No changes detected.[/]");
            return;
        }

        // Group changes into hunks with 3 lines of context
        var regions = new List<(int Start, int End)>();
        foreach (var idx in changes)
        {
            int s = Math.Max(0, idx - 3);
            int e = Math.Min(diff.Count - 1, idx + 3);
            if (regions.Count > 0 && s <= regions[^1].End)
                regions[^1] = (regions[^1].Start, Math.Max(regions[^1].End, e));
            else
                regions.Add((s, e));
        }

        AnsiConsole.MarkupLine($"[bold cyan]--- PROPOSED CHANGES: {Markup.Escape(fileName)} ---[/]");

        int oldLn = 1, newLn = 1, dIdx = 0;
        foreach (var reg in regions)
        {
            // Advance line counters to region start
            while (dIdx < reg.Start)
            {
                if (diff[dIdx].Op != DiffOp.Inserted) oldLn++;
                if (diff[dIdx].Op != DiffOp.Deleted) newLn++;
                dIdx++;
            }

            AnsiConsole.MarkupLine($"[cyan]@@ hunk near line {oldLn} @@[/]");

            for (int i = reg.Start; i <= reg.End; i++)
            {
                var d = diff[i];
                var color = d.Op switch
                {
                    DiffOp.Inserted => "green",
                    DiffOp.Deleted  => "red",
                    _               => "grey"
                };
                var prefix = d.Op switch
                {
                    DiffOp.Inserted => "+",
                    DiffOp.Deleted  => "-",
                    _               => " "
                };
                
                // Using Text instead of Markup for raw content to prevent bracket parsing crashes
                AnsiConsole.Write(new Text($"{prefix}{d.Text}", new Style(Color.FromConsoleColor(GetConsoleColor(color)))));
                AnsiConsole.WriteLine();

                if (d.Op != DiffOp.Inserted) oldLn++;
                if (d.Op != DiffOp.Deleted) newLn++;
            }

            dIdx = reg.End + 1;
        }
        
        AnsiConsole.MarkupLine("[bold cyan]--- END OF CHANGES ---[/]");
        AnsiConsole.WriteLine();
    }

    private ConsoleColor GetConsoleColor(string color) => color switch
    {
        "green" => ConsoleColor.Green,
        "red"   => ConsoleColor.Red,
        "grey"  => ConsoleColor.Gray,
        _       => ConsoleColor.White
    };

    /// <summary>
    /// Reads lines robustly across \r\n and \n using StringReader.
    /// </summary>
    private string[] ReadLines(string input)
    {
        var lines = new List<string>();
        using var reader = new StringReader(input);
        string? line;
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);
        return lines.ToArray();
    }

    /// <summary>
    /// Line-sequence search: finds the start index in `source` where `pattern` lines appear
    /// consecutively, using whitespace normalization for robust matching.
    /// </summary>
    private int FindLineSequence(string[] source, string[] pattern, int startAt = 0)
    {
        if (pattern.Length == 0) return startAt;

        for (int i = startAt; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (Normalize(source[i + j]) != Normalize(pattern[j]))
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Normalizes a line for comparison by collapsing all whitespace and trimming.
    /// This handles tabs vs spaces and accidental indentation shifts.
    /// </summary>
    private string Normalize(string input)
    {
        return Regex.Replace(input.Trim(), @"\s+", " ");
    }
}
