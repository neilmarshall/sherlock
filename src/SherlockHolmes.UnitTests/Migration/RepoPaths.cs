namespace SherlockHolmes.UnitTests.Migration;

internal static class RepoPaths
{
    /// <summary>
    /// Walks up from the test binary folder until it finds the repository's data/stories folder.
    /// Returns null when it cannot be found (e.g. the tests were copied away from the repo).
    /// </summary>
    public static string? FindStoriesDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "stories");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
