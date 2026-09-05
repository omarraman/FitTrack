if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: SourceArchiver [source-directory]");
    return 2;
}

var sourceDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : FindSolutionRoot();

if (!Directory.Exists(sourceDirectory))
{
    Console.Error.WriteLine($"Source directory does not exist: {sourceDirectory}");
    return 1;
}

var documentsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
if (string.IsNullOrWhiteSpace(documentsDirectory))
{
    Console.Error.WriteLine("Could not determine the user's Documents directory.");
    return 1;
}

var archiveDirectory = Path.Combine(documentsDirectory, "codearchives");
var destinationDirectory = Path.Combine(
    archiveDirectory,
    $"{new DirectoryInfo(sourceDirectory).Name}-{DateTime.Now:yyyyMMdd-HHmmss}");

Directory.CreateDirectory(destinationDirectory);

var sourceFileCount = CopyDirectory(sourceDirectory, destinationDirectory);

Console.WriteLine(
    $"Copied {sourceDirectory} to {destinationDirectory} with {sourceFileCount} file(s).");
return 0;

static string FindSolutionRoot()
{
    var candidates = new[]
    {
        new DirectoryInfo(Directory.GetCurrentDirectory()),
        new DirectoryInfo(AppContext.BaseDirectory)
    };

    foreach (var candidate in candidates)
    {
        for (var directory = candidate; directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.sln").Any())
                return directory.FullName;
        }
    }

    throw new InvalidOperationException(
        "Could not find a solution directory. Pass the source directory as an argument.");
}

static int CopyDirectory(
    string sourceDirectory,
    string destinationDirectory,
    Func<string, bool>? fileFilter = null)
{
    var sourceFiles = Directory
        .EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
        .Where(file =>
            !HasExcludedDirectory(file, sourceDirectory) &&
            (fileFilter is null ? IsIncludedFile(file) : fileFilter(file)))
        .ToArray();

    foreach (var sourceFile in sourceFiles)
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
        var destinationFile = Path.Combine(destinationDirectory, relativePath + ".txt");
        var destinationFileDirectory = Path.GetDirectoryName(destinationFile);

        if (destinationFileDirectory is not null)
            Directory.CreateDirectory(destinationFileDirectory);

        File.Copy(sourceFile, destinationFile, overwrite: true);
    }

    return sourceFiles.Length;
}

static bool IsIncludedFile(string filePath)
{
    var extension = Path.GetExtension(filePath);

    return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".config", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
        extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(filePath).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(filePath).Equals(".editorconfig", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(filePath).Equals(".gitignore", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(filePath).Equals(".dockerignore", StringComparison.OrdinalIgnoreCase);
}

static bool HasExcludedDirectory(string filePath, string sourceDirectory)
{
    var relativePath = Path.GetRelativePath(sourceDirectory, filePath);
    var directorySegments = relativePath
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    return directorySegments
        .Take(directorySegments.Length - 1)
        .Any(segment =>
            segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("debug", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("release", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("backups", StringComparison.OrdinalIgnoreCase));
}
