using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using WorkspaceAnalyser.Models;
using WorkspaceAnalyser.Services;

namespace WorkspaceAnalyser.Utils;

/// <summary>
/// A service responsible for extracting detailed information about a single controller.
/// It implements the <c>IControllerDetailService</c> interface.
/// </summary>
public class ControllerDetailService : IControllerDetailService
{
    /// <summary>
    /// Asynchronously gathers all details about a controller by reading and parsing various files
    /// within its specific directory.
    /// </summary>
    /// <param name="ustPath">The file path to the controller's root directory.</param>
    /// <returns>A <c>Task</c> that resolves to a <c>ControllerDetails</c> object populated with the found information.</returns>
    public async Task<ControllerDetails> GetControllerDetailsAsync(string ustPath)
    {
        // Offload all file I/O to a background thread to keep the UI responsive.
        return await Task.Run(() =>
        {
            var details = new ControllerDetails { Path = ustPath };

            // --- 1. Count Macros and FUP Sheets ---
            try
            {
                var utfFiles = Directory.GetFiles(ustPath, "*.utf");
                details.AnzahlMakros = utfFiles.Count(utf => File.Exists(Path.ChangeExtension(utf, ".u"))).ToString();
                details.AnzahlFUPBlaetter = (utfFiles.Length - int.Parse(details.AnzahlMakros)).ToString();
            }
            catch { /* Ignore errors if files can't be accessed */ }


            // --- 2. Read Data from upl/ldopen.xml ---
            var ldOpenPath = Path.Combine(ustPath, "upl", "ldopen.xml");
            if (File.Exists(ldOpenPath))
            {
                try
                {
                    var xDoc = XDocument.Load(ldOpenPath);
                    details.Hardwaretyp = xDoc.Root?.Element("HardwareType")?.Value ?? "N/A";
                    details.CPVersion = xDoc.Root?.Element("Version")?.Value ?? "N/A";
                    details.IPAdresse = xDoc.Root?.Element("IP")?.Value ?? "N/A";
                }
                catch { details.Hardwaretyp = "Error reading XML"; }
            }


            // --- 3. Check for neuueber.txt (Translation File) ---
            var neuUeberPath = Path.Combine(ustPath, "neuueber.txt");
            details.Uebersetzt = File.Exists(neuUeberPath);
            if (details.Uebersetzt)
            {
                details.Uebersetzungstext = string.Join("\n", File.ReadLines(neuUeberPath).TakeLast(2));
            }
            else
            {
                details.Uebersetzungstext = "No translation file found.";
            }


            // --- 4. Read Events from all Log.xml files ---
            var events = new List<LogEvent>();
            try
            {
                var logFiles = Directory.GetFiles(ustPath, "Log.xml", SearchOption.AllDirectories);
                foreach (var logFile in logFiles)
                {
                    var doc = XDocument.Load(logFile);
                    
                    var newFileEvents = doc.Descendants("Neu")
                        .Select(el => new LogEvent(
                            Date: el.Element("Datum")?.Value ?? "Unknown Date",
                            Title: $"New file created: {el.Element("Name")?.Value ?? "N/A"}"
                        ));
                    events.AddRange(newFileEvents);

                    var pluginEvents = doc.Descendants("PlugIn_importiert")
                        .Select(el => new LogEvent(
                            Date: el.Element("Datum")?.Value ?? "Unknown Date",
                            Title: $"New PlugIn was imported: {el.Element("Info")?.Value ?? "N/A"}"
                        ));
                    events.AddRange(pluginEvents);
                }
                details.Events = events.OrderByDescending(e => e.Date).ToList();
            }
            catch { details.Events = []; } // Assign an empty list on error
            
            return details;
        });
    }
}