using System.Text.RegularExpressions;

namespace SherlockHolmes.Migration;

public static partial class StoryMetadata
{
    private static readonly Dictionary<string, int> CollectionYears = new(StringComparer.OrdinalIgnoreCase)
    {
        ["The Adventures of Sherlock Holmes"] = 1892,
        ["The Memoirs of Sherlock Holmes"] = 1894,
        ["The Return of Sherlock Holmes"] = 1905,
        ["His Last Bow"] = 1917,
        ["The Case-Book of Sherlock Holmes"] = 1927,
    };

    public static int GetYearPublished(string collection)
    {
        return CollectionYears.TryGetValue(collection, out var year) ? year : 0;
    }

    public static string GenerateSlug(string title)
    {
        var slug = title;

        // Strip common prefixes for shorter slugs
        slug = slug.Replace("The Adventure of ", "", StringComparison.OrdinalIgnoreCase);
        slug = slug.Replace("The ", "", StringComparison.OrdinalIgnoreCase);
        slug = slug.Replace("A ", "", StringComparison.OrdinalIgnoreCase);

        slug = slug.ToLowerInvariant();
        slug = NonAlphanumericRegex().Replace(slug, "-");
        slug = ConsecutiveHyphensRegex().Replace(slug, "-");
        slug = slug.Trim('-');

        return slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex ConsecutiveHyphensRegex();
}
