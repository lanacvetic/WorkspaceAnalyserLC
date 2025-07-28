namespace WorkspaceAnalyser.Models;

public class ControllerDetails
{
    public string Path { get; set; } 
    public string AnzahlMakros { get; set; }
    public string AnzahlFUPBlaetter { get; set; }
    public string Hardwaretyp { get; set; }
    public string CPVersion { get; set; }
    public string IPAdresse { get; set; }
    public bool Uebersetzt { get; set; }
    public string Uebersetzungstext { get; set; }
}