using CommunityToolkit.Mvvm.ComponentModel;

namespace WorkspaceAnalyser.Models;

/// <summary>
/// Represents a single pattern for identifying junk files,
/// along with a flag to enable or disable it.
/// </summary>
public partial class JunkFilePattern : ObservableObject
{
    /// <summary>
    /// The file search pattern (e.g., "*.log", "*.tmp").
    /// </summary>
    [ObservableProperty]
    private string _pattern;

    /// <summary>
    /// A description of what the pattern targets.
    /// </summary>
    [ObservableProperty]
    private string _description;

    /// <summary>
    /// A flag indicating whether this pattern should be used in the scan.
    /// This will be bound to a CheckBox in the UI.
    /// </summary>
    [ObservableProperty]
    private bool _isEnabled;

    public JunkFilePattern(string pattern, string description, bool isEnabled = true)
    {
        _pattern = pattern;
        _description = description;
        _isEnabled = isEnabled;
    }
}