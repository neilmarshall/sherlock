namespace SherlockHolmes.Migration;

public record ParsedStory(string Title, string Collection, string Body, int WordCount);

public static class StoryParser
{
    private const string Separator = "============================================================";

    public static ParsedStory Parse(string filePath)
    {
        var content = File.ReadAllText(filePath);

        var separatorIndex = content.IndexOf(Separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
            throw new InvalidOperationException($"No separator found in {filePath}");

        var header = content[..separatorIndex];
        var body = content[(separatorIndex + Separator.Length)..].Trim();

        var title = ExtractField(header, "Title:");
        var collection = ExtractField(header, "Collection:");
        var wordCount = body.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Length;

        return new ParsedStory(title, collection, body, wordCount);
    }

    private static string ExtractField(string header, string fieldName)
    {
        foreach (var line in header.Split('\n'))
        {
            if (line.StartsWith(fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return line[fieldName.Length..].Trim();
            }
        }
        throw new InvalidOperationException($"Field '{fieldName}' not found in header");
    }
}
