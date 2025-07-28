using System.Windows;
using WorkspaceAnalyser;

namespace WpfApp;

/// <summary>
///    Interaction logic for the MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}