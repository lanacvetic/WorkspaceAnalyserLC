using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace WpfApp;

/// <summary>
/// Interaktionslogik für MainWindow.xaml.
/// Diese Klasse verwaltet die Hauptlogik der WPF-Anwendung zur Analyse von Plattengrößen.
/// </summary>
public partial class MainWindow : Window
{
    // Basis-Pfad für die Analyse. Beachten Sie, dass dieser statisch ist und im Konstruktor nicht direkt verwendet wird,
    // stattdessen wird der Pfad aus dem 'RootPathTextBox' der UI gelesen. Der Kommentar wurde entsprechend aktualisiert.
    private static readonly string rootPath = "C:\\Users\\lcvetic\\Documents\\Workspace";

    /// <summary>
    /// Konstruktor der MainWindow-Klasse.
    /// Initialisiert die UI-Komponenten und abonniert den Klick-Event des Start-Buttons.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent(); // Initialisiert die in XAML definierten WPF-Komponenten.
        // Abonniert den Klick-Event des 'StartAnalysisButton' mit der Methode 'StartAnalysis_Click'.
        StartAnalysisButton.Click += StartAnalysis_Click;
    }
    private void RootPathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            StartAnalysis_Click(StartAnalysisButton, new RoutedEventArgs());
        }
    }
    /// <summary>
    /// Event-Handler für den "Analyse starten"-Button.
    /// Führt die Plattengrößenanalyse basierend auf dem eingegebenen Pfad aus und zeigt die Ergebnisse an.
    /// </summary>
    private void StartAnalysis_Click(object sender, RoutedEventArgs e)
    {
        // Löscht alle vorhandenen UI-Elemente aus dem ProjectsPanel, um die Anzeige zu aktualisieren.
        ProjectsPanel.Children.Clear();

        // Holt den eingegebenen Pfad aus der Textbox und entfernt führende/nachfolgende Leerzeichen.
        string inputPath = RootPathTextBox.Text.Trim();

        // Überprüft, ob der eingegebene Pfad existiert. Wenn nicht, wird eine Fehlermeldung angezeigt.
        if (!Directory.Exists(inputPath))
        {
            MessageBox.Show("Der eingegebene Pfad existiert nicht!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            return; // Beendet die Methode, wenn der Pfad ungültig ist.
        }

        // Überprüft, ob der eingegebene Pfad selbst ein Projekt ist (enthält prj.xml).
        if (IsProject(inputPath))
        {
            // Wenn der Input-Pfad ein Projekt ist, wird nur dieses eine Projekt angezeigt.
            DisplaySingleProject(inputPath, inputPath);
        }
        else
        {
            // Wenn der Input-Pfad kein Projekt ist, wird versucht, das nächste Projekt-Wurzelverzeichnis zu finden.
            string prjRoot = FindNearestProjectRoot(inputPath);

            // Überprüft, ob ein Projekt-Wurzelverzeichnis gefunden wurde und es nicht der Input-Pfad selbst ist.
            if (prjRoot != null && prjRoot != inputPath)
            {
                // Wenn wir uns tief innerhalb eines einzelnen Projekts befinden, zeige dieses Projekt an.
                DisplaySingleProject(prjRoot, inputPath);
            }
            else
            {
                // Wenn wir auf einer Ebene sind, die mehrere mögliche Projektordner enthält (oder gar keine).
                // Holt alle Projektpfade, die im eingegebenen Pfad oder seinen Unterverzeichnissen gefunden wurden.
                var allPrjPaths = GetAllProjectPaths(inputPath);

                // Wenn keine Projekte gefunden wurden, zeige eine Warnmeldung an.
                if (allPrjPaths.Count == 0)
                {
                    MessageBox.Show("Kein gültiges Projekt (prj.xml) im Pfad gefunden!", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // Beendet die Methode.
                }

                // Durchläuft alle gefundenen Projektpfade und zeigt jedes einzelne Projekt an.
                foreach (var prj in allPrjPaths)
                {
                    DisplaySingleProject(prj, prj);
                }
            }
        }
    }

    /// <summary>
    /// Zeigt die Details eines einzelnen Projekts und seiner UST-Kategorien in der Benutzeroberfläche an.
    /// </summary>
    /// <param name="prjRoot">Der Wurzelpfad des Projekts (dort wo prj.xml liegt).</param>
    /// <param name="focusPath">Der Pfad, der vom Benutzer eingegeben wurde und auf den die USTs gefiltert werden sollen.</param>
    private void DisplaySingleProject(string prjRoot, string focusPath)
    {
        // Berechnet die Gesamtgröße des Projekts.
        double prjSize = GetDiskSize(prjRoot);
        // Formatiert die Projektgröße für die Anzeige (z.B. in MB oder GB).
        string prjOutput = FormatSize(prjSize);

        // Erstellt ein vertikales StackPanel, um die UI-Elemente für das aktuelle Projekt zu organisieren.
        var prjPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        // Fügt einen TextBlock hinzu, der den Pfad und die Gesamtgröße des Projekts anzeigt.
        prjPanel.Children.Add(new TextBlock
        {
            Text = $"{prjRoot}: {prjOutput}",
            FontWeight = FontWeights.Bold, // Setzt den Text auf fett.
            TextWrapping = TextWrapping.Wrap, // Ermöglicht den Zeilenumbruch.
            Foreground = Brushes.Black // Setzt die Textfarbe auf Schwarz.
        });

        // Holt alle UST-Pfade im gesamten Unterbaum des Projekts.
        var allUsts = GetAllUstPathsUnderSubtree(prjRoot);
        // Filtert die UST-Pfade basierend auf dem 'focusPath'.
        // Es werden nur USTs angezeigt, die den Fokuspfad enthalten oder vom Fokuspfad enthalten sind.
        var ustPaths = allUsts
            .Where(p => focusPath.StartsWith(p, StringComparison.OrdinalIgnoreCase) || 
                        p.StartsWith(focusPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Bestimmt die Sortierreihenfolge (aufsteigend oder absteigend) aus der ComboBox.
        string selectedSort = (SortOrderCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
        bool sortAscending = selectedSort == "Kleinste zuerst";

        // Sortiert die gefundenen UST-Pfade basierend auf ihrer Größe und der ausgewählten Reihenfolge.
        ustPaths = sortAscending
            ? ustPaths.OrderBy(p => GetDiskSize(p)).ToList() // Sortiert aufsteigend nach Größe.
            : ustPaths.OrderByDescending(p => GetDiskSize(p)).ToList(); // Sortiert absteigend nach Größe.

        // Überprüft, ob UST-Kategorien gefunden wurden, bevor UI-Elemente dafür erstellt werden.
        if (ustPaths.Count > 0)
        {
            // Durchläuft jede gefilterte und sortierte UST-Kategorie.
            foreach (var ustPath in ustPaths)
            {
                // Berechnet die Größe der aktuellen UST-Kategorie.
                double ustSize = GetDiskSize(ustPath);
                // Berechnet den prozentualen Anteil der UST-Kategorie an der Gesamtgröße des Projekts.
                double ustPercent = GetPercentage(ustSize, prjSize);
                // Formatiert die UST-Größe für die Anzeige.
                string ustOutput = FormatSize(ustSize);

                // Bestimmt die Textfarbe basierend auf dem prozentualen Anteil der UST-Kategorie.
                // Grün für < 33%, Orange für 33-66%, Rot für > 66%.
                Brush ustColor = ustPercent switch
                {
                    < 33 => Brushes.Green,
                    < 66 => Brushes.Orange,
                    _ => Brushes.Red
                };

                // Fügt einen TextBlock für jede UST-Kategorie zum Projekt-Panel hinzu.
                prjPanel.Children.Add(new TextBlock
                {
                    Text = $"{ustPath}: {ustOutput} ({ustPercent:0.##}%)",
                    Margin = new Thickness(10, 2, 0, 2), // Setzt einen kleinen linken Abstand für Einrückung.
                    Foreground = ustColor, // Setzt die Farbe des TextBlocks.
                    FontStyle = FontStyles.Italic // Setzt den Text auf kursiv.
                });
            }
        }
        else
        {
            // Wenn keine UST-Kategorien gefunden wurden, fügt einen entsprechenden Hinweistext hinzu.
            prjPanel.Children.Add(new TextBlock
            {
                Text = "Keine ust.xml Ordner unter diesem Pfad gefunden.",
                Margin = new Thickness(10, 5, 0, 5),
                Foreground = Brushes.Gray // Setzt die Textfarbe auf Grau.
            });
        }

        // Fügt das gesamte Projekt-Panel dem Haupt-Panel in der UI hinzu.
        ProjectsPanel.Children.Add(prjPanel);
    }

    /// <summary>
    /// Findet rekursiv alle UST-Kategoriepfade unterhalb eines gegebenen Wurzelverzeichnisses.
    /// Eine UST-Kategorie wird durch das Vorhandensein einer 'ust.xml' Datei im Ordner identifiziert.
    /// </summary>
    /// <param name="root">Der Pfad, ab dem die Suche gestartet werden soll.</param>
    /// <returns>Eine Liste von Strings, die die Pfade zu allen gefundenen UST-Kategorien enthalten.</returns>
    private static List<string> GetAllUstPathsUnderSubtree(string root)
    {
        var list = new List<string>(); // Initialisiert eine leere Liste zum Speichern der UST-Pfade.

        try
        {
            // Durchläuft alle direkten Unterverzeichnisse des aktuellen Root-Pfades.
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                // Überprüft, ob das aktuelle Verzeichnis eine UST-Kategorie ist.
                if (IsUstCategory(dir))
                    list.Add(dir); // Fügt den Pfad hinzu, wenn es eine UST-Kategorie ist.

                // Rekursiver Aufruf, um auch die Unterverzeichnisse der Unterverzeichnisse zu durchsuchen.
                list.AddRange(GetAllUstPathsUnderSubtree(dir));
            }
        }
        catch (Exception ex)
        {
            // Eine leere Catch-Anweisung ist hier vorhanden, um Fehler wie fehlende Berechtigungen
            // beim Zugriff auf Verzeichnisse zu ignorieren und die Anwendung nicht abstürzen zu lassen.
            // Für eine robustere Anwendung wäre hier eine Fehlerprotokollierung empfehlenswert.
            Console.WriteLine($"Fehler beim Zugriff auf Verzeichnis '{root}': {ex.Message}");
        }

        return list; // Gibt die Liste der gefundenen UST-Pfade zurück.
    }

    /// <summary>
    /// Findet den nächstgelegenen übergeordneten Projekt-Wurzelpfad relativ zu einem gegebenen Pfad.
    /// Ein Projekt-Wurzelpfad ist ein Verzeichnis, das eine 'prj.xml' Datei enthält.
    /// </summary>
    /// <param name="path">Der Startpfad, ab dem aufwärts gesucht werden soll.</param>
    /// <returns>Den vollständigen Pfad des nächstgelegenen Projekt-Wurzelverzeichnisses, oder null, wenn keines gefunden wird.</returns>
    static string FindNearestProjectRoot(string path)
    {
        var dir = new DirectoryInfo(path); // Erstellt ein DirectoryInfo-Objekt für den Startpfad.

        // Solange das aktuelle Verzeichnis existiert (nicht null ist).
        while (dir != null)
        {
            // Überprüft, ob das aktuelle Verzeichnis ein Projekt ist.
            if (IsProject(dir.FullName))
                return dir.FullName; // Wenn ja, gib den vollständigen Pfad zurück.

            dir = dir.Parent; // Gehe zum übergeordneten Verzeichnis und wiederhole die Schleife.
        }

        return null; // Wenn kein Projekt-Wurzelverzeichnis gefunden wurde, gib null zurück.
    }

    /// <summary>
    /// Berechnet die Gesamtgröße aller Dateien in einem Verzeichnis (inkl. Unterverzeichnisse).
    /// </summary>
    /// <param name="path">Pfad des zu analysierenden Verzeichnisses.</param>
    /// <returns>Gesamtgröße in Bytes als double, oder 0 bei Fehlern (z.B. Zugriff verweigert).</returns>
    static double GetDiskSize(string path)
    {
        try
        {
            // Erstellt ein DirectoryInfo-Objekt für den angegebenen Pfad.
            return new DirectoryInfo(path)
                // Zählt alle Dateien im Verzeichnis und seinen Unterverzeichnissen auf.
                .EnumerateFiles("*", SearchOption.AllDirectories)
                // Summiert die Längen (Größen in Bytes) aller gefundenen Dateien.
                .Sum(fi => (double)fi.Length);
        }
        catch
        {
            // Eine leere Catch-Anweisung ist hier vorhanden, um Fehler wie fehlende Berechtigungen
            // beim Zugriff auf Verzeichnisse zu ignorieren und 0 zurückzugeben.
            // Für eine robustere Anwendung wäre hier eine Fehlerprotokollierung empfehlenswert.
            return 0;
        }
    }

    /// <summary>
    /// Formatiert eine Byte-Größe in lesbare Einheiten (B, KB, MB, GB).
    /// </summary>
    /// <param name="size">Größe in Bytes (double).</param>
    /// <returns>Formatierter String (z.B. "1.5 MB").</returns>
    static string FormatSize(double size)
    {
        // Definiert die Konstanten für die Umrechnung von Bytes in größere Einheiten.
        const double KB = 1024;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        // Nutzt ein Switch-Expression, um die Größe in die passende Einheit umzurechnen und zu formatieren.
        return size switch
        {
            >= GB => $"{size / GB:0.##} GB", // Wenn Größe >= 1 GB, formatiere als GB (mit zwei Nachkommastellen).
            >= MB => $"{size / MB:0.##} MB", // Wenn Größe >= 1 MB, formatiere als MB.
            >= KB => $"{size / KB:0.##} KB", // Wenn Größe >= 1 KB, formatiere als KB.
            _ => $"{size} B" // Standardfall: Größe bleibt in Bytes.
        };
    }

    /// <summary>
    /// Berechnet den prozentualen Anteil einer Teilgröße an einer Gesamtgröße.
    /// </summary>
    /// <param name="partSize">Die Größe des Teils (double).</param>
    /// <param name="totalSize">Die Gesamtgröße (double).</param>
    /// <returns>Der Prozentsatz (Wert von 0 bis 100), oder 0, wenn die Gesamtgröße 0 ist (um Division durch Null zu vermeiden).</returns>
    static double GetPercentage(double partSize, double totalSize)
    {
        // Vermeidet eine Division durch Null. Wenn die Gesamtgröße 0 ist, ist der Prozentsatz ebenfalls 0.
        return totalSize == 0 ? 0 : (partSize / totalSize) * 100.0;
    }

    /// <summary>
    /// Prüft, ob ein Verzeichnis ein Projekt ist.
    /// Ein Verzeichnis gilt als Projekt, wenn es die Datei 'prj.xml' enthält.
    /// </summary>
    /// <param name="path">Pfad des zu prüfenden Verzeichnisses.</param>
    /// <returns>True, wenn 'prj.xml' im angegebenen Pfad existiert, sonst False.</returns>
    static bool IsProject(string path) =>
        File.Exists(Path.Combine(path, "prj.xml")); // Prüft, ob die Datei 'prj.xml' im angegebenen Pfad existiert.

    /// <summary>
    /// Prüft, ob ein Verzeichnis eine UST-Kategorie ist.
    /// Eine UST-Kategorie ist ein Ordner, der die Datei 'ust.xml' enthält.
    /// </summary>
    /// <param name="path">Pfad des zu prüfenden Verzeichnisses.</param>
    /// <returns>True, wenn 'ust.xml' im angegebenen Pfad existiert, sonst False.</returns>
    static bool IsUstCategory(string path) =>
        File.Exists(Path.Combine(path, "ust.xml")); // Prüft, ob die Datei 'ust.xml' im angegebenen Pfad existiert.

    /// <summary>
    /// Findet alle Projektpfade, indem es rekursiv alle Unterverzeichnisse ab einem Startpfad durchsucht.
    /// Ein Verzeichnis wird als Projekt erkannt, wenn es die Datei 'prj.xml' enthält.
    /// </summary>
    /// <param name="path">Der Startpfad für die Suche nach Projekten.</param>
    /// <returns>Eine Liste von Strings, die die vollständigen Pfade zu allen gefundenen Projekten enthalten.</returns>
    static List<string> GetAllProjectPaths(string path)
    {
        var list = new List<string>(); // Initialisiert eine leere Liste zum Speichern der Projektpfade.

        try
        {
            // Durchläuft alle direkten Unterverzeichnisse des aktuellen Pfads.
            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                // Überprüft, ob das aktuelle Verzeichnis ein Projekt ist.
                if (IsProject(dir)) list.Add(dir); // Fügt den Pfad hinzu, wenn es ein Projekt ist.
                // Rekursiver Aufruf, um auch die Unterverzeichnisse der Unterverzeichnisse zu durchsuchen.
                list.AddRange(GetAllProjectPaths(dir));
            }
        }
        catch (Exception ex)
        {
            // Eine leere Catch-Anweisung ist hier vorhanden, um Fehler wie fehlende Berechtigungen
            // beim Zugriff auf Verzeichnisse zu ignorieren und die Anwendung nicht abstürzen zu lassen.
            // Für eine robustere Anwendung wäre hier eine Fehlerprotokollierung empfehlenswert.
            Console.WriteLine($"Fehler beim Zugriff auf Verzeichnis '{path}': {ex.Message}");
        }

        return list; // Gibt die Liste der gefundenen Projektpfade zurück.
    }
            //Wann man ENter druckt der triggert das Analysis Starten

}