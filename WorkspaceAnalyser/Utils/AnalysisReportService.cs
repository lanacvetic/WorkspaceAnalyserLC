using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using WorkspaceAnalyser.Models;
using WorkspaceAnalyser.Services;

namespace WorkspaceAnalyser.Utils;

public class AnalysisReportService : IAnalysisReportService
{
    public void SaveAnalysisResults(ObservableCollection<ProjectNode> prjNodes, string baseFilePath)
    {
        // ... same code as before, using JsonSerializer ...
        var json = JsonSerializer.Serialize(prjNodes, new JsonSerializerOptions { WriteIndented = true });
        var fileName = Path.GetFileNameWithoutExtension(baseFilePath);
        File.WriteAllText($"{fileName}_analysis.json", json);
    }
}