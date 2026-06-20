using System.ComponentModel;

namespace Logis.Tools;

/// <summary>
/// Provides file system operations for the AI model, including reading and creating files.
/// </summary>
public class FileSystemTools
{
    private readonly string _workspaceRoot;
    private readonly HashSet<string> _targetFiles;
    private const long MaxFileSizeBytes = 50 * 1024; // 50KB

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemTools"/> class.
    /// </summary>
    /// <param name="targetFiles">The optional collection of files currently being edited/focused.</param>
    public FileSystemTools(IEnumerable<string>? targetFiles = null)
    {
        _workspaceRoot = Directory.GetCurrentDirectory();
        _targetFiles = targetFiles != null 
            ? new HashSet<string>(targetFiles, StringComparer.OrdinalIgnoreCase) 
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The file content, or an error message if the file is too large or not found.</returns>
    [Description("Reads the content of a file.")]
    public async Task<string> ReadFileAsync(
        [Description("The relative path to the file to read.")] string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, path));
            
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

            // Normalize path for lookup in target files
            string relativePath = path.Replace("/", "\\").TrimStart('\\');
            // Only block ReadFile if the file is in focus AND contains actual content (size > 0)
            if (_targetFiles.Contains(relativePath) && fileInfo.Length > 0)
            {
                return $"Error: You already have the content of '{relativePath}' in your message history. DO NOT use ReadFile on target files. Propose your changes using SEARCH/REPLACE blocks now.";
            }
            
            // Size Limit: Prevent context overflow or large memory usage
            if (fileInfo.Length > MaxFileSizeBytes)
            {
                return $"Error: File is too large ({fileInfo.Length / 1024}KB). Maximum allowed is 50KB.";
            }

            return await File.ReadAllTextAsync(fullPath, cancellationToken);
        }
        catch (Exception ex)
        {
            // Return error to model instead of throwing to allow self-correction
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Creates a new empty file at the specified path, ensuring parent directories exist.
    /// </summary>
    /// <param name="path">The relative path to the file to create.</param>
    /// <returns>A success message, or an error message if the file already exists or access is denied.</returns>
    [Description("Creates a new empty file at the specified path.")]
    public string CreateFile(
        [Description("The relative path to the file to create.")] string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, path));

            // Path Sandboxing: Prevent escaping the workspace root
            if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                return "Error: Access denied. You can only create files within the workspace.";
            }

            if (File.Exists(fullPath))
            {
                return $"Error: File '{path}' already exists.";
            }

            // Ensure parent directories exist
            string? directoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Create the empty file
            File.WriteAllText(fullPath, string.Empty);

            return $"Success: Created empty file '{path}'. It has been added to focused files. In the next turn, you can transition to EDIT state and propose content for this file.";
        }
        catch (Exception ex)
        {
            // Return error to model instead of throwing to allow self-correction
            return $"Error: {ex.Message}";
        }
    }
}
