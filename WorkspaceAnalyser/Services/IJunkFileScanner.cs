using WorkspaceAnalyser.Models;

namespace WorkspaceAnalyser.Services;

public interface IJunkFileScanner
{
    Task<List<string>> ScanForJunkFilesAsync(string path, IEnumerable<JunkFilePattern> patterns);
}

