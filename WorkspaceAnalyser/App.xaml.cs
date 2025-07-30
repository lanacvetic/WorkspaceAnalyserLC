using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WorkspaceAnalyser;

public partial class App : Application
{
    public App()
    {
        // Subscribe to the unhandled exception event
        DispatcherUnhandledException += App_DispatcherUnhandledException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Prevent the application from crashing immediately
        e.Handled = true;

        // Create a user-friendly error message
        string errorMessage = $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nStack Trace:\n{e.Exception.StackTrace}";

        try
        {
            // Define a path for the log file
            string logPath = Path.Combine(AppContext.BaseDirectory, "startup_error_log.txt");

            // Write the error message to the log file
            File.WriteAllText(logPath, errorMessage);

            // Inform the user that a log was created
            MessageBox.Show($"A critical error occurred. A log file has been created at:\n{logPath}", "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception)
        {
            // Fallback if we can't even write the file
            MessageBox.Show(errorMessage, "Critical Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        // Shutdown the application
        Shutdown();
    }
}