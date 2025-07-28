using WorkspaceAnalyser.Models;
using WorkspaceAnalyser.Services;

namespace WorkspaceAnalyser.Utils;
// Make sure this matches where you want this service

public class ProjectAnalysisProcessor : IProjectAnalysisProcessor // <--- Add : IProjectAnalysisProcessor
{
    // If DiskAnalyzer and ColorProvider are static, you don't need these fields or constructor parameters.
    // They will be called directly inside the methods.

    public ProjectNode ProcessProject(string projectPath, double workspaceSize, bool sortUstsAscending)
    {
        // --- Project Size and Count ---
        // Calls directly to static methods from DiskAnalyzer
        var projectSize = DiskAnalyzer.GetDiskSize(projectPath);
        var formattedProjectSize = DiskAnalyzer.FormatSize(projectSize);

        // --- Controller (UST) Analysis ---
        var allUsts = DiskAnalyzer.GetAllUstPathsUnderSubtree(projectPath);

        var filteredUsts = allUsts
            .Where(p => projectPath.StartsWith(p, StringComparison.OrdinalIgnoreCase) ||
                        p.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase));

        IEnumerable<string> sortedUsts;
        if (sortUstsAscending)
            sortedUsts = filteredUsts.OrderBy(DiskAnalyzer.GetDiskSize);
        else
            sortedUsts = filteredUsts.OrderByDescending(DiskAnalyzer.GetDiskSize);

        var relevantUsts = sortedUsts.ToList();

        // --- Create ProjectNode Object ---
        var projectNode = new ProjectNode
        {
            Path = projectPath,
            FormattedSize = formattedProjectSize,
            // Calls directly to static methods from ColorProvider and DiskAnalyzer
            TextColor = ColorProvider.GetProjectCategoryColor(DiskAnalyzer.GetPercentage(projectSize, workspaceSize)),
            Usts = relevantUsts.Select(ustPath =>
            {
                // Calls directly to static methods from DiskAnalyzer and ColorProvider
                var ustSize = DiskAnalyzer.GetDiskSize(ustPath);
                var percent = DiskAnalyzer.GetPercentage(ustSize, projectSize);
                var color = ColorProvider.GetUstCategoryColor(percent);
                var formattedUstSize = DiskAnalyzer.FormatSize(ustSize);
                return new UstNode
                {
                    Path = ustPath,
                    FormattedSize = formattedUstSize,
                    Percentage = percent,
                    TextColor = color
                };
            }).ToList()
        };

        return projectNode;
    }
}