namespace WorkspaceAnalyser.Services;

public interface IJunkFileScanner
{
    Task<List<string>> ScanForJunkFilesAsync(string rootPath);
}