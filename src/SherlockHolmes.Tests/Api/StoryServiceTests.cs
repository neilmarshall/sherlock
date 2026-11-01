using System.Reflection;
using SherlockHolmes.Api.Models;
using SherlockHolmes.Api.Services;
using SherlockHolmes.Tests.Api.Fakes;

namespace SherlockHolmes.Tests.Api;

/// <summary>
/// StoryService keeps its metadata cache in a static field, so state leaks between instances.
/// The cache is cleared before every test, and these tests do not run in parallel (MSTest default),
/// which keeps them order-independent.
/// </summary>
[TestClass]
public class StoryServiceTests
{
    private static readonly StoryEntity Bohemia = Entity("A_Scandal_in_Bohemia", "A Scandal in Bohemia", "The Adventures of Sherlock Holmes", 1892, 7000);
    private static readonly StoryEntity BlackPeter = Entity("The_Adventure_of_Black_Peter", "The Adventure of Black Peter", "The Memoirs of Sherlock Holmes", 1894, 6000);
    private static readonly StoryEntity LastBow = Entity("His_Last_Bow", "His Last Bow", "His Last Bow", 1917, 8000);

    private static readonly Dictionary<string, string> Blobs = new()
    {
        ["A_Scandal_in_Bohemia"] = "Bohemia body",
        ["The_Adventure_of_Black_Peter"] = "Black Peter body",
        ["His_Last_Bow"] = "Last Bow body",
    };

    [TestInitialize]
    public void ResetStaticCache() => SetCache(null);

    [TestCleanup]
    public void ClearStaticCache() => SetCache(null);

    private static StoryEntity Entity(string rowKey, string title, string collection, int year, int words) => new()
    {
        PartitionKey = "story",
        RowKey = rowKey,
        Title = title,
        Collection = collection,
        YearPublished = year,
        WordCount = words,
        BlobName = rowKey,
    };

    private static void SetCache(List<StoryMetadataResponse>? value)
    {
        typeof(StoryService)
            .GetField("_cachedMetadata", BindingFlags.NonPublic | BindingFlags.Static)!
            .SetValue(null, value);
    }

    private static List<StoryMetadataResponse>? GetCache()
        => (List<StoryMetadataResponse>?)typeof(StoryService)
            .GetField("_cachedMetadata", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null);

    private static (StoryService Service, FakeTableServiceClient Tables, FakeBlobServiceClient Blobs) Create(params StoryEntity[] stories)
    {
        var tables = new FakeTableServiceClient(stories);
        var blobs = new FakeBlobServiceClient(Blobs);
        return (new StoryService(tables, blobs), tables, blobs);
    }

    [TestMethod]
    public async Task GetAllStoriesMetadataAsync_MapsEntitiesAndQueriesStoryPartition()
    {
        var (service, tables, _) = Create(Bohemia, LastBow);

        var result = await service.GetAllStoriesMetadataAsync();

        Assert.HasCount(2, result);
        var bohemia = result.Single(s => s.Id == "A_Scandal_in_Bohemia");
        Assert.AreEqual("A Scandal in Bohemia", bohemia.Title);
        Assert.AreEqual("The Adventures of Sherlock Holmes", bohemia.Collection);
        Assert.AreEqual(1892, bohemia.YearPublished);
        Assert.AreEqual(7000, bohemia.WordCount);
        Assert.AreEqual("PartitionKey eq 'story'", tables.StoriesTable.LastFilter);
    }

    [TestMethod]
    public async Task GetAllStoriesMetadataAsync_CachesResultAfterFirstCall()
    {
        var (service, tables, _) = Create(Bohemia, LastBow);

        var first = await service.GetAllStoriesMetadataAsync();
        var second = await service.GetAllStoriesMetadataAsync();

        Assert.AreEqual(1, tables.StoriesTable.QueryCount, "Table should only be queried once.");
        Assert.AreSame(first, second);
    }

    [TestMethod]
    public async Task GetAllStoriesMetadataAsync_CacheIsSharedAcrossInstances()
    {
        var (first, tables, _) = Create(Bohemia);
        await first.GetAllStoriesMetadataAsync();

        var (second, _, _) = Create(LastBow);
        var result = await second.GetAllStoriesMetadataAsync();

        Assert.HasCount(1, result);
        Assert.AreEqual("A_Scandal_in_Bohemia", result[0].Id);
        Assert.AreEqual(1, tables.StoriesTable.QueryCount);
    }

    [TestMethod]
    public async Task GetAllStoriesMetadataAsync_PopulatesStaticCache()
    {
        var (service, _, _) = Create(Bohemia);

        Assert.IsNull(GetCache());
        await service.GetAllStoriesMetadataAsync();

        Assert.IsNotNull(GetCache());
        Assert.HasCount(1, GetCache()!);
    }

