using SherlockHolmes.Migration;

namespace SherlockHolmes.UnitTests.Migration;

[TestClass]
public class StoryParserTests
{
    private const string Separator = "============================================================";

    private string _tempDir = "";

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sherlock-parser-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteFile(string content)
    {
        var path = Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, content);
        return path;
    }

    [TestMethod]
    public void Parse_ExtractsTitleAndCollectionFromHeader()
    {
        var path = WriteFile(
            "Title: A Study in Scarlet\n" +
            "Collection: Novel\n" +
            "Author: Arthur Conan Doyle\n" +
            "\n" +
            Separator + "\n" +
            "Body text here.\n");

        var story = StoryParser.Parse(path);

        Assert.AreEqual("A Study in Scarlet", story.Title);
        Assert.AreEqual("Novel", story.Collection);
    }

    [TestMethod]
    public void Parse_TrimsBodyAndExcludesHeaderAndSeparator()
    {
        var path = WriteFile(
            "Title: T\n" +
            "Collection: C\n" +
            Separator + "\n\n" +
            "  First line.\n\nSecond line.  \n\n");

        var story = StoryParser.Parse(path);

        Assert.AreEqual("First line.\n\nSecond line.", story.Body);
        Assert.IsFalse(story.Body.Contains("Title:"));
        Assert.IsFalse(story.Body.Contains(Separator));
    }

    [TestMethod]
    public void Parse_CountsWordsSeparatedByAnyWhitespace()
    {
        var path = WriteFile(
            "Title: T\nCollection: C\n" + Separator + "\n" +
            "one two\tthree\r\nfour   five\n\n");

        var story = StoryParser.Parse(path);

        Assert.AreEqual(5, story.WordCount);
    }

    [TestMethod]
    public void Parse_WordCountIsZeroForEmptyBody()
    {
        var path = WriteFile("Title: T\nCollection: C\n" + Separator + "\n   \n");

        var story = StoryParser.Parse(path);

        Assert.AreEqual(0, story.WordCount);
        Assert.AreEqual("", story.Body);
    }

    [TestMethod]
    public void Parse_FieldNamesAreMatchedCaseInsensitively()
    {
        var path = WriteFile("title: lower case\nCOLLECTION: Upper\n" + Separator + "\nBody\n");

        var story = StoryParser.Parse(path);

        Assert.AreEqual("lower case", story.Title);
        Assert.AreEqual("Upper", story.Collection);
    }

    [TestMethod]
    public void Parse_ThrowsWhenSeparatorMissing()
    {
        var path = WriteFile("Title: T\nCollection: C\nno separator here\n");

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => StoryParser.Parse(path));

        StringAssert.Contains(ex.Message, "No separator");
    }

    [TestMethod]
    public void Parse_ThrowsWhenTitleMissing()
    {
        var path = WriteFile("Collection: C\n" + Separator + "\nBody\n");

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => StoryParser.Parse(path));

        StringAssert.Contains(ex.Message, "'Title:'");
    }

    [TestMethod]
    public void Parse_ThrowsWhenCollectionMissing()
    {
        var path = WriteFile("Title: T\n" + Separator + "\nBody\n");

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => StoryParser.Parse(path));

        StringAssert.Contains(ex.Message, "'Collection:'");
    }

    [TestMethod]
    public void Parse_RealStory_AScandalInBohemia_ParsesHeaderAndBody()
    {
        var dir = RepoPaths.FindStoriesDirectory();
        if (dir is null)
            Assert.Inconclusive("data/stories not found above the test binary folder.");

        var story = StoryParser.Parse(Path.Combine(dir, "A_Scandal_in_Bohemia.txt"));

        Assert.AreEqual("A Scandal in Bohemia", story.Title);
        Assert.AreEqual("The Adventures of Sherlock Holmes", story.Collection);
        Assert.IsTrue(story.Body.StartsWith("I. A SCANDAL IN BOHEMIA", StringComparison.Ordinal));
        Assert.IsFalse(story.Body.Contains("Title:"));
        Assert.IsFalse(story.Body.Contains(Separator));
        Assert.IsGreaterThan(1000, story.WordCount);
    }

    [TestMethod]
    public void Parse_AllRealStories_HaveTitleCollectionAndKnownYear()
    {
        var dir = RepoPaths.FindStoriesDirectory();
        if (dir is null)
            Assert.Inconclusive("data/stories not found above the test binary folder.");

        var files = Directory.GetFiles(dir, "*.txt");
        Assert.HasCount(56, files);

        foreach (var file in files)
        {
            var story = StoryParser.Parse(file);

            Assert.IsFalse(string.IsNullOrWhiteSpace(story.Title), $"Empty title in {Path.GetFileName(file)}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(story.Body), $"Empty body in {Path.GetFileName(file)}");
            Assert.IsGreaterThan(0, story.WordCount, $"No words in {Path.GetFileName(file)}");
            Assert.AreNotEqual(0, StoryMetadata.GetYearPublished(story.Collection),
                $"Unmapped collection '{story.Collection}' in {Path.GetFileName(file)}");
        }
    }
}
