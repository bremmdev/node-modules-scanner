#:package CommandLineParser@2.9.1

#pragma warning disable IDE0005
using CommandLine;
using System.IO;
#pragma warning restore IDE0005

Parser.Default.ParseArguments<CLIArguments>(args).WithParsed(options =>
{
    try
    {
        CLIArgumentsValidator.Validate(options);
        List<string> startingDirectories = DirectoryScanner.GetStartingDirectory(options.RootPath);
        foreach (string directory in startingDirectories)
        {
            Console.WriteLine(directory);
        }

    }
    catch (ArgumentException e)
    {
        Console.Error.WriteLine($"Error: {e.Message}");
    }
});

class DirectoryScanner
{
    internal static List<string> GetStartingDirectory(string startingDirectory)
    {
        // If we have a valid directory, use it, otherwise we get all drives
        if (!String.IsNullOrWhiteSpace(startingDirectory)) return [startingDirectory];

        // There is no common root in Windows, each drive is their own root, so we need to go through all drives
        // We also need to check if the drive is ready (e.g. CD-ROM or a USB drive), otherwise it will throw an exception
        return DriveInfo.GetDrives().Where(d => d.IsReady).Select(d => d.Name).ToList();
    }
}

class CLIArgumentsValidator
{
    internal static void Validate(CLIArguments arguments)
    {
        int modifiedAgo = arguments.ModifiedAgo ?? 0;
        string rootPath = arguments.RootPath;

        if (modifiedAgo < 0)
        {
            throw new ArgumentException("Modified age must be a non-negative number");
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

    [Option('m', "modified-ago", HelpText = "Modified age in days (int)")]
    public int? ModifiedAgo { get; set; }
}