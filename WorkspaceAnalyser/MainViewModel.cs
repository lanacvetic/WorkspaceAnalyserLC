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
    GroessteZuerst  // Largest first
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
    }

    //-------------------------------------------------------------------------
    // Commands
    //-------------------------------------------------------------------------

/// <summary>
/// Triggers the analysis based on the current ProjectViewPath. This simplified version
/// only processes the path if it points directly to a controller folder.
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
        MessageBox.Show("The entered path does not exist or is empty!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        // Use the helper method to analyze and display the single project.
        DisplayProjectAtPath(ProjectViewPath);
    }
    // --- Case 3: The path is a workspace containing multiple projects. ---
    else
    {
        // Find all valid project paths within the workspace.
        var allPrjPaths = DiskAnalyzer.GetAllProjectPaths(ProjectViewPath);
        if (!allPrjPaths.Any())
        {
            MessageBox.Show("No valid project (prj.xml) or controller (ust.xml) found at this path!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Pre-calculate project sizes to allow for sorting.
        var projectSizes = allPrjPaths.Select(prj => new { Path = prj, Size = DiskAnalyzer.GetDiskSize(prj) }).ToList();

        // Sort the projects based on size and the user's selected sort order.
        var sortedProjects = SortAscending
            ? projectSizes.OrderBy(p => p.Size)
            : projectSizes.OrderByDescending(p => p.Size);

        // Process and display each project from the sorted list.
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
        MessageBox.Show($"Error saving analysis JSON: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

    /// <summary>
    /// Opens the selected project's folder in Windows Explorer.
    /// Triggered by a button on a project item in the UI.
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
    /// Opens the selected controller's folder in Windows Explorer and loads its details.
    /// Triggered by a button on a controller item in the UI.
    /// </summary>
    /// <param name="ustNode">The UstNode whose folder should be opened.</param>
    [RelayCommand]
    private void OpenUstFolder(UstNode ustNode)
    {
        if (ustNode != null)
        {
            // Asynchronously load details for the selected controller.
            _ = LoadControllerDetailsAsync(ustNode.Path);
            Process.Start("explorer.exe", ustNode.Path);
        }
    }

    /// <summary>
    /// Scans the workspace directory for temporary or junk files asynchronously.
    /// </summary>
    [RelayCommand]
    private async Task ScanForJunkFilesAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectViewPath) || !Directory.Exists(ProjectViewPath))
        {
            MessageBox.Show("The entered path does not exist or is empty!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            // Use the junk file scanner service to find files.
            var foundJunkFiles = await _junkFileScanner.ScanForJunkFilesAsync(ProjectViewPath);
            foreach (var file in foundJunkFiles)
            {
                JunkFiles.Add(file);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error scanning for junk files:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show("No junk files found to delete. Please scan first.", "Nothing to Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Ask the user for confirmation as this is a destructive and irreversible action.
        var confirmResult = MessageBox.Show(
            $"Are you sure you want to PERMANENTLY delete {JunkFiles.Count} file(s)?\n\nThis action CANNOT be undone.",
            "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);

        if (confirmResult != MessageBoxResult.Yes) return;

        var successfullyDeletedFiles = new List<string>();
        var failedToDeleteFiles = new List<string>();

        // Run the deletion on a background thread to keep the UI responsive.
        await Task.Run(() =>
        {
            var filesToDelete = JunkFiles.ToList(); // Create a copy to iterate over while modifying the original collection.
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

        // Update the UI on the main thread after the background task is complete.
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Remove successfully deleted files from the UI list.
            foreach (var deletedFile in successfullyDeletedFiles)
            {
                JunkFiles.Remove(deletedFile);
            }
            
            // Show a summary message to the user.
            var summary = new StringBuilder("Deletion complete.\n\n");
            if (failedToDeleteFiles.Any())
            {
                summary.AppendLine($"{successfullyDeletedFiles.Count} file(s) successfully deleted.");
                summary.AppendLine($"{failedToDeleteFiles.Count} file(s) failed to delete:");
                summary.Append(string.Join("\n", failedToDeleteFiles));
                MessageBox.Show(summary.ToString(), "Deletion Results - With Errors", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                summary.Append("All selected junk files were successfully deleted!");
                MessageBox.Show(summary.ToString(), "Deletion Results", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });
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
        // Update the boolean flag used for sorting.
        SortAscending = value == SortOrder.KleinsteZuerst;
        
        // If the analysis can be run, re-run it to apply the new sort order.
        if (StartAnalysisCommand.CanExecute(null))
        {
            StartAnalysisCommand.Execute(null);
        }
    }
    
    //-------------------------------------------------------------------------
    // Private Helper Methods
    //-------------------------------------------------------------------------

    /// <summary>
    /// Analyzes a single project directory, creates a `ProjectNode`, and populates it with its child `UstNode` controllers.
    /// </summary>
    /// <param name="path">The full path to the project directory.</param>
    private void DisplayProjectAtPath(string path)
    {
        var projectSize = DiskAnalyzer.GetDiskSize(path);
        ProjectNumber++;

        // Find all controller paths ("ust.xml" folders) within this project's directory.
        var allUsts = DiskAnalyzer.GetAllUstPathsUnderSubtree(path);
        var filteredUsts = allUsts.Where(p => p.StartsWith(path, StringComparison.OrdinalIgnoreCase));

        // Sort the controllers within the project based on their size.
        var sortedUsts = SortAscending
            ? filteredUsts.OrderBy(DiskAnalyzer.GetDiskSize)
            : filteredUsts.OrderByDescending(DiskAnalyzer.GetDiskSize);

        // Create the main ProjectNode for the UI.
        var projectNode = new ProjectNode
        {
            Path = path,
            FormattedSize = DiskAnalyzer.FormatSize(projectSize),
            // The color is determined by the project's size relative to the entire workspace.
            TextColor = ColorProvider.GetProjectCategoryColor(DiskAnalyzer.GetPercentage(projectSize, WorkspaceSize)),
            // Create a UstNode for each controller found.
            Usts = sortedUsts.Select(ustPath =>
            {
                var ustSize = DiskAnalyzer.GetDiskSize(ustPath);
                var ustNode = new UstNode
                {
                    Path = ustPath,
                    FormattedSize = DiskAnalyzer.FormatSize(ustSize),
                    // Percentage and color are relative to the parent project's size.
                    Percentage = DiskAnalyzer.GetPercentage(ustSize, projectSize),
                    TextColor = ColorProvider.GetUstCategoryColor(DiskAnalyzer.GetPercentage(ustSize, projectSize))
                };
                ustNode.PropertyChanged += Node_PropertyChanged; // Hook up a selection handler.
                return ustNode;
            }).ToList()
        };

        projectNode.PropertyChanged += Node_PropertyChanged; // Hook up a selection handler.
        ControllerNumber += projectNode.Usts.Count; // Add to the total controller count.
        PrjNodes.Add(projectNode); // Add the fully populated project node to the main collection for display.
    }

    /// <summary>
    /// Event handler for the `PropertyChanged` event on `ProjectNode` and `UstNode` objects.
    /// It primarily handles changes to the `IsSelected` property to manage UI selection.
    /// </summary>
    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // We only care about the 'IsSelected' property.
        if (e.PropertyName != nameof(NodeBase.IsSelected))
        {
            return;
        }

        if (sender is NodeBase { IsSelected: true } newlySelectedNode)
        {
            // If another node was already selected, deselect it to enforce a single selection.
            if (_currentlySelectedNode != null && _currentlySelectedNode != newlySelectedNode)
            {
                _currentlySelectedNode.IsSelected = false;
            }
            // Track the newly selected node.
            _currentlySelectedNode = newlySelectedNode;

            // If the selected node is a controller, trigger loading its details.
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
            // Use the service to fetch details and update the UI-bound property.
            SelectedControllerDetails = await _controllerDetailService.GetControllerDetailsAsync(ustPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading controller details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            SelectedControllerDetails = null; // Clear details on an error.
        }
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
            // --- START: Temporary Debugging Code ---
            string pathValue = logEvent?.SourceFilePath ?? "The path is NULL";
            MessageBox.Show($"Command is exiting early. The file path is: '{pathValue}'");
            // --- END: Temporary Debugging Code ---
            return;
        }

        if (!File.Exists(logEvent.SourceFilePath))
        {
            MessageBox.Show($"The source file could not be found at: {logEvent.SourceFilePath}", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            MessageBox.Show($"Could not open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}