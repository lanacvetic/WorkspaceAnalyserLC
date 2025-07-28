using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WorkspaceAnalyser.Converters;

// Converts a null value to Visibility.Collapsed, and non-null to Visibility.Visible.
// Set ConverterParameter='true' for inverse behavior (null becomes Visible).
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Check for parameter to invert logic (e.g., show if null)
        var isInverted = parameter != null && (bool)parameter;

        if (value == null) return isInverted ? Visibility.Visible : Visibility.Collapsed;

        return isInverted ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // One-way conversion, not needed for this scenario.
        throw new NotImplementedException();
    }
}