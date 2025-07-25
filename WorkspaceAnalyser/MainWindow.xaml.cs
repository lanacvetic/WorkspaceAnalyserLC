﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Media;
using System.Windows.Input;
using System.Text.Json;
using WorkspaceAnalyser;

namespace WpfApp;

public class ProjectNode
{
    public string Display => $"{Path}: {FormattedSize}";
    public string Path { get; set; }
    public string FormattedSize { get; set; }
    public Brush TextColor { get; set; }
    public List<UstNode> Usts { get; set; } = new();
}

public class UstNode
{
    public string Display => $"{Path}: {FormattedSize} ({Percentage:0.##}%)";

    public string Path { get; set; }
    public string FormattedSize { get; set; }
    public double Percentage { get; set; }
    public Brush TextColor { get; set; }
}

/// <summary>
///     Interaktionslogik für MainWindow.xaml.
///     Diese Klasse verwaltet die Hauptlogik der WPF-Anwendung zur Analyse von Plattengrößen.
/// </summary>
public partial class MainWindow : Window
{
    // Consider making this configurable via settings or a user input for flexibility.
    // As it stands, it's a hardcoded default that is overridden by UI input.
    // private static readonly string DefaultRootPath = "C:\\Users\\lcvetic\\Documents\\Workspace";

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainWindow" /> class.
    ///     Sets up UI components and subscribes to the Start Analysis button click event.
    /// </summary>
    private double _workspaceSize;

    private int _projectNumber;
    private int _controllerNumber;
    private List<string> _junkFiles = new();

    public List<string> JunkFiles
    {
        get => _junkFiles;
        set
        {
            _junkFiles = value;
            SetDisplayNumber();
        }
    }

    public int PrjNumber
    {
        get => _projectNumber;
        set
        {
            _projectNumber = value;
            SetDisplayNumber();
        }
    }

    public int UstNumber
    {
        get => _controllerNumber;
        set
        {
            _controllerNumber = value;
            SetDisplayNumber();
        }
    }
    
    private readonly Junk _junkScanner = new();
    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        
        // Event handlers can be assigned directly in XAML (e.g., Click="StartAnalysis_Click")
        // for better separation of concerns, but programmatic assignment is also valid.
        StartAnalysisButton.Click += StartAnalysis_Click;

