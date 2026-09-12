using ModuLens.Core.Parsing;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: ModuLens.Cli <javascript-file>");
    return 2;
}

var filePath = args[0];

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"Source file not found: {filePath}");
    return 3;
}

try
{
    var sourceText = await File.ReadAllTextAsync(filePath);
    var document = new SectionParser().Parse(filePath, sourceText);

    Console.WriteLine("Name\tStartLine\tEndLine");
    foreach (var section in document.Sections)
    {
        Console.WriteLine(
            $"{section.Name.Replace('\t', ' ')}\t" +
            $"{section.FullRange.StartLine}\t" +
            $"{section.FullRange.EndLine}");
    }

    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Unable to read source file '{filePath}': {exception.Message}");
    return 4;
}
