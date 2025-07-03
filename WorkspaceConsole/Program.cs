using System.Globalization;
using System.Text.Json;

namespace WorkspaceConsole;

class Program
{
    private static string rootPath = "C:\\Users\\lcvetic\\Documents\\Workspace";

    //Main Void
    static void Main(string[] args)
    {
        double totalSize = getDisksize(rootPath); // make sure it's declared first

        var allprjPaths = getAllPrjPaths(rootPath);
        foreach (var prjPath in allprjPaths)
        {
            double size = getDisksize(prjPath);
            string output = getOutput(size);
            double percentage = getPercentage(size, totalSize);

            Console.WriteLine($"{prjPath}: {output} ({percentage:0.##}%)");
            
            var ustPaths = getAllUstPaths(prjPath);
            foreach (var ustPath in ustPaths)
            {
                double ustSize = getDisksize(ustPath);
                string ustOutput = getOutput(ustSize);
                double ustPercentage = getPercentage(ustSize, size); // relative to project

                Console.WriteLine($"\t{ustPath}: {ustOutput} ({ustPercentage:0.##}%)");
            }
        }
    }
    //Directory Path Ausgabe
    static double getDisksize(string path)
    {
        DirectoryInfo di = new DirectoryInfo(path);
        return di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
    }

    //Werten Bennenung
    static string getOutput(double size)
    {
        const double KB = 1024;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        return size switch
        {
            >= GB => $"{size / GB:0.##} GB",
            >= MB => $"{size / MB:0.##} MB",
            >= KB => $"{size / KB:0.##} KB",
            _ => $"{size} B"
        };
    }

    //Projekt Finder
    static bool isPorject(string path)
    {
        var filepath = $"{path}\\prj.xml";
        return File.Exists(filepath);
    }

    //Projekt Files anrufer
    static List<string> getAllPrjPaths(string path)
    {
         List<string> prjFiles = new List<string>();
         
        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            if (isPorject(dir))
            {
                prjFiles.Add(dir);
            }
            prjFiles.AddRange(getAllPrjPaths(dir));
        }
        
        return prjFiles;
    }
    
    static bool isUstCategory(string path)
    {
        var filepath = Path.Combine(path, "ust.xml");
        return File.Exists(filepath);
    }
    
    static List<string> getAllUstPaths(string prjPath)
    {
        List<string> ustPaths = new List<string>();

        foreach (var dir in Directory.EnumerateDirectories(prjPath))
        {
            if (isUstCategory(dir))
            {
                ustPaths.Add(dir);
            }
        }

        return ustPaths;
    }
    
    static double getPercentage(double partSize, double totalSize)
    {
        if (totalSize == 0) return 0;
        return (partSize / totalSize) * 100.0;
    }
    
    
}