namespace WorkspaceAnalyser;

public class UstNode
{
    public string Display => $"{Path}: {FormattedSize} ({Percentage:0.##}%)";
    public string Path { get; set; }
    public string FormattedSize { get; set; }
    public double Percentage { get; set; }
}