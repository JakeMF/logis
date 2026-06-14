namespace Logis.Models;

/// <summary>
/// Defines the available formats for model-proposed code edits.
/// </summary>
public enum EditFormat { Whole, Diff }

/// <summary>
/// Represents the final runtime options for the application, 
/// merging configuration file settings with command-line overrides.
/// </summary>
public record LogisOptions(
    bool Debug, 
    bool Verbose, 
    int MaxToolIterations, 
    EditFormat EditFormat = EditFormat.Whole,
    string? SessionId = null,
    bool SingleShot = false
);
