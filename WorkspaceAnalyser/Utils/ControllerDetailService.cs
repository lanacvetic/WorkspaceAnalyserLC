using System.IO;
using System.Xml.Linq;
using WorkspaceAnalyser.Models;

namespace WorkspaceAnalyser.Services;

/// <summary>
/// A service responsible for extracting detailed information about a single controller.
/// It implements the <c>IControllerDetailService</c> interface.
/// </summary>
public class ControllerDetailService : IControllerDetailService
{
    /// <summary>
    /// Asynchronously gathers details about a controller by reading and parsing various files
    /// within its specific directory.
    /// </summary>
    /// <param name="ustPath">The file path to the controller's root directory (the one containing ust.xml).</param>
    /// <returns>A <c>Task</c> that resolves to a <c>ControllerDetails</c> object populated with the found information.</returns>
    public async Task<ControllerDetails> GetControllerDetailsAsync(string ustPath)
    {
        // Use Task.Run to offload the file I/O operations to a background thread,
        // preventing the UI from freezing while reading from the disk.
        return await Task.Run(() =>
        {
            var details = new ControllerDetails { Path = ustPath };

            // --- 1. Count Macros and FUP Sheets ---
            // This section differentiates between two types of logic files based on their extensions.
            var utfFiles = Directory.GetFiles(ustPath, "*.utf");
            var macroCount = 0;
            var fupSheetCount = 0;

            foreach (var utfFile in utfFiles)
            {
                // A .utf file is considered a "macro" if a corresponding .u file exists.
                var uFile = Path.ChangeExtension(utfFile, ".u");
                if (File.Exists(uFile))
                    macroCount++;
                else // Otherwise, it's considered a "FUP sheet".
                    fupSheetCount++;
            }

            // Populate the model with the counted values.
            details.AnzahlMakros = macroCount.ToString();
            details.AnzahlFUPBlaetter = fupSheetCount.ToString();

            // --- 2. Read Data from upl/ldopen.xml ---
            // This file contains configuration details for the controller hardware.
            var ldOpenPath = Path.Combine(ustPath, "upl", "ldopen.xml");
            if (File.Exists(ldOpenPath))
            {
                try
                {
                    var xDoc = XDocument.Load(ldOpenPath);
                    // Safely access XML elements using the null-conditional operator and provide default values.
                    details.Hardwaretyp = xDoc.Root?.Element("HardwareType")?.Value ?? "Nicht gefunden";
                    details.CPVersion = xDoc.Root?.Element("Version")?.Value ?? "Nicht gefunden";
                    details.IPAdresse = xDoc.Root?.Element("IP")?.Value ?? "Nicht gefunden";
                }
                catch
                {
                    // If the XML file is corrupt or cannot be read, set an error message.
                    details.Hardwaretyp = "Fehler beim Lesen der XML";
                }
            }


            // --- 3. Check for neuueber.txt ---
            // The existence of this file indicates that the controller's code has been recently compiled ("übersetzt").
            var neuUeberPath = Path.Combine(ustPath, "neuueber.txt");
            details.Uebersetzt = File.Exists(neuUeberPath);

            if (details.Uebersetzt)
            {
                // If the file exists, read the last two lines, which typically contain
                // the timestamp and result of the last compilation.
                var lines = File.ReadLines(neuUeberPath).TakeLast(2).ToList();
                details.Uebersetzungstext = string.Join("\n", lines);
            }
            else
            {
                details.Uebersetzungstext = "Keine Übersetzungsdatei gefunden.";
            }

            return details;
        });
    }
}