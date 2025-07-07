using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WpfApp;

namespace WorkspaceAnalyser;

public class Junk
{
    public ObservableCollection<string> JunkFiles { get; } = new();

    public async Task ScanForJunkFilesAsync(string rootPath, Dispatcher dispatcher)
    {
        JunkFiles.Clear();

        try
        {
            await Task.Run(() =>
            {
                foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories))
                {
                    if (DiskAnalyzer.IsJunkFile(file))
                    {
                        dispatcher.Invoke(() => JunkFiles.Add(file));
                    }
                }
            });
        }
        catch (Exception ex)
        {
            dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Error scanning files:\n{ex.Message}", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }
}