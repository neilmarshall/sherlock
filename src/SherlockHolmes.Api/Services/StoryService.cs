using Azure.Data.Tables;
using Azure.Storage.Blobs;
using SherlockHolmes.Api.Models;

namespace SherlockHolmes.Api.Services;

public class StoryService : IStoryService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;

    private static List<StoryMetadataResponse>? _cachedMetadata;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public StoryService(TableServiceClient tableServiceClient, BlobServiceClient blobServiceClient)
    {
        _tableServiceClient = tableServiceClient;
        _blobServiceClient = blobServiceClient;
    }

    public async Task<StoryResponse?> GetRandomStoryAsync()
    {
        var metadata = await GetAllStoriesMetadataAsync();
        if (metadata.Count == 0) return null;

        var randomId = metadata[Random.Shared.Next(metadata.Count)].Id;
        return await GetStoryAsync(randomId);
    }

    public async Task<StoryResponse?> GetStoryAsync(string id)
    {
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("stories");
            var entity = (await tableClient.GetEntityAsync<StoryEntity>("story", id)).Value;

            var blobContainerClient = _blobServiceClient.GetBlobContainerClient("stories");
            var blobClient = blobContainerClient.GetBlobClient(entity.BlobName);
            var blobResponse = await blobClient.DownloadContentAsync();
            var body = blobResponse.Value.Content.ToString();

            return new StoryResponse(
                entity.RowKey,
                entity.Title,
                entity.Collection,
                entity.YearPublished,
                entity.WordCount,
                body);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<StoryMetadataResponse>> GetAllStoriesMetadataAsync()
    {
        if (_cachedMetadata is not null) return _cachedMetadata;

        await _lock.WaitAsync();
        try
        {
            if (_cachedMetadata is not null) return _cachedMetadata;

            var tableClient = _tableServiceClient.GetTableClient("stories");
            var list = new List<StoryMetadataResponse>();
            await foreach (var entity in tableClient.QueryAsync<StoryEntity>(
                filter: "PartitionKey eq 'story'",
                select: ["RowKey", "Title", "Collection", "YearPublished", "WordCount"]))
            {
                list.Add(new StoryMetadataResponse(
                    entity.RowKey,
                    entity.Title,
                    entity.Collection,
                    entity.YearPublished,
                    entity.WordCount));
            }
            _cachedMetadata = list;
            return list;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetStoryBodyAsync(string id)
    {
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("stories");
            var entity = (await tableClient.GetEntityAsync<StoryEntity>(
                "story", id, select: ["BlobName"])).Value;

            var blobContainerClient = _blobServiceClient.GetBlobContainerClient("stories");
            var blobClient = blobContainerClient.GetBlobClient(entity.BlobName);
            var blobResponse = await blobClient.DownloadContentAsync();
            return blobResponse.Value.Content.ToString();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<StoryMetadataResponse?> GetStoryMetadataAsync(string id)
    {
        try
        {
            var tableClient = _tableServiceClient.GetTableClient("stories");
            var entity = (await tableClient.GetEntityAsync<StoryEntity>(
                "story", id,
                select: ["RowKey", "Title", "Collection", "YearPublished", "WordCount"])).Value;

            return new StoryMetadataResponse(
                entity.RowKey,
                entity.Title,
                entity.Collection,
                entity.YearPublished,
                entity.WordCount);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
