using System.Collections.ObjectModel;
using WorkspaceAnalyser.Models;

namespace WorkspaceAnalyser.Services;

public interface IAnalysisReportService
{
    void SaveAnalysisResults(ObservableCollection<ProjectNode> prjNodes, string baseFilePath);
}