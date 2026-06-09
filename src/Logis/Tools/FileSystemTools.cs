using System.ComponentModel;
using Spectre.Console;

namespace Logis.Tools;

/// <summary>
/// Provides read-only file system operations for the AI model.
/// </summary>
public class FileSystemTools
{
    private readonly string _workspaceRoot;
    private readonly string? _targetFilePath;
    private readonly StatusContext? _statusContext;
    private const long MaxFileSizeBytes = 50 * 1024; // 50KB

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemTools"/> class.
    /// </summary>
    /// <param name="targetFilePath">The optional path to the file currently being edited.</param>
    /// <param name="statusContext">Optional Spectre StatusContext for UI updates.</param>
    public FileSystemTools(string? targetFilePath = null, StatusContext? statusContext = null)
    {
        _workspaceRoot = Directory.GetCurrentDirectory();
        _targetFilePath = targetFilePath;
        _statusContext = statusContext;
    }

    /// <summary>
    /// Lists the files and directories in the specified path.
    /// </summary>
    /// <param name="path">The relative path to the directory to list.</param>
    /// <returns>A newline-separated list of relative paths, or an error message.</returns>
    [Description("Lists the files and directories in the specified path.")]
    public string ListDirectory(
        [Description("The relative path to the directory to list.")] string path = ".")
    {
        _statusContext?.Status($"[grey]LOGIS: Listing {Markup.Escape(path)}...[/]");
        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, path));

            // Path Sandboxing: Prevent escaping the workspace root
            if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "Error: Access denied. You can only list directories within the workspace.";
            }

            if (!Directory.Exists(fullPath))
            {
                return $"Error: Directory '{path}' not found.";
            }

            var entries = Directory.EnumerateFileSystemEntries(fullPath)
                .Select(e => Path.GetRelativePath(_workspaceRoot, e));

            return string.Join(Environment.NewLine, entries);
        }
        catch (Exception ex)
        {
            // Return error to model instead of throwing to allow self-correction
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Reads the content of a file.
    /// </summary>
    /// <param name="path">The relative path to the file to read.</param>
    /// <returns>The file content, or an error message if the file is too large or not found.</returns>
    [Description("Reads the content of a file.")]
    public string ReadFile(
        [Description("The relative path to the file to read.")] string path)
    {
        _statusContext?.Status($"[grey]LOGIS: Reading {Markup.Escape(path)}...[/]");
        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, path));

            // Guard: Prevent the model from reading the file it already has in its prompt.
            // This prevents "Tool Looping" where the model keeps reading the target file.
            if (!string.IsNullOrEmpty(_targetFilePath))
            {
                string fullTarget = Path.GetFullPath(_targetFilePath);
                if (fullPath.Equals(fullTarget, StringComparison.OrdinalIgnoreCase))
                {
                    return "Error: You already have the content of this file in your message history. DO NOT use ReadFile on the target file again. Propose changes now.";
                }
            }

            // Path Sandboxing: Prevent escaping the workspace root
            if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "Error: Access denied. You can only read files within the workspace.";
            }

            if (!File.Exists(fullPath))
            {
                return $"Error: File '{path}' not found.";
            }

            var fileInfo = new FileInfo(fullPath);
            
            // Size Limit: Prevent context overflow or large memory usage
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                return $"Error: File is too large ({fileInfo.Length / 1024}KB). Maximum allowed is 50KB.";
            }

            return File.ReadAllText(fullPath);
        }
        catch (Exception ex)
        {
            // Return error to model instead of throwing to allow self-correction
            return $"Error: {ex.Message}";
        }
    }
}
