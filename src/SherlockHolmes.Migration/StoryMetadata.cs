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

        // Strip common prefixes for shorter slugs (anchored to start only)
        slug = StripPrefix(slug, "The Adventure of ");
        slug = StripPrefix(slug, "The ");
        slug = StripPrefix(slug, "A ");

        slug = slug.ToLowerInvariant();
        slug = NonAlphanumericRegex().Replace(slug, "-");
        slug = ConsecutiveHyphensRegex().Replace(slug, "-");
        slug = slug.Trim('-');

        return slug;
    }

    private static string StripPrefix(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : value;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex ConsecutiveHyphensRegex();
}
