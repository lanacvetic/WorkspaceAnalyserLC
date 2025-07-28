using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;

namespace WorkspaceAnalyser.Converters;

public class KeyEventArgsToEnterCommandConverter : IValueConverter
{
    // The Convert method is called when data is being propagated from the source (the KeyEventArgs) to the target (the bound property, likely a command's CanExecute).
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 1. Type Check: It first attempts to cast the incoming 'value' to a KeyEventArgs object.
        //    This is crucial because the 'value' parameter will be the actual event arguments passed from the UI event.
        if (value is KeyEventArgs args)
        {
            // 2. Key Check: If the cast is successful, it checks if the 'Key' property of the KeyEventArgs is equal to Key.Enter.
            //    This is the core logic: we only care if the Enter key was pressed.
            if (args.Key == Key.Enter)
                // 3. Return True for Enter: If Enter was pressed, it returns 'true'.
                //    This 'true' value would typically enable a command (if bound to CanExecute) or trigger an action.
                return true;

            // 4. Return Null for Other Keys: If any other key was pressed (i.e., not Enter), it returns 'null'.
            //    Returning 'null' or 'DependencyProperty.UnsetValue' typically means the binding should not perform any action or that the command should not execute.
            return null;
        }

        // 5. Return Null for Non-KeyEventArgs: If the incoming 'value' is not a KeyEventArgs (e.g., it's null or some other type), it also returns 'null'.
        return null;
    }

    // The ConvertBack method is called when data is being propagated from the target to the source.
    // In this specific scenario (converting KeyEventArgs to a boolean for a command), there's no logical way to convert a boolean back into a KeyEventArgs.
    // Therefore, it correctly throws a NotImplementedException, indicating that this conversion is one-way.
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}