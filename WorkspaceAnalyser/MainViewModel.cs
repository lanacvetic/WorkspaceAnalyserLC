using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkspaceAnalyser.Models;
using WorkspaceAnalyser.Services;
using WorkspaceAnalyser.Utils;

namespace WorkspaceAnalyser;

/// <summary>
/// Defines the sorting order for displaying projects.
/// </summary>
public enum SortOrder
{
    KleinsteZuerst, // Smallest first
    GroessteZuerst // Largest first
}

/// <summary>
/// The main ViewModel for the application window, handling all UI logic and data analysis orchestration.
/// It uses the CommunityToolkit.Mvvm library to implement the MVVM pattern.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    //-------------------------------------------------------------------------
    // Fields
    //-------------------------------------------------------------------------

    private NodeBase? _currentlySelectedNode;
    private readonly IAnalysisReportService _reportService;
    private readonly IControllerDetailService _controllerDetailService;
    private readonly IJunkFileScanner _junkFileScanner;

    /// <summary>
    /// A master list holding all files and folders for the currently inspected controller.
    /// This list is used as a source for the filtered display list.
    /// </summary>
    private readonly ObservableCollection<ContentItem> _masterControllerContent = new();

    //-------------------------------------------------------------------------
    // Properties
    //-------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the root path of the workspace to be analyzed.
    /// </summary>
    [ObservableProperty] private string _projectViewPath = @"C:\Users\lcvetic\Documents\Workspace";

    /// <summary>
    /// Gets or sets a collection of ProjectNode objects found in the workspace.
    /// This is the main data source for the project TreeView.
    /// </summary>
    [ObservableProperty] private ObservableCollection<ProjectNode> _prjNodes = new();

    /// <summary>
    /// Gets or sets a collection of file paths identified as junk files.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _junkFiles = new();

    /// <summary>
    /// Gets or sets the detailed information for the currently selected controller.
    /// </summary>
    [ObservableProperty] private ControllerDetails? _selectedControllerDetails;

    /// <summary>
    /// Gets or sets the current user-selected sort order.
    /// </summary>
    [ObservableProperty] private SortOrder _selectedSortOrder;

    /// <summary>
    /// A boolean flag derived from SelectedSortOrder. True for ascending, false for descending.
    /// </summary>
    [ObservableProperty] private bool _sortAscending = true;

    /// <summary>
    /// Gets or sets the total number of projects found during the analysis.
    /// </summary>
    [ObservableProperty] private int _projectNumber;

    /// <summary>
    /// Gets or sets the total number of controllers found across all projects.
    /// </summary>
    [ObservableProperty] private int _controllerNumber;

    /// <summary>
    /// Gets or sets the total size of the analyzed workspace directory.
    /// </summary>
    [ObservableProperty] private double _workspaceSize;

    /// <summary>
    /// A flag to indicate if the junk file scan is currently running.
    /// </summary>
    [ObservableProperty] private bool _isScanning;

    /// <summary>
    /// Gets or sets the list of junk file patterns for the settings tab.
    /// </summary>
    [ObservableProperty] private ObservableCollection<JunkFilePattern> _junkFilePatterns = new();

    /// <summary>
    /// Gets or sets the text used to filter the controller content list.
    /// </summary>
    [ObservableProperty] private string _controllerContentFilter = string.Empty;

    /// <summary>
    /// Gets or sets the filtered list of a controller's content to be displayed in the UI.
    /// </summary>
    [ObservableProperty] private ObservableCollection<ContentItem> _displayedControllerContent = new();

    /// <summary>
    /// Gets or sets the currently selected log section from the Log Viewer's dropdown.
    /// </summary>
    [ObservableProperty] private LogSection? _selectedLogSection;

    /// <summary>
    /// Gets or sets the list of log entries for the currently selected section.
    /// This is the data source for the Log Viewer's DataGrid.
    /// </summary>
    [ObservableProperty] private ObservableCollection<LogEntry> _selectedLogEntries = new();

    //-------------------------------------------------------------------------
    // Constructor
    //-------------------------------------------------------------------------

    /// <summary>
    /// Initializes a new instance of the MainViewModel and its services.
    /// </summary>
    public MainViewModel()
    {
        _reportService = new AnalysisReportService();
        _controllerDetailService = new ControllerDetailService();
        _junkFileScanner = new JunkFileScanner();

        // Initialize the default junk file patterns for the settings tab.
        JunkFilePatterns.Add(new JunkFilePattern("mainbdf.mot", "Motion Definition File"));
        JunkFilePatterns.Add(new JunkFilePattern("mainbdf.ppe", "Project Parameter File"));
        JunkFilePatterns.Add(new JunkFilePattern("grafikbilderinfo.txt", "Graphics Info Text"));
        JunkFilePatterns.Add(new JunkFilePattern("*.bak", "Backup Files"));
        JunkFilePatterns.Add(new JunkFilePattern("*.tmp", "Temporary Files"));
        JunkFilePatterns.Add(new JunkFilePattern("*.sav", "Save Files"));
        JunkFilePatterns.Add(new JunkFilePattern("mainbdf_fbg5.inc", "Include File"));
        JunkFilePatterns.Add(new JunkFilePattern("mainbdf_tup.inc", "Include File"));
        JunkFilePatterns.Add(new JunkFilePattern("fupliste.xml", "FUP List XML"));
        JunkFilePatterns.Add(new JunkFilePattern("fupliste.xmlpl", "FUP List File"));
        JunkFilePatterns.Add(new JunkFilePattern("fupblattliste.mnu", "FUP Menu File"));
    }

    //-------------------------------------------------------------------------
    // Commands
    //-------------------------------------------------------------------------

    /// <summary>
    /// Triggers the main analysis based on the ProjectViewPath.
    /// </summary>
    [RelayCommand]
    private void StartAnalysis()
    {
        // Reset all relevant properties for a clean run.
        PrjNodes.Clear();
        JunkFiles.Clear();
        ProjectNumber = 0;
        ControllerNumber = 0;
        SelectedControllerDetails = null;
        _currentlySelectedNode = null;

        if (string.IsNullOrWhiteSpace(ProjectViewPath) || !Directory.Exists(ProjectViewPath))
        {
            MessageBox.Show("Die Quelldatei konnte unter folgendem Pfad nicht gefunden werden!", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        WorkspaceSize = DiskAnalyzer.GetDiskSize(ProjectViewPath);

        // --- Analysis Logic ---
        if (DiskAnalyzer.IsUstCategory(ProjectViewPath)) // Case 1: Path is a single controller.
        {
            var ustSize = DiskAnalyzer.GetDiskSize(ProjectViewPath);
            var ustNode = new UstNode
            {
                Path = ProjectViewPath, FormattedSize = DiskAnalyzer.FormatSize(ustSize), Percentage = 100,
                TextColor = ColorProvider.GetUstCategoryColor(100)
            };
            ustNode.PropertyChanged += Node_PropertyChanged;
            var wrapperProjectNode = new ProjectNode
            {
                Path = ProjectViewPath, FormattedSize = DiskAnalyzer.FormatSize(ustSize), TextColor = Brushes.Black,
                Usts = [ustNode]
            };
            wrapperProjectNode.PropertyChanged += Node_PropertyChanged;
            PrjNodes.Add(wrapperProjectNode);
            ProjectNumber = 1;
            ControllerNumber = 1;
        }
        else if (DiskAnalyzer.IsProject(ProjectViewPath)) // Case 2: Path is a single project.
        {
            DisplayProjectAtPath(ProjectViewPath);
        }
        else // Case 3: Path is a workspace with multiple projects.
        {
            var allPrjPaths = DiskAnalyzer.GetAllProjectPaths(ProjectViewPath);
            if (!allPrjPaths.Any())
            {
                MessageBox.Show("Kein gültiges Projekt (prj.xml) oder Controller (ust.xml) unter diesem Pfad gefunden!",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var projectSizes = allPrjPaths.Select(prj => new { Path = prj, Size = DiskAnalyzer.GetDiskSize(prj) })
                .ToList();
            var sortedProjects = SortAscending
                ? projectSizes.OrderBy(p => p.Size)
                : projectSizes.OrderByDescending(p => p.Size);
            foreach (var prj in sortedProjects)
            {
                DisplayProjectAtPath(prj.Path);
            }
        }

        // Save results to a report file.
        try
        {
            _reportService.SaveAnalysisResults(PrjNodes, ProjectViewPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Speichern der Analyse-JSON: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Clears the current analysis results and the search path from the view.
    /// </summary>
    [RelayCommand]
    private void ClearAnalysis()
    {
        PrjNodes.Clear();
        JunkFiles.Clear();
        ProjectNumber = 0;
        ControllerNumber = 0;
        SelectedControllerDetails = null;
        _currentlySelectedNode = null;
        ProjectViewPath = string.Empty;
        _masterControllerContent.Clear();
        DisplayedControllerContent.Clear();
        ControllerContentFilter = string.Empty;
    }

    /// <summary>
    /// Opens the selected project's folder in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenProjectFolder(ProjectNode prjNode)
    {
        if (prjNode != null)
        {
            Process.Start("explorer.exe", prjNode.Path);
        }
    }

    /// <summary>
    /// Opens the selected controller's folder in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenUstFolder(UstNode ustNode)
    {
        if (ustNode != null)
        {
            _ = LoadControllerDetailsAsync(ustNode.Path);
            Process.Start("explorer.exe", ustNode.Path);
        }
    }

    /// <summary>
    /// Copies the path of a given node to the clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyPath(NodeBase? node)
    {
        if (node != null && !string.IsNullOrEmpty(node.Path))
        {
            try
            {
                Clipboard.SetText(node.Path);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Pfad konnte nicht in die Zwischenablage kopiert werden: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Scans the folder of a given controller and populates the content view panel.
    /// </summary>
    [RelayCommand]
    private void ShowControllerContent(UstNode? ustNode)
    {
        _masterControllerContent.Clear();
        ControllerContentFilter = string.Empty;

        if (ustNode == null || !Directory.Exists(ustNode.Path))
        {
            FilterControllerContent(); // Clears the displayed list
            return;
        }

        try
        {
            // Get all directories and files recursively
            var allEntries = Directory.EnumerateFileSystemEntries(ustNode.Path, "*", SearchOption.AllDirectories);
            foreach (var fullPath in allEntries)
            {
                string relativePath = Path.GetRelativePath(ustNode.Path, fullPath);
                string icon = Directory.Exists(fullPath) ? "📁" : "📄";
                _masterControllerContent.Add(new ContentItem($"{icon} {relativePath}", fullPath));
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not read directory contents: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        FilterControllerContent();
    }

    /// <summary>
    /// Opens a selected file or folder from the content viewer panel.
    /// </summary>
    [RelayCommand]
    private void OpenContentFile(ContentItem? selectedItem)
    {
        if (selectedItem == null) return;

        try
        {
            // The ContentItem model stores the absolute path, so we can open it directly.
            if (File.Exists(selectedItem.FullPath) || Directory.Exists(selectedItem.FullPath))
            {
                Process.Start(new ProcessStartInfo(selectedItem.FullPath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show($"Could not find the item at:\n{selectedItem.FullPath}", "Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open the item: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Scans the workspace directory for junk files asynchronously.
    /// </summary>
    [RelayCommand]
    private async Task ScanForJunkFilesAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectViewPath) || !Directory.Exists(ProjectViewPath))
        {
            MessageBox.Show("Der eingegebene Pfad existiert nicht oder ist leer!", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        JunkFiles.Clear();
        IsScanning = true;

        try
        {
            var foundJunkFiles = await _junkFileScanner.ScanForJunkFilesAsync(ProjectViewPath, JunkFilePatterns);
            foreach (var file in foundJunkFiles)
            {
                JunkFiles.Add(file);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Suchen nach Mülldateien:\n{ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Permanently deletes all files found by the junk file scan after user confirmation.
    /// </summary>
    [RelayCommand]
    private async Task DeleteJunkFilesAsync()
    {
        if (!JunkFiles.Any())
        {
            MessageBox.Show("Keine Mülldateien zum Löschen gefunden. Bitte zuerst scannen.", "Nichts zu löschen",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmResult = MessageBox.Show(
            $"Sind Sie sicher, dass Sie {JunkFiles.Count} Datei(en) DAUERHAFT löschen möchten?\n\nDiese Aktion kann nicht rückgängig gemacht werden.",
            "Löschen bestätige", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (confirmResult != MessageBoxResult.Yes) return;

        var successfullyDeletedFiles = new List<string>();
        var failedToDeleteFiles = new List<string>();

        await Task.Run(() =>
        {
            var filesToDelete = JunkFiles.ToList();
            foreach (var filePath in filesToDelete)
            {
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                    successfullyDeletedFiles.Add(filePath);
                }
                catch (Exception ex)
                {
                    failedToDeleteFiles.Add($"{filePath} (Error: {ex.Message})");
                }
            }
        });

        Application.Current.Dispatcher.Invoke(() =>
        {
            foreach (var deletedFile in successfullyDeletedFiles)
            {
                JunkFiles.Remove(deletedFile);
            }

            var summary = new StringBuilder("Deletion complete.\n\n");
            if (failedToDeleteFiles.Any())
            {
                summary.AppendLine($"{successfullyDeletedFiles.Count} Datei(en) erfolgreich gelösch.");
                summary.AppendLine($"{failedToDeleteFiles.Count} Datei(en) konnten nicht gelöscht werden:");
                summary.Append(string.Join("\n", failedToDeleteFiles));
                MessageBox.Show(summary.ToString(), "Deletion Results - Mit Errors", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                summary.Append("Alle ausgewählten Mülldateien wurden erfolgreich gelöscht!");
                MessageBox.Show(summary.ToString(), "Deletion Results", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        });
    }

    /// <summary>
    /// Opens the source Log.xml file for a given event.
    /// </summary>
    [RelayCommand]
    private void OpenLogFile(LogEvent? logEvent)
    {
        if (logEvent == null || string.IsNullOrWhiteSpace(logEvent.SourceFilePath)) return;

        if (!File.Exists(logEvent.SourceFilePath))
        {
            MessageBox.Show($"The source file could not be found at: {logEvent.SourceFilePath}", "File Not Found",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(logEvent.SourceFilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading controller details: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    //-------------------------------------------------------------------------
    // Partial On...Changed Methods (Auto-generated by CommunityToolkit.Mvvm)
    //-------------------------------------------------------------------------

    /// <summary>
    /// Fires when the user changes the sort order, triggering a new analysis.
    /// </summary>
    partial void OnSelectedSortOrderChanged(SortOrder value)
    {
        SortAscending = value == SortOrder.KleinsteZuerst;
        if (StartAnalysisCommand.CanExecute(null))
        {
            StartAnalysisCommand.Execute(null);
        }
    }

    /// <summary>
    /// Fires when the user types in the content filter TextBox, updating the displayed list.
    /// </summary>
    partial void OnControllerContentFilterChanged(string value)
    {
        FilterControllerContent();
    }

    /// <summary>
    /// Fires when a new controller is selected, updating the Log Viewer.
    /// </summary>
    partial void OnSelectedControllerDetailsChanged(ControllerDetails? value)
    {
        SelectedLogSection = null;
        SelectedLogEntries.Clear();
        if (value?.StructuredLog.Any() == true)
        {
            SelectedLogSection = value.StructuredLog.First();
        }
    }

    /// <summary>
    /// Fires when the user selects a new section in the Log Viewer's dropdown.
    /// </summary>
    partial void OnSelectedLogSectionChanged(LogSection? value)
    {
        SelectedLogEntries.Clear();
        if (value != null)
        {
            foreach (var entry in value.Entries)
            {
                SelectedLogEntries.Add(entry);
            }
        }
    }

    //-------------------------------------------------------------------------
    // Private Helper Methods
    //-------------------------------------------------------------------------

    /// <summary>
    /// Filters the master content list based on the ControllerContentFilter text.
    /// </summary>
    private void FilterControllerContent()
    {
        DisplayedControllerContent.Clear();

        var itemsToFilter = string.IsNullOrWhiteSpace(ControllerContentFilter)
            ? _masterControllerContent
            : _masterControllerContent.Where(item =>
                item.DisplayName.Contains(ControllerContentFilter, StringComparison.OrdinalIgnoreCase));

        foreach (var item in itemsToFilter)
        {
            DisplayedControllerContent.Add(item);
        }
    }

    /// <summary>
    /// Analyzes a single project directory and adds its node to the TreeView.
    /// </summary>
    private void DisplayProjectAtPath(string path)
    {
        var projectSize = DiskAnalyzer.GetDiskSize(path);
        ProjectNumber++;

        var allUsts = DiskAnalyzer.GetAllUstPathsUnderSubtree(path);
        var filteredUsts = allUsts.Where(p => p.StartsWith(path, StringComparison.OrdinalIgnoreCase));
        var sortedUsts = SortAscending
            ? filteredUsts.OrderBy(DiskAnalyzer.GetDiskSize)
            : filteredUsts.OrderByDescending(DiskAnalyzer.GetDiskSize);

        var projectNode = new ProjectNode
        {
            Path = path,
            FormattedSize = DiskAnalyzer.FormatSize(projectSize),
            TextColor = ColorProvider.GetProjectCategoryColor(DiskAnalyzer.GetPercentage(projectSize, WorkspaceSize)),
            Usts = sortedUsts.Select(ustPath =>
            {
                var ustSize = DiskAnalyzer.GetDiskSize(ustPath);
                var ustNode = new UstNode
                {
                    Path = ustPath,
                    FormattedSize = DiskAnalyzer.FormatSize(ustSize),
                    Percentage = DiskAnalyzer.GetPercentage(ustSize, projectSize),
                    TextColor = ColorProvider.GetUstCategoryColor(DiskAnalyzer.GetPercentage(ustSize, projectSize))
                };
                ustNode.PropertyChanged += Node_PropertyChanged;
                return ustNode;
            }).ToList()
        };

        projectNode.PropertyChanged += Node_PropertyChanged;
        ControllerNumber += projectNode.Usts.Count;
        PrjNodes.Add(projectNode);
    }

    /// <summary>
    /// Handles the selection logic for items in the TreeView.
    /// </summary>
    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NodeBase.IsSelected)) return;

        if (sender is NodeBase { IsSelected: true } newlySelectedNode)
        {
            // Enforce single selection
            if (_currentlySelectedNode != null && _currentlySelectedNode != newlySelectedNode)
            {
                _currentlySelectedNode.IsSelected = false;
            }

            _currentlySelectedNode = newlySelectedNode;

            // --- Main selection logic ---
            if (newlySelectedNode is ProjectNode projectNode) // If a Project is clicked...
            {
                // ...drill down by re-running the analysis on its path.
                ProjectViewPath = projectNode.Path;
                if (StartAnalysisCommand.CanExecute(null))
                {
                    StartAnalysisCommand.Execute(null);
                }
            }
            else if (newlySelectedNode is UstNode ustNode) // If a Controller is clicked...
            {
                // ...update the path, load its details, and scan for junk files.
                ProjectViewPath = ustNode.Path;
                _ = LoadControllerDetailsAsync(ustNode.Path);
                _ = ScanForJunkFilesAsync();
            }
        }
    }

    /// <summary>
    /// Asynchronously loads detailed information for a given controller.
    /// </summary>
    private async Task LoadControllerDetailsAsync(string ustPath)
    {
        try
        {
            SelectedControllerDetails = await _controllerDetailService.GetControllerDetailsAsync(ustPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Laden der Controller-Details: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            SelectedControllerDetails = null;
        }
    }
}