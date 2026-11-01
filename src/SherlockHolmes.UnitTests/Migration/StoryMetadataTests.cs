using SherlockHolmes.Migration;

namespace SherlockHolmes.UnitTests.Migration;

[TestClass]
public class StoryMetadataTests
{
    [TestMethod]
    [DataRow("A Scandal in Bohemia", "scandal-in-bohemia")]
    [DataRow("The Adventure of the Speckled Band", "speckled-band")]
    [DataRow("The Red-Headed League", "red-headed-league")]
    [DataRow("His Last Bow", "his-last-bow")]
    [DataRow("The Adventure of Black Peter", "black-peter")]
    public void GenerateSlug_StripsLeadingArticlesAndNormalises(string title, string expected)
    {
        Assert.AreEqual(expected, StoryMetadata.GenerateSlug(title));
    }

    [TestMethod]
    public void GenerateSlug_CollapsesPunctuationRuns()
    {
        Assert.AreEqual("dying-detective", StoryMetadata.GenerateSlug("The Dying Detective!!"));
    }

    [TestMethod]
    [DataRow("The Adventures of Sherlock Holmes", 1892)]
    [DataRow("The Memoirs of Sherlock Holmes", 1894)]
    [DataRow("The Return of Sherlock Holmes", 1905)]
    [DataRow("His Last Bow", 1917)]
    [DataRow("The Case-Book of Sherlock Holmes", 1927)]
    public void GetYearPublished_ReturnsYearForKnownCollection(string collection, int year)
    {
        Assert.AreEqual(year, StoryMetadata.GetYearPublished(collection));
    }

    [TestMethod]
    public void GetYearPublished_IsCaseInsensitive()
    {
        Assert.AreEqual(1917, StoryMetadata.GetYearPublished("his last bow"));
    }

    [TestMethod]
    public void GetYearPublished_ReturnsZeroForUnknownCollection()
    {
        Assert.AreEqual(0, StoryMetadata.GetYearPublished("Some Other Collection"));
    }
}
