#:package CommandLineParser@2.9.1
#:package Spectre.Console@0.54.1-alpha.0.86

#pragma warning disable IDE0005
using CommandLine;
using System.IO;
using Spectre.Console;
#pragma warning restore IDE0005

Parser.Default.ParseArguments<CLIArguments>(args).WithParsed(options =>
{
    try
    {
        CLIArgumentsValidator.Validate(options);
        IReadOnlyList<string> startingDirectories = DirectoryScanner.GetStartingDirectory(options.RootPath);
        HashSet<string> excludedFolders = new(options.Exclude, StringComparer.OrdinalIgnoreCase);
        (IEnumerable<NodeModulesFolder> nodeModulesFolders, int directoryCount) = DirectoryScanner.ScanDirectories(startingDirectories, excludedFolders, options.MinModifiedAgo ?? 0);

        // Order by last modified and filter out empty folders
        IReadOnlyList<NodeModulesFolder> filteredNodeModulesFolders = nodeModulesFolders
        .OrderBy(f => f.LastModified)
        .Where(f => Math.Round(f.SizeInMb) > 0)
        .ToList();
        ConsoleOutput.Output(filteredNodeModulesFolders, directoryCount);
    }
    catch (ArgumentException e)
    {
        AnsiConsole.MarkupLineInterpolated($"[red]✗[/] Error: {e.Message}");
    }
});

class DirectoryScanner
{
    internal static IReadOnlyList<string> GetStartingDirectory(string startingDirectory)
    {
        // If we have a valid directory, use it, otherwise we get all drives
        if (!String.IsNullOrWhiteSpace(startingDirectory)) return [startingDirectory];

        // There is no common root in Windows, each drive is their own root, so we need to go through all drives
        // We also need to check if the drive is ready (e.g. CD-ROM or a USB drive), otherwise it will throw an exception
        return DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).ToList();
    }

    internal static (IReadOnlyList<NodeModulesFolder>, int) ScanDirectories(IEnumerable<string> startingDirectories, HashSet<string> excludedFolders, int minModifiedAgo)
    {
        List<NodeModulesFolder> nodeModulesFolders = new List<NodeModulesFolder>();
        int directoryCount = 0;

        // Explicitly walk the directory tree instead of using recursion to avoid stack overflows for deep directory trees
        // Recursive methods work fine for shallow trees, but directory trees on a full drive can be hundreds of levels deep in theory, and recursive calls consume stack frames
        foreach (string startingDirectory in startingDirectories)
        {
            var stack = new Stack<string>();
            stack.Push(startingDirectory);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                string[] subDirs;
                try
                {
                    subDirs = Directory.GetDirectories(current);
                }
                catch (UnauthorizedAccessException) { continue; } // skip protected folders
                catch (IOException) { continue; }

                foreach (string subDir in subDirs)
                {
                    string folderName = Path.GetFileName(subDir);

                    if (excludedFolders.Contains(folderName)) continue;

                    if (folderName == "node_modules")
                    {
                        // Found one — collect it, don't recurse into it
                        var dirInfo = new DirectoryInfo(subDir);
                        var lastModified = dirInfo.GetFileSystemInfos()
                            .Select(f => f.LastWriteTime)
                            .DefaultIfEmpty(dirInfo.LastWriteTime) // If the directory is empty, use the last write time of the directory
                            .Max();

                        if (lastModified > DateTime.Now.AddDays(-minModifiedAgo)) continue;

                        nodeModulesFolders.Add(new NodeModulesFolder(subDir, GetDirectorySize(dirInfo), lastModified));
                    }
                    else
                    {
                        directoryCount++;
                        stack.Push(subDir); // keep searching deeper
                    }
                }
            }
        }
        return (nodeModulesFolders, directoryCount);
    }

    // Skips symlinks and junction points to avoid counting the same physical files multiple times.
    // This is especially important for pnpm, which symlinks packages from a .pnpm store into node_modules.
    private static long GetDirectorySize(DirectoryInfo rootDir)
    {
        long size = 0;
        var stack = new Stack<DirectoryInfo>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            DirectoryInfo dir = stack.Pop();
            try
            {
                foreach (var file in dir.EnumerateFiles())
                {
                    if (!file.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        size += file.Length;
                }
                foreach (var subDir in dir.EnumerateDirectories())
                {
                    if (!subDir.Attributes.HasFlag(FileAttributes.ReparsePoint))
                        stack.Push(subDir);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        return size;
    }
}

class ConsoleOutput
{
    internal static void Output(IReadOnlyList<NodeModulesFolder> nodeModulesFolders, int directoryCount)
    {
        var table = new Table().AddColumn("Path").AddColumn("Size").AddColumn("Last Modified");

        foreach (var folder in nodeModulesFolders)
        {
            var lastModified = folder.LastModified.ToString("yyyy-MM-dd");
            var rowColor = folder.AgeInDays > 180 ? Color.Red
                : folder.AgeInDays > 90 ? Color.Yellow
                : Color.Green;

            var style = new Style(foreground: rowColor);

            table.AddRow(
                new Text(folder.Path, style),
                new Text($"{Math.Round(folder.SizeInMb).ToString()} MB", style),
                new Text(lastModified, style)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[bold]Legend[/]\n" +
            "[green]Green[/]: modified within the last 90 days\n" +
            "[yellow]Yellow[/]: modified 91 to 180 days ago\n" +
            "[red]Red[/]: modified more than 180 days ago"
        );
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Total node_module size is {(int)nodeModulesFolders.Sum(n => n.SizeInMb)} MB");
        AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Total directories scanned: {directoryCount}");
    }
}

class CLIArgumentsValidator
{
    internal static void Validate(CLIArguments arguments)
    {
        int minModifiedAgo = arguments.MinModifiedAgo ?? 0;
        string rootPath = arguments.RootPath;

        if (minModifiedAgo < 0)
        {
            throw new ArgumentException("Minimum modified age must be a non-negative number of days");
        }

        if (File.Exists(rootPath))
        {
            throw new ArgumentException("Starting directory should be a directory, not a file");
        }

        if (!String.IsNullOrWhiteSpace(rootPath) && !Directory.Exists(rootPath))
        {
            throw new ArgumentException("Directory does not exist");
        }
    }
}

class CLIArguments
{
    [Value(0, MetaName = "root-path", HelpText = "Starting directory for the search")]
    public string RootPath { get; set; } = string.Empty;

    [Option('m', "min-modified-ago", HelpText = "Only include directories modified longer than X days ago (int)")]
    public int? MinModifiedAgo { get; set; }

    [Option('e', "exclude", Separator = ',', HelpText = "Comma-separated list of directory names to exclude (e.g. --exclude a,b,c)")]
    public IEnumerable<string> Exclude { get; set; } = [];
}

internal record NodeModulesFolder(string Path, long SizeBytes, DateTime LastModified)
{
    public int AgeInDays => (DateTime.Now - LastModified).Days;
    public double SizeInMb => SizeBytes / 1024.0 / 1024.0; // Actual size, not the size it takes up on disk (which is larger)
}