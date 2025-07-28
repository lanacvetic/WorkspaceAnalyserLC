namespace WorkspaceAnalyser.Models;

/// <summary>
/// Represents a single event read from a Log.xml file.
/// </summary>
public record LogEvent(string Date, string Title, string SourceFilePath);