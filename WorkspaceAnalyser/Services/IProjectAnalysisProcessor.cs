using WorkspaceAnalyser.Models;

namespace WorkspaceAnalyser.Services;

public interface IProjectAnalysisProcessor
{
    ProjectNode ProcessProject(string projectPath, double workspaceSize, bool sortUstsAscending);
}