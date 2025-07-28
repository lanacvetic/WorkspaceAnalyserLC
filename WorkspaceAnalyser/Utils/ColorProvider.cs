using System.Windows.Media;

namespace WorkspaceAnalyser.Utils;

public static class ColorProvider
{
    public static Brush GetUstCategoryColor(double ustPercent)
    {
        return ustPercent switch
        {
            < 33 => Brushes.Green,
            < 66 => Brushes.Orange,
            _ => Brushes.Red
        };
    }

    public static Brush GetProjectCategoryColor(double projectPercent)
    {
        return projectPercent switch
        {
            < 10 => Brushes.Green,
            < 25 => Brushes.Orange,
            _ => Brushes.Red
        };
    }
}