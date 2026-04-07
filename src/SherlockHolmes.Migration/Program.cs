using System.Text;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using SherlockHolmes.Migration;

var connectionString = args.Length > 0 ? args[0] : "UseDevelopmentStorage=true";
var storiesPath = args.Length > 1
    ? args[1]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "stories"));

if (!Directory.Exists(storiesPath))
{
    Console.Error.WriteLine($"Stories directory not found: {storiesPath}");
    return 1;
}

Console.WriteLine($"Connection: {connectionString}");
Console.WriteLine($"Stories path: {storiesPath}");

var tableServiceClient = new TableServiceClient(connectionString);
var tableClient = tableServiceClient.GetTableClient("stories");
await tableClient.CreateIfNotExistsAsync();

var blobServiceClient = new BlobServiceClient(connectionString);
var blobContainerClient = blobServiceClient.GetBlobContainerClient("stories");
await blobContainerClient.CreateIfNotExistsAsync();

var files = Directory.GetFiles(storiesPath, "*.txt");
Console.WriteLine($"Found {files.Length} story files");

var successCount = 0;
foreach (var file in files)
{
    try
    {
        var story = StoryParser.Parse(file);
        var slug = StoryMetadata.GenerateSlug(story.Title);
        var year = StoryMetadata.GetYearPublished(story.Collection);

        // Upload body to blob storage
        var blobClient = blobContainerClient.GetBlobClient(slug);
        using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(story.Body));
        await blobClient.UploadAsync(bodyStream, overwrite: true);

        // Write metadata to table storage
        var entity = new TableEntity("story", slug)
        {
            { "Title", story.Title },
            { "Collection", story.Collection },
            { "YearPublished", year },
            { "WordCount", story.WordCount },
            { "BlobName", slug },
        };
        await tableClient.UpsertEntityAsync(entity);

        Console.WriteLine($"  [{++successCount:D2}] {story.Title} -> {slug} ({story.WordCount} words, {year})");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  FAILED: {Path.GetFileName(file)}: {ex.Message}");
    }
}

Console.WriteLine($"\nMigration complete: {successCount}/{files.Length} stories imported.");
return successCount == files.Length ? 0 : 1;