        // Initialize ComboBox selection if not set in XAML
        if (SortierungComboBox.SelectedItem == null && SortierungComboBox.Items.Count > 0)
            SortierungComboBox.SelectedIndex = 0; // Select the first item by default
    }

    private void SetDisplayNumber()
    {
        DisplayNum.Content = $"Projekte: {PrjNumber} | Controller: {UstNumber}";
    }

    /// <summary>
    ///     Handles the KeyDown event for the RootPathTextBox, triggering analysis on Enter key press.
    /// </summary>
    private void RootPathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            // Simulate a button click
            StartAnalysis_Click(StartAnalysisButton, new RoutedEventArgs());
    }


    /// <summary>
    ///     Event handler for the "Start Analysis" button.
    ///     Performs disk size analysis based on the entered path and displays the results.
    /// </summary>
    public void StartAnalysis_Click(object sender, RoutedEventArgs e)
    {

        ProjectsTreeView.Items.Clear(); // Clear previous results
        PrjNumber = 0;
        UstNumber = 0;

        var inputPath = RootPathTextBox.Text.Trim();

        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var jsonString = JsonSerializer.Serialize(inputPath);
        Console.WriteLine(jsonString);
        using var createStream = File.Create(fileName + ".json");


        if (string.IsNullOrWhiteSpace(inputPath) || !Directory.Exists(inputPath))
        {
            MessageBox.Show("The entered path does not exist or is empty!", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _workspaceSize = DiskAnalyzer.GetDiskSize(inputPath);
        // Determine sorting preference
        var sortAscending =
            (SortierungComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Kleinste zuerst";

        // PROJECT DISPLAY 
        if (DiskAnalyzer.IsProject(inputPath))
        {
            // If the input path is a project, display only this project.
            DisplaySingleProject(inputPath, sortAscending);
        }
        else
        {
            // If the input path is not a project, try to find the nearest project root.
            var prjRoot = DiskAnalyzer.FindNearestProjectRoot(inputPath);

            if (prjRoot != null && prjRoot.Equals(inputPath, StringComparison.OrdinalIgnoreCase))
            {
                // The input path itself is the nearest project root.
                DisplaySingleProject(inputPath, sortAscending);
            }
            else if (prjRoot != null)
            {
                // We are deep within a single project, display this project.
                DisplaySingleProject(inputPath, sortAscending);
            }
            else
            {
                // If no nearest project root found, or it's not the input path itself,
                // assume we are at a level that might contain multiple project folders.
                var allPrjPaths = DiskAnalyzer.GetAllProjectPaths(inputPath);


                if (!allPrjPaths.Any())
                {
                    MessageBox.Show("No valid project (prj.xml) found in the path!", "Error", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // Calculate sizes once and then sort
                var projectSizes = allPrjPaths
                    .Select(prj => new { Path = prj, Size = DiskAnalyzer.GetDiskSize(prj) })
                    .ToList();
                //Fragezeichnen Teil???
                projectSizes = sortAscending
                    ? projectSizes.OrderBy(p => p.Size).ToList()
                    : projectSizes.OrderByDescending(p => p.Size).ToList();

                // Display projects in sorted order
                foreach (var prj in projectSizes) DisplaySingleProject(prj.Path, sortAscending);
            }
        }
    }


    //Open explorer for the Contoller buttons function taken from the UI Button
    public void OpenExplorerButton(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is UstNode ustNode)
            Process.Start("explorer.exe", ustNode.Path);
    }

    //Open explorer for the Project buttons function taken from the UI Button
    public void OpenFolderButton(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is ProjectNode prjNode)
            Process.Start("explorer.exe", prjNode.Path);
    }
    
    private async void JunkFilesClick(object sender, RoutedEventArgs e)
    {
        var inputPath = RootPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(inputPath) || !Directory.Exists(inputPath))
        {
            MessageBox.Show("Please enter a valid directory path first.", "Invalid Path",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Run the scan
        await _junkScanner.ScanForJunkFilesAsync(inputPath, Dispatcher);

        // Update ListBox
        JunkFilesListBox.ItemsSource = null; // Reset first
        if (_junkScanner.JunkFiles.Count == 0)
        {
            MessageBox.Show("No junk files found.", "Result", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            JunkFilesListBox.ItemsSource = _junkScanner.JunkFiles;
            JunkFilesListBox.ItemsSource = _junkScanner.JunkFiles.Take(20).ToList();
        }
    }

    /// <summary>
    ///     Displays the details of a single project and its UST categories in the UI.
    /// </summary>
    /// <param name="projectRoot">The root path of the project (where prj.xml is located).</param>
    /// <param name="sortAscending">True to sort USTs in ascending order by size, false for descending.</param>
    private void DisplaySingleProject(string projectRoot, bool sortAscending)
    {
        //Makes the conversion happen
        var projectSize = DiskAnalyzer.GetDiskSize(projectRoot);
        var formattedProjectSize = DiskAnalyzer.FormatSize(projectSize);
        PrjNumber++;

        var allUsts = DiskAnalyzer.GetAllUstPathsUnderSubtree(projectRoot);
        UstNumber += allUsts.Count;

        var relevantUsts = allUsts
            .Where(p => projectRoot.StartsWith(p, StringComparison.OrdinalIgnoreCase) ||
                        p.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            .ToList();

        relevantUsts = sortAscending
            ? relevantUsts.OrderBy(DiskAnalyzer.GetDiskSize).ToList()
            : relevantUsts.OrderByDescending(DiskAnalyzer.GetDiskSize).ToList();

        var projectNode = new ProjectNode
        {
            TextColor = GetUstCategoryColor(DiskAnalyzer.GetPercentage(projectSize, _workspaceSize)),
            Path = projectRoot,
            FormattedSize = formattedProjectSize,
            Usts = relevantUsts.Select(ustPath =>
            {
                var ustSize = DiskAnalyzer.GetDiskSize(ustPath);
                var percent = DiskAnalyzer.GetPercentage(ustSize, projectSize);

                var color = GetUstCategoryColor(percent);

                return new UstNode
                {
                    Path = ustPath,
                    FormattedSize = DiskAnalyzer.FormatSize(ustSize),
                    Percentage = percent,
                    TextColor = color
                };
            }).ToList()
        };


        ProjectsTreeView.Items.Add(projectNode);
    }

    /// <summary>
    ///     Determines the text color for a UST category based on its percentage of the total project size.
    /// </summary>
    /// <param name="ustPercent">The percentage of the UST category size relative to the project size.</param>
    /// <returns>A <see cref="Brush" /> representing the color.</returns>
    private static Brush GetUstCategoryColor(double ustPercent)
    {
        return ustPercent switch
        {
            < 33 => Brushes.Green,
            < 66 => Brushes.Orange,
            _ => Brushes.Red
        };
    }

    /// <summary>
    ///     Handles the SelectionChanged event for the SortierungComboBox, re-running analysis.
    /// </summary>
    private void SortierungComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Only trigger analysis if the root path text box has content,
        // preventing unnecessary runs on initial load or empty input.
        if (!string.IsNullOrWhiteSpace(RootPathTextBox.Text))
            StartAnalysis_Click(StartAnalysisButton, new RoutedEventArgs());
    }
}

/// <summary>
///     A static helper class for disk analysis operations, promoting better separation of concerns.
/// </summary>
public static class DiskAnalyzer
{
    /// <summary>
    ///     Recursively finds all UST category paths under a given root directory.
    ///     A UST category is identified by the presence of a 'ust.xml' file in the folder.
    /// </summary>
    /// <param name="root">The path from which to start the search.</param>
    /// <returns>A list of strings containing the paths to all found UST categories.</returns>
    public static List<string> GetAllUstPathsUnderSubtree(string root)
    {
        // Initialize an empty list to store the paths of identified "UST" directories.
        var ustPaths = new List<string>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                // Check if current directory is a "UST" category
                if (IsUstCategory(dir)) ustPaths.Add(dir);
                // Recursively search subdirectories and add results
                ustPaths.AddRange(GetAllUstPathsUnderSubtree(dir));
            }
        }

        // Handle access denied errors gracefully
        catch (UnauthorizedAccessException ex)
        {
            // Log specific access denied errors if needed, otherwise just skip
            Console.WriteLine($"Access denied to directory '{root}': {ex.Message}");
        }
        // Handle other general directory access errors
        catch (Exception ex)
        {
            Console.WriteLine($"Error accessing directory '{root}': {ex.Message}");
        }

        return ustPaths;
    }

    /// <summary>
    ///     Finds the nearest parent project root path relative to a given path.
    ///     A project root path is a directory that contains a 'prj.xml' file.
    /// </summary>
    /// <param name="path">The starting path from which to search upwards.</param>
    /// <returns>The full path of the nearest project root directory, or null if none is found.</returns>
    public static string FindNearestProjectRoot(string path)
    {
        var dir = new DirectoryInfo(path);
        while (dir != null)
        {
            if (IsProject(dir.FullName)) return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    ///     Calculates the total size of all files in a directory (including subdirectories).
    /// </summary>
    /// <param name="path">Path of the directory to analyze.</param>
    /// <returns>Total size in bytes as double, or 0 on errors (e.g., access denied).</returns>
    public static double GetDiskSize(string path)
    {
        try
        {
            // Use long for file lengths to avoid potential overflow with very large files
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(fi => (double)fi.Length);
        }
        catch (UnauthorizedAccessException)
        {
            // Silently ignore access denied errors and return 0
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error calculating disk size for '{path}': {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    ///     Formats a byte size into human-readable units (B, KB, MB, GB).
    /// </summary>
    /// <param name="size">Size in bytes (double).</param>
    /// <returns>Formatted string (e.g., "1.5 MB").</returns>
    public static string FormatSize(double size)
    {
        const double KB = 1024;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        return size switch
        {
            >= GB => $"{size / GB:0.##} GB",
            >= MB => $"{size / MB:0.##} MB",
            >= KB => $"{size / KB:0.##} KB",
            _ => $"{size} B"
        };
    }

    /// <summary>
    ///     Calculates the percentage of a part size relative to a total size.
    /// </summary>
    /// <param name="partSize">The size of the part (double).</param>
    /// <param name="totalSize">The total size (double).</param>
    /// <returns>The percentage (value from 0 to 100), or 0 if the total size is 0.</returns>
    public static double GetPercentage(double partSize, double totalSize)
    {
        return totalSize == 0 ? 0 : partSize / totalSize * 100.0;
    }

    /// <summary>
    ///     Checks if a directory is a project.
    ///     A directory is considered a project if it contains the file 'prj.xml'.
    /// </summary>
    /// <param name="path">Path of the directory to check.</param>
    /// <returns>True if 'prj.xml' exists in the specified path, otherwise False.</returns>
    public static bool IsProject(string path)
    {
        return File.Exists(Path.Combine(path, "prj.xml"));
    }

    /// <summary>
    ///     Checks if a directory is a UST category.
    ///     A UST category is a folder that contains the file 'ust.xml'.
    /// </summary>
    /// <param name="path">Path of the directory to check.</param>
    /// <returns>True if 'ust.xml' exists in the specified path, otherwise False.</returns>
    public static bool IsUstCategory(string path)
    {
        return File.Exists(Path.Combine(path, "ust.xml"));
    }

    /// <summary>
    ///     Finds all project paths by recursively searching all subdirectories from a starting path.
    ///     A directory is recognized as a project if it contains the file 'prj.xml'.
    /// </summary>
    /// <param name="path">The starting path for the project search.</param>
    /// <returns>A list of strings containing the full paths to all found projects.</returns>
    public static List<string> GetAllProjectPaths(string path)
    {
        var projectPaths = new List<string>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                if (IsProject(dir)) projectPaths.Add(dir);

                projectPaths.AddRange(GetAllProjectPaths(dir)); // Recursive call
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Access denied to directory '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error accessing directory '{path}': {ex.Message}");
        }

        return projectPaths;
    }

    public static bool IsJunkFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLower();

        var junkPatterns = new[]
        {
            "mainbdf.mot", "mainbdf.ppe", "grafikbilderinfo.txt", "*.bak", "*.tmp", "*.sav",
            "mainbdf_fbg5.inc", "mainbdf_tup.inc", "fupliste.xml", "fupliste.xmlpl", "fupblattliste.mnu"
        };

        return junkPatterns.Any(pattern =>
            pattern.StartsWith("*.")
                ? fileName.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase)
                : fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase));
    }
}
 












 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 
 



