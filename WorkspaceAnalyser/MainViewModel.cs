using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkspaceAnalyser.Models;
using WorkspaceAnalyser.Services;
using WorkspaceAnalyser.Utils;

namespace WorkspaceAnalyser;

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

    /// <summary>
    /// Tracks the currently selected node in the tree view to manage a single selection.
    /// </summary>
    private NodeBase? _currentlySelectedNode;

    // Services are injected via the constructor for dependency management.
    private readonly IAnalysisReportService _reportService;
    private readonly IControllerDetailService _controllerDetailService;
    private readonly IJunkFileScanner _junkFileScanner;
    private readonly ObservableCollection<string> _masterControllerContent = new();


    //-------------------------------------------------------------------------
    // Properties
    //-------------------------------------------------------------------------

    /// <summary>
    /// The root path of the workspace to be analyzed. Bound to the main path input field in the UI.
    /// </summary>
    [ObservableProperty] private string _projectViewPath = @"C:\Users\lcvetic\Documents\Workspace";

    /// <summary>
    /// A collection of `ProjectNode` objects representing the projects found in the workspace. 
    /// This is the main data source for the project/controller tree view.
    /// </summary>
    [ObservableProperty] private ObservableCollection<ProjectNode> _prjNodes = new();

    /// <summary>
    /// A collection of file paths identified as junk files by the scanner.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _junkFiles = new();

    /// <summary>
    /// Holds the detailed information for the currently selected controller (`UstNode`). Bound to the details view pane.
    /// </summary>
    [ObservableProperty] private ControllerDetails? _selectedControllerDetails;

    /// <summary>
    /// The current sort order selected by the user (e.g., smallest first, largest first). Bound to a UI control like a ComboBox.
    /// </summary>
    [ObservableProperty] private SortOrder _selectedSortOrder;

    /// <summary>
    /// A boolean flag derived from `SelectedSortOrder`. True for ascending, false for descending. Used internally for sorting logic.
    /// </summary>
    [ObservableProperty] private bool _sortAscending = true;

    /// <summary>
    /// The total number of projects found during the analysis. Bound to a summary display in the UI.
    /// </summary>
    [ObservableProperty] private int _projectNumber;

    /// <summary>
    /// The total number of controllers found across all projects. Bound to a summary display in the UI.
    /// </summary>
    [ObservableProperty] private int _controllerNumber;

    /// <summary>
    /// The total size of the analyzed workspace directory, in bytes.
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
    /// The text used to filter the controller content list.
    /// </summary>
    [ObservableProperty] private string _controllerContentFilter = string.Empty;

    /// <summary>
    /// The filtered list of a controller's files and folders to be displayed in the UI.
    /// </summary>
    [ObservableProperty] private ObservableCollection<string> _displayedControllerContent = new();

    //-------------------------------------------------------------------------
    // Constructor
    //-------------------------------------------------------------------------

    /// <summary>
    /// Initializes a new instance of the `MainViewModel` and instantiates the required services.
    /// </summary>
    public MainViewModel()
    {
        _reportService = new AnalysisReportService();
        _controllerDetailService = new ControllerDetailService();
        _junkFileScanner = new JunkFileScanner();

        // Initialize the default junk file patterns
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
    /// Triggers the analysis based on the current ProjectViewPath.
    /// </summary>
    [RelayCommand]
    private void StartAnalysis()
    {
        // Reset all relevant properties and collections for a clean run.
        PrjNodes.Clear();
        JunkFiles.Clear();
        ProjectNumber = 0;
        ControllerNumber = 0;
        SelectedControllerDetails = null;
        _currentlySelectedNode = null;

        // Validate the input path.
        if (string.IsNullOrWhiteSpace(ProjectViewPath) || !Directory.Exists(ProjectViewPath))
        {
            MessageBox.Show("Die Quelldatei konnte unter folgendem Pfad nicht gefunden werden!", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Calculate the total size of the directory for percentage calculations later.
        WorkspaceSize = DiskAnalyzer.GetDiskSize(ProjectViewPath);

        // --- Case 1: The path points directly to a single controller folder. ---
        if (DiskAnalyzer.IsUstCategory(ProjectViewPath))
        {
            var ustSize = DiskAnalyzer.GetDiskSize(ProjectViewPath);
            var ustNode = new UstNode
            {
                Path = ProjectViewPath,
                FormattedSize = DiskAnalyzer.FormatSize(ustSize),
                Percentage = 100,
                TextColor = ColorProvider.GetUstCategoryColor(100)
            };
            ustNode.PropertyChanged += Node_PropertyChanged;

            var wrapperProjectNode = new ProjectNode
            {
                Path = ProjectViewPath,
                FormattedSize = DiskAnalyzer.FormatSize(ustSize),
                TextColor = Brushes.Black,
                Usts = [ustNode]
            };
            wrapperProjectNode.PropertyChanged += Node_PropertyChanged;

            PrjNodes.Add(wrapperProjectNode);
            ProjectNumber = 1;
            ControllerNumber = 1;
        }
        // --- Case 2: The path points to a single project folder. ---
        else if (DiskAnalyzer.IsProject(ProjectViewPath))
        {
            DisplayProjectAtPath(ProjectViewPath);
        }
        // --- Case 3: The path is a workspace containing multiple projects. ---
        else
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

        // After the analysis is complete, save the results to a JSON report file.
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
    /// <param name="prjNode">The ProjectNode whose folder should be opened.</param>
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
    /// <param name="ustNode">The UstNode whose folder should be opened.</param>
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
    /// <param name="node">The node whose path should be copied.</param>
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
    /// Scans the folder of a given controller and populates the content view.
    /// </summary>
    /// <param name="ustNode">The controller node to inspect.</param>
    [RelayCommand]
    private void ShowControllerContent(UstNode? ustNode)
    {
        _masterControllerContent.Clear();
        ControllerContentFilter = string.Empty;

        if (ustNode == null || !Directory.Exists(ustNode.Path))
        {
            FilterControllerContent(); // This will clear the displayed list
            return;
        }

        try
        {
            var directories = Directory.GetDirectories(ustNode.Path);
            foreach (var dir in directories)
            {
                _masterControllerContent.Add($"📁 {Path.GetFileName(dir)}");
            }

            var files = Directory.GetFiles(ustNode.Path);
            foreach (var file in files)
            {
                _masterControllerContent.Add($"📄 {Path.GetFileName(file)}");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not read directory contents: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        FilterControllerContent();
    }
    

    /// <summary>
    /// Scans the workspace directory for temporary or junk files asynchronously.
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
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

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
    /// Opens the source Log.xml file for a given event using the default system application.
    /// </summary>
    /// <param name="logEvent">The event whose source file should be opened.</param>
    [RelayCommand]
    private void OpenLogFile(LogEvent? logEvent)
    {
        if (logEvent == null || string.IsNullOrWhiteSpace(logEvent.SourceFilePath))
        {
            return;
        }

        if (!File.Exists(logEvent.SourceFilePath))
        {
            MessageBox.Show($"The source file could not be found at: {logEvent.SourceFilePath}", "File Not Found",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo(logEvent.SourceFilePath)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading controller details: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    //-------------------------------------------------------------------------
    // Partial On...Changed Methods
    //-------------------------------------------------------------------------

    /// <summary>
    /// This method is automatically called by the CommunityToolkit.Mvvm source generator
    /// whenever the `SelectedSortOrder` property changes.
    /// </summary>
    /// <param name="value">The new value of the `SelectedSortOrder` property.</param>
    partial void OnSelectedSortOrderChanged(SortOrder value)
    {
        SortAscending = value == SortOrder.KleinsteZuerst;

        if (StartAnalysisCommand.CanExecute(null))
        {
            StartAnalysisCommand.Execute(null);
        }
    }
    
    partial void OnControllerContentFilterChanged(string value)
    {
        FilterControllerContent();
    }

    //-------------------------------------------------------------------------
    // Private Helper Methods
    //-------------------------------------------------------------------------
    
    private void FilterControllerContent()
    {
        DisplayedControllerContent.Clear();

        if (string.IsNullOrWhiteSpace(ControllerContentFilter))
        {
            foreach (var item in _masterControllerContent)
            {
                DisplayedControllerContent.Add(item);
            }
        }
        else
        {
            var filteredItems = _masterControllerContent.Where(item => 
                item.Length > 2 &&
                item.Substring(2).StartsWith(ControllerContentFilter, StringComparison.OrdinalIgnoreCase));
            
            foreach (var item in filteredItems)
            {
                DisplayedControllerContent.Add(item);
            }
        }
    }

    /// <summary>
    /// Analyzes a single project directory, creates a `ProjectNode`, and populates it with its child `UstNode` controllers.
    /// </summary>
    /// <param name="path">The full path to the project directory.</param>
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
    /// Event handler for the `PropertyChanged` event on `ProjectNode` and `UstNode` objects.
    /// It primarily handles changes to the `IsSelected` property to manage UI selection.
    /// </summary>
    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NodeBase.IsSelected))
        {
            return;
        }

        if (sender is NodeBase { IsSelected: true } newlySelectedNode)
        {
            if (_currentlySelectedNode != null && _currentlySelectedNode != newlySelectedNode)
            {
                _currentlySelectedNode.IsSelected = false;
            }

            _currentlySelectedNode = newlySelectedNode;

            if (newlySelectedNode is UstNode ustNode)
            {
                _ = LoadControllerDetailsAsync(ustNode.Path);
            }
        }
    }

    /// <summary>
    /// Asynchronously loads detailed information for a given controller path and updates the `SelectedControllerDetails` property.
    /// </summary>
    /// <param name="ustPath">The path to the controller's "ust.xml" folder.</param>
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