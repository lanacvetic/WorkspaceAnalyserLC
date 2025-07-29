using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WorkspaceAnalyser.Models;
using WorkspaceAnalyser.Services;

namespace WorkspaceAnalyser.Utils;

public class JunkFileScanner : IJunkFileScanner
{
    // The implementation uses the patterns to perform the search
    public async Task<List<string>> ScanForJunkFilesAsync(string path, IEnumerable<JunkFilePattern> patterns)
    {
        return await Task.Run(() =>
        {
            var junkFiles = new List<string>();
            
            // Get only the patterns that are enabled by the user in the Settings tab
            var enabledPatterns = patterns.Where(p => p.IsEnabled).Select(p => p.Pattern);

            // Search for files matching each enabled pattern
            foreach (var pattern in enabledPatterns)
            {
                try
                {
                    // Search for files matching specific names (e.g., "mainbdf.mot")
                    if (!pattern.Contains('*'))
                    {
                        junkFiles.AddRange(Directory.GetFiles(path, pattern, SearchOption.AllDirectories));
                    }
                    // Search for files matching wildcards (e.g., "*.tmp")
                    else
                    {
                        junkFiles.AddRange(Directory.GetFiles(path, pattern, SearchOption.AllDirectories));
                    }
                }
                catch (IOException) 
                { 
                    // Ignore errors for locked files, long paths, etc.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ignore folders the user doesn't have permission to access
                }
            }
            return junkFiles;
        });
    }
}