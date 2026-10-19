using System.CommandLine;

var rootCommand = new RootCommand("rootCommand for file bundler CLI");


var languageOption = new Option<string[]>("--language", "-l")
{
    Description = "Bundles code files of the selected languages or all languages",
    Required = true,
    AllowMultipleArgumentsPerToken = true
};

var outputOption = new Option<FileInfo>("--output", "-o")
{
    Description = "File path and name for the bundle file",
    Required = true
};
var noteOption = new Option<bool>("--note", "-n")
{
    Description = "Add the source file path as a comment before each file",
};
var removeEmptyLinesOption = new Option<bool>("--remove-empty-lines", "-rel")
{
    Description = "Remove empty lines from the code before bundling"
};
var sortOption = new Option<string>("--sort", "-s")
{
    Description = "Sort files by 'name' (default) or by 'type'",
    DefaultValueFactory = _ => "name"
};
var authorOption = new Option<string>("--author", "-a")
{
    Description = "Name of the bundle file's author"
};
sortOption.AcceptOnlyFromAmong("name", "type");
var bundleCommand = new Command("bundle", "Bundles code files into a single file");
var createRspCommand = new Command("create-rsp", "Create a response file with a ready bundle command");

var languageExtensions = new Dictionary<string, string>
{
    ["csharp"] = ".cs",
    ["html"] = ".html",
    ["css"] = ".css",
    ["javascript"] = ".js",
    ["typescript"] = ".ts",
    ["sql"] = ".sql",
    ["python"] = ".py",
    ["java"] = ".java",
    ["cpp"] = ".cpp",
    ["c"] = ".c",
    ["json"] = ".json"
};
createRspCommand.SetAction(parseResult =>
{
    string languagesAnswer;
    while (true)
    {
        Console.Write("Which languages? (separated by spaces, or 'all'): ");
        languagesAnswer = Console.ReadLine() ?? "";

        var chosen = languagesAnswer.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (chosen.Length == 0)
        {
            Console.WriteLine("Please enter at least one language.");
        }
        else if (chosen.All(l => l == "all" || languageExtensions.ContainsKey(l)))
        {
            break;
        }
        else
        {
            Console.WriteLine($"Unknown language. Options: all, {string.Join(", ", languageExtensions.Keys)}");
        }
    }

    string outputAnswer;
    while (true)
    {
        Console.Write("Output file name or path: ");
        outputAnswer = Console.ReadLine() ?? "";

        if (!string.IsNullOrWhiteSpace(outputAnswer))
        {
            break;
        }
        Console.WriteLine("Output file name is required.");
    }

    Console.Write("Add source notes? (y/n): ");
    var noteAnswer = Console.ReadLine() == "y";

    string sortAnswer;
    while (true)
    {
        Console.Write("Sort by 'name' or 'type' (press Enter for name): ");
        sortAnswer = Console.ReadLine() ?? "";

        if (sortAnswer == "" || sortAnswer == "name" || sortAnswer == "type")
        {
            break;
        }
        Console.WriteLine("Please enter 'name', 'type', or press Enter.");
    }

    Console.Write("Remove empty lines? (y/n): ");
    var removeEmptyLinesAnswer = Console.ReadLine() == "y";

    Console.Write("Author name (press Enter to skip): ");
    var authorAnswer = Console.ReadLine();

    Console.Write("Response file name: ");
    var rspFileName = Console.ReadLine();
    var parts = new List<string> { "bundle" };

    parts.Add("--language");
    parts.Add(languagesAnswer);

    parts.Add("--output");
    parts.Add($"\"{outputAnswer}\"");

    if (noteAnswer)
    {
        parts.Add("--note");
    }

    if (!string.IsNullOrWhiteSpace(sortAnswer))
    {
        parts.Add("--sort");
        parts.Add(sortAnswer);
    }

    if (removeEmptyLinesAnswer)
    {
        parts.Add("--remove-empty-lines");
    }

    if (!string.IsNullOrWhiteSpace(authorAnswer))
    {
        parts.Add("--author");
        parts.Add($"\"{authorAnswer}\"");
    }

    var fullCommand = string.Join(" ", parts);

    var rspPath = rspFileName + ".rsp";
    File.WriteAllText(rspPath, fullCommand);

    Console.WriteLine($"Created {rspPath}");
    Console.WriteLine($"Run it with: ss @{rspPath}");
});

