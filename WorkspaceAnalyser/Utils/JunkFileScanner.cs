using System.IO;
using WorkspaceAnalyser.Services;

namespace WorkspaceAnalyser.Utils;

public class JunkFileScanner : IJunkFileScanner // Implements the interface
{
    // No constructor injection needed for DiskAnalyzer here,
    // because DiskAnalyzer's methods are static and called directly.
    // Explicit parameterless constructor

    public async Task<List<string>> ScanForJunkFilesAsync(string rootPath)
    {
        var junkFiles = new List<string>();
        try
        {
            await Task.Run(() =>
            {
                // Directly call the static DiskAnalyzer.IsJunkFile method
                foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
                    if (DiskAnalyzer.IsJunkFile(file)) // <--- Calling static method directly
                        junkFiles.Add(file);
            });
        }
        catch (Exception ex)
        {
            // Re-throw the exception for the ViewModel to handle (e.g., show MessageBox)
            throw new InvalidOperationException($"Failed to scan for junk files in {rootPath}.", ex);
        }

        return junkFiles;
    }
}