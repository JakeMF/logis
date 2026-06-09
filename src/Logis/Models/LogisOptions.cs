namespace Logis.Models;

/// <summary>
/// Represents the final runtime options for the application, 
/// merging configuration file settings with command-line overrides.
/// </summary>
public record LogisOptions(bool Debug, bool Verbose, int MaxToolIterations);
