using System.IO;

namespace WorkspaceAnalyser.Utils;

public class DiskAnalyzer
{
    /// <summary>
    ///     Recursively finds all UST category paths under a given root directory.
    ///     A UST category is identified by the presence of a 'ust.xml' file in the folder.
    /// </summary>
    /// <param name="root">The path from which to start the search.</param>
    /// <returns>A list of strings containing the paths to all found UST categories.</returns>
    public static List<string> GetAllUstPathsUnderSubtree(string root)
    {
        var ustPaths = new List<string>();
        try
        {
            // FIX: Check the root folder itself first.
            if (IsUstCategory(root))
            {
                ustPaths.Add(root);
            }

            // Now, continue to search in its sub-folders recursively.
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                ustPaths.AddRange(GetAllUstPathsUnderSubtree(dir));
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Access denied to directory '{root}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error accessing directory '{root}': {ex.Message}");
        }

        return ustPaths;
    }

    /// <summary>
    ///     Finds the nearest parent project root path relative to a given path by searching upwards.
    ///     A project root path is a directory that contains a 'prj.xml' file.
    /// </summary>
    /// <param name="path">The starting path from which to search upwards.</param>
    /// <returns>The full path of the nearest project root directory, or null if none is found.</returns>
    public static string FindNearestProjectRoot(string path)
    {
        // Create a DirectoryInfo object for the starting path. This allows easy access to the parent directory.
        var dir = new DirectoryInfo(path);

        // Loop upwards through the directory hierarchy until the root directory is reached (dir becomes null).
        while (dir != null)
        {
            // Check if the current directory is a project root.
            if (IsProject(dir.FullName))
                return dir.FullName; // If found, return its full path immediately.

            // Move up to the parent directory for the next iteration.
            dir = dir.Parent;
        }

        // If the loop completes without finding a project root, return null.
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
            // Create a DirectoryInfo object for the specified path.
            // Enumerate all files within the directory and its subdirectories (SearchOption.AllDirectories).
            // Sum the lengths of all files. Cast fi.Length (long) to double for the sum to handle large sizes and consistent return type.
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(fi => (double)fi.Length);
        }
        // Handle UnauthorizedAccessException specifically. This is common when trying to access system directories
        // or directories for which the current user lacks permissions.
        catch (UnauthorizedAccessException)
        {
            // Silently ignore access denied errors and return 0. This prevents the application from crashing
            // and allows it to continue processing other accessible directories.
            return 0;
        }
        // Catch any other general exceptions that might occur during file enumeration or size calculation.
        catch (Exception ex)
        {
            // Log the error message to the console.
            Console.WriteLine($"Error calculating disk size for '{path}': {ex.Message}");
            return 0; // Return 0 to indicate an error or inability to calculate size.
        }
    }

    /// <summary>
    ///     Formats a byte size into human-readable units (B, KB, MB, GB).
    /// </summary>
    /// <param name="size">Size in bytes (double).</param>
    /// <returns>Formatted string (e.g., "1.5 MB").</returns>
    public static string FormatSize(double size)
    {
        // Define constants for common units of size.
        const double KB = 1024;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        // Use a switch expression (C# 8.0+) for concise conditional formatting.
        return size switch
        {
            // If size is 1 GB or more, format as GB with two decimal places.
            >= GB => $"{size / GB:0.##} GB",
            // If size is 1 MB or more, format as MB with two decimal places.
            >= MB => $"{size / MB:0.##} MB",
            // If size is 1 KB or more, format as KB with two decimal places.
            >= KB => $"{size / KB:0.##} KB",
            // Otherwise (size is less than 1 KB), format as B (bytes) with no decimal places.
            _ => $"{size} B"
        };
    }

    /// <summary>
    ///     Calculates the percentage of a part size relative to a total size.
    /// </summary>
    /// <param name="partSize">The size of the part (double).</param>
    /// <param name="totalSize">The total size (double).</param>
    /// <returns>The percentage (value from 0 to 100), or 0 if the total size is 0 to avoid division by zero.</returns>
    public static double GetPercentage(double partSize, double totalSize)
    {
        // Prevent division by zero: if totalSize is 0, return 0%.
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
        // Combines the directory path with the file name "prj.xml" and checks if the file exists.
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
        // Combines the directory path with the file name "ust.xml" and checks if the file exists.
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
            // FIX: Check the starting path itself first.
            if (IsProject(path))
            {
                projectPaths.Add(path);
            }

            // Now, search in the sub-folders recursively.
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                projectPaths.AddRange(GetAllProjectPaths(dir)); 
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

    /// <summary>
    ///     Checks if a given file path corresponds to a "junk file" based on a predefined list of patterns.
    ///     This method is duplicated from MainViewModel. It's generally better to have a single
    ///     source of truth for such utility functions.
    /// </summary>
    /// <param name="filePath">The full path of the file to check.</param>
    /// <returns>True if the file matches any of the junk patterns, False otherwise.</returns>
    public static bool IsJunkFile(string filePath)
    {
        // Get the file name from the full path and convert it to lowercase for case-insensitive comparison.
        var fileName = Path.GetFileName(filePath).ToLower();

        // Define an array of patterns that identify "junk" files.
        // Patterns can be exact file names or wildcard patterns like "*.bak".
        var junkPatterns = new[]
        {
            "mainbdf.mot", "mainbdf.ppe", "grafikbilderinfo.txt", "*.bak", "*.tmp", "*.sav",
            "mainbdf_fbg5.inc", "mainbdf_tup.inc", "fupliste.xml", "fupliste.xmlpl", "fupblattliste.mnu"
        };

        // Use LINQ's Any() method to check if the file name matches any of the patterns.
        return junkPatterns.Any(pattern =>
            pattern.StartsWith("*.") // Check if the pattern starts with "*. " (indicating a file extension match).
                ? fileName.EndsWith(pattern.Substring(1),
                    StringComparison
                        .OrdinalIgnoreCase) // If so, check if the file name ends with the pattern's extension (substring after "*").
                : fileName.Equals(pattern,
                    StringComparison
                        .OrdinalIgnoreCase)); // Otherwise (for exact name patterns), check if the file name is an exact match.
    }
}