bundleCommand.SetAction(parseResult =>
{
    var currentDirectory = Directory.GetCurrentDirectory();
    var files = Directory.GetFiles(currentDirectory, "*.*", SearchOption.AllDirectories);
    var languages = parseResult.GetValue(languageOption) ?? Array.Empty<string>();
    var extensions = new List<string>();
    if (languages.Contains("all"))
    {
        extensions.AddRange(languageExtensions.Values);
    }
    else
    {
        foreach (var language in languages)
        {
            if (languageExtensions.ContainsKey(language))
            {
                extensions.Add(languageExtensions[language]);
            }
            else
            {
                Console.WriteLine($"Unknown language: {language}");
            }
        }
    }
    var excludedFolders = new[] { "bin", "obj", "debug", ".vs" };
    var selectedFiles = new List<string>();
    foreach (var file in files)
    {
        var extension = Path.GetExtension(file);
        var folders = file.ToLower().Split(Path.DirectorySeparatorChar);

        if (extensions.Contains(extension) && !folders.Any(f => excludedFolders.Contains(f)))
        {
            selectedFiles.Add(file);
        }
    }
    if (selectedFiles.Count == 0)
    {
        Console.WriteLine("No matching code files were found in this folder.");
        return;
    }
    var output = parseResult.GetValue(outputOption)!;
    var note = parseResult.GetValue(noteOption);
    var sort = parseResult.GetValue(sortOption);
    var removeEmptyLines = parseResult.GetValue(removeEmptyLinesOption);
    var author = parseResult.GetValue(authorOption);

    if (sort == "type")
    {
        selectedFiles = selectedFiles
        .OrderBy(f => Path.GetExtension(f))
        .ThenBy(f => Path.GetFileName(f))
        .ToList();
    }
    else
    {
        selectedFiles = selectedFiles
            .OrderBy(f => Path.GetFileName(f))
            .ToList();
    }
    if (extensions.Count == 0)
    {
        Console.WriteLine("Error: no valid languages were given. Nothing to bundle.");
        return;
    }
    try
    {
        using var writer = new StreamWriter(output.FullName);
        if (!string.IsNullOrWhiteSpace(author))
        {
            writer.WriteLine($"// Author: {author}");
        }
        foreach (var file in selectedFiles)
        {
            if (note)
            {
                var relativePath = Path.GetRelativePath(currentDirectory, file);
                writer.WriteLine($"// Source: {relativePath}");
            }
            var content = File.ReadAllText(file);

            if (removeEmptyLines)
            {
                var lines = content.Split('\n');
                var nonEmptyLines = lines.Where(line => !string.IsNullOrWhiteSpace(line));
                content = string.Join("\n", nonEmptyLines);
            }

            writer.WriteLine(content);
        }

        Console.WriteLine($"Bundle created: {output.FullName}");
    }
    catch (DirectoryNotFoundException)
    {
        Console.WriteLine("Error: the folder in the output path does not exist");
    }
    catch (UnauthorizedAccessException)
    {
        Console.WriteLine("Error: no permission to write to this location");
    }
});

rootCommand.Add(bundleCommand);
rootCommand.Add(createRspCommand);
bundleCommand.Add(languageOption);
bundleCommand.Add(outputOption);
bundleCommand.Add(noteOption);
bundleCommand.Add(sortOption);
bundleCommand.Add(removeEmptyLinesOption);
bundleCommand.Add(authorOption);
return rootCommand.Parse(args).Invoke();