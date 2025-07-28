namespace WorkspaceConsole;

internal class Program
{
    private static readonly string rootPath = "C:\\Users\\lcvetic\\Documents\\Workspace";

    //Main Void
    private static void Main(string[] args)
    {
        var totalSize = getDisksize(rootPath); // make sure it's declared first

        var allprjPaths = getAllPrjPaths(rootPath);
        foreach (var prjPath in allprjPaths)
        {
            var size = getDisksize(prjPath);
            var output = getOutput(size);
            var percentage = getPercentage(size, totalSize);

            Console.WriteLine($"{prjPath}: {output} ({percentage:0.##}%)");

            var ustPaths = getAllUstPaths(prjPath);
            foreach (var ustPath in ustPaths)
            {
                var ustSize = getDisksize(ustPath);
                var ustOutput = getOutput(ustSize);
                var ustPercentage = getPercentage(ustSize, size); // relative to project

                Console.WriteLine($"\t{ustPath}: {ustOutput} ({ustPercentage:0.##}%)");
            }
        }
    }

    //Directory Path Ausgabe
    private static double getDisksize(string path)
    {
        var di = new DirectoryInfo(path);
        return di.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
    }

    //Werten Bennenung
    private static string getOutput(double size)
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
    private static bool isPorject(string path)
    {
        var filepath = $"{path}\\prj.xml";
        return File.Exists(filepath);
    }

    //Projekt Files anrufer
    private static List<string> getAllPrjPaths(string path)
    {
        var prjFiles = new List<string>();

        foreach (var dir in Directory.EnumerateDirectories(path))
        {
            if (isPorject(dir)) prjFiles.Add(dir);
            prjFiles.AddRange(getAllPrjPaths(dir));
        }

        return prjFiles;
    }

    private static bool isUstCategory(string path)
    {
        var filepath = Path.Combine(path, "ust.xml");
        return File.Exists(filepath);
    }

    private static List<string> getAllUstPaths(string prjPath)
    {
        var ustPaths = new List<string>();

        foreach (var dir in Directory.EnumerateDirectories(prjPath))
            if (isUstCategory(dir))
                ustPaths.Add(dir);

        return ustPaths;
    }

    private static double getPercentage(double partSize, double totalSize)
    {
        if (totalSize == 0) return 0;
        return partSize / totalSize * 100.0;
    }
}