    [TestMethod]
    public async Task GetRandomStoryAsync_ReturnsNullWhenNoStories()
    {
        var (service, _, _) = Create();

        var story = await service.GetRandomStoryAsync();

        Assert.IsNull(story);
    }

    [TestMethod]
    public async Task GetRandomStoryAsync_ReturnsStoryWhoseBodyMatchesItsId()
    {
        var (service, _, _) = Create(Bohemia, BlackPeter, LastBow);

        // Random selection is not seedable, so check the invariant over several draws:
        // every returned story is one of the stored stories and carries its own body.
        var expected = new Dictionary<string, (string Title, string Body)>
        {
            ["A_Scandal_in_Bohemia"] = ("A Scandal in Bohemia", "Bohemia body"),
            ["The_Adventure_of_Black_Peter"] = ("The Adventure of Black Peter", "Black Peter body"),
            ["His_Last_Bow"] = ("His Last Bow", "Last Bow body"),
        };

        for (var i = 0; i < 25; i++)
        {
            var story = await service.GetRandomStoryAsync();

            Assert.IsNotNull(story);
            Assert.IsTrue(expected.ContainsKey(story.Id), $"Unexpected id {story.Id}");
            Assert.AreEqual(expected[story.Id].Title, story.Title);
            Assert.AreEqual(expected[story.Id].Body, story.Body);
        }
    }

    [TestMethod]
    public async Task GetRandomStoryAsync_UsesCachedMetadataForSelection()
    {
        var (service, tables, _) = Create(Bohemia, LastBow);

        await service.GetRandomStoryAsync();
        await service.GetRandomStoryAsync();
        await service.GetRandomStoryAsync();

        // One query for the metadata list; the per-story lookups use GetEntityAsync, not QueryAsync.
        Assert.AreEqual(1, tables.StoriesTable.QueryCount);
    }

    [TestMethod]
    public async Task GetStoryAsync_ReturnsFullStoryWithBody()
    {
        var (service, _, blobs) = Create(Bohemia);

        var story = await service.GetStoryAsync("A_Scandal_in_Bohemia");

        Assert.IsNotNull(story);
        Assert.AreEqual(new StoryResponse(
            "A_Scandal_in_Bohemia",
            "A Scandal in Bohemia",
            "The Adventures of Sherlock Holmes",
            1892,
            7000,
            "Bohemia body"), story);
        Assert.AreEqual("stories", blobs.RequestedContainerName);
    }

    [TestMethod]
    public async Task GetStoryAsync_ReturnsNullWhenEntityMissing()
    {
        var (service, _, _) = Create(Bohemia);

        var story = await service.GetStoryAsync("does_not_exist");

        Assert.IsNull(story);
    }

    [TestMethod]
    public async Task GetStoryAsync_ReturnsNullWhenBlobMissing()
    {
        var orphan = Entity("Orphan", "Orphan", "Collection", 0, 0);
        orphan.BlobName = "no-such-blob";
        var (service, _, _) = Create(orphan);

        var story = await service.GetStoryAsync("Orphan");

        Assert.IsNull(story);
    }

    [TestMethod]
    public async Task GetStoryMetadataAsync_ReturnsMetadataWithoutBody()
    {
        var (service, tables, _) = Create(LastBow);

        var metadata = await service.GetStoryMetadataAsync("His_Last_Bow");

        Assert.AreEqual(new StoryMetadataResponse("His_Last_Bow", "His Last Bow", "His Last Bow", 1917, 8000), metadata);
        Assert.IsNotNull(tables.StoriesTable.LastSelect);
        CollectionAssert.DoesNotContain(tables.StoriesTable.LastSelect!.ToList(), "BlobName");
    }

    [TestMethod]
    public async Task GetStoryMetadataAsync_ReturnsNullWhenMissing()
    {
        var (service, _, _) = Create(LastBow);

        var metadata = await service.GetStoryMetadataAsync("missing");

        Assert.IsNull(metadata);
    }

    [TestMethod]
    public async Task GetStoryBodyAsync_ReturnsBlobContent()
    {
        var (service, tables, _) = Create(BlackPeter);

        var body = await service.GetStoryBodyAsync("The_Adventure_of_Black_Peter");

        Assert.AreEqual("Black Peter body", body);
        CollectionAssert.AreEqual(new[] { "BlobName" }, tables.StoriesTable.LastSelect!.ToList());
    }

    [TestMethod]
    public async Task GetStoryBodyAsync_ReturnsNullWhenEntityMissing()
    {
        var (service, _, _) = Create(BlackPeter);

        var body = await service.GetStoryBodyAsync("missing");

        Assert.IsNull(body);
    }
}
