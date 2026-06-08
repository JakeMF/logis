namespace Logis.Services;

/// <summary>
/// Handles all filesystem interactions, providing an abstraction layer for reading 
/// and eventually writing files in the user's workspace.
/// </summary>
public class WorkspaceService
{
    /// <summary>
    /// Reads the entire content of a file as a string.
    /// </summary>
    /// <param name="path">The relative or absolute path to the file.</param>
    /// <returns>The file contents as a string.</returns>
    /// <exception cref="ArgumentException">Thrown if the path is empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the target file does not exist.</exception>
    /// <exception cref="IOException">Thrown if an unexpected error occurs during reading.</exception>
    public string ReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(path));
        }

        // Check for existence explicitly to provide a better error message
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Target file not found: {path}");
        }

        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            // Wrap the raw exception to provide more context to the caller
            throw new IOException($"Failed to read file at {path}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Overwrites the target file with the provided content.
    /// </summary>
    /// <param name="path">The path to the file to be overwritten.</param>
    /// <param name="content">The new content to write into the file.</param>
    /// <exception cref="ArgumentException">Thrown if the path is empty.</exception>
    /// <exception cref="IOException">Thrown if an unexpected error occurs during writing.</exception>
    public void WriteFile(string path, string content)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("File path cannot be empty.", nameof(path));
        }

        try
        {
            File.WriteAllText(path, content);
        }
        catch (Exception ex)
        {
            // Fail loudly if we can't save the work, following the "Maximum Visibility" principle
            throw new IOException($"Failed to write to file at {path}: {ex.Message}", ex);
        }
    }
}
