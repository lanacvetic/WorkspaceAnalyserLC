using System.Windows;

namespace WfpDropdownExample;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Event handler for the button click
    private void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        // Toggle visibility of the TextBlock displaying info
        if (infoText.Visibility == Visibility.Collapsed)
        {
            infoText.Visibility = Visibility.Visible;
            infoText.Text = GetInformation(); // Show dynamic info
        }
        else
        {
            infoText.Visibility = Visibility.Collapsed; // Hide info if it's already visible
        }
    }

    // Example method that generates dynamic info (you can replace this with actual logic)
    private string GetInformation()
    {
        // For example: we could show the current date and time, or some data from your app
        return $"Current time: {DateTime.Now}";
    }
}