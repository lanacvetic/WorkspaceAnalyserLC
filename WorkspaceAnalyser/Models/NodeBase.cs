using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace WorkspaceAnalyser.Models;

public class NodeBase : INotifyPropertyChanged
{
    private bool _isSelected;
    
    public string Path { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }
    
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// NODES
public class ProjectNode : NodeBase
{
    public string Display => $"{Path}: {FormattedSize}";
    // Remove Path from here
    public string FormattedSize { get; set; }

    [JsonIgnore] public Brush TextColor { get; set; }

    public List<UstNode> Usts { get; set; } = new();
}

public class UstNode : NodeBase
{
    public string Display => $"{Path}: {FormattedSize} ({Percentage:0.##}%)";

    // Remove Path from here
    public string FormattedSize { get; set; }
    public double Percentage { get; set; }

    [JsonIgnore] public Brush TextColor { get; set; }
}