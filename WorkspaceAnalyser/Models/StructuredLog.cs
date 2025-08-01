using System.Collections.Generic;

namespace WorkspaceAnalyser.Models;

/// <summary>
/// Represents a single, specific action or event within a log section.
/// </summary>
/// <param name="Action">The type of action (e.g., "Kopieren", "Neu").</param>
/// <param name="Date">The timestamp of the event.</param>
/// <param name="Details">Additional details, like a filename or info text.</param>
public record LogEntry(string Action, string Date, string Details);

/// <summary>
/// Represents a major section from the log.xml file (e.g., "fup_xl.exe").
/// It contains a name and a list of all entries within that section.
/// </summary>
/// <param name="SectionName">The name of the section (e.g., "fup_xl.exe").</param>
/// <param name="Entries">A list of all log entries found within this section.</param>
public record LogSection(string SectionName, List<LogEntry> Entries);