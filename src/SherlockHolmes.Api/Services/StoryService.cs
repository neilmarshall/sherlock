using Azure.Data.Tables;
using Azure.Storage.Blobs;
using SherlockHolmes.Api.Models;

namespace SherlockHolmes.Api.Services;

public class StoryService : IStoryService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;

    private static List<string>? _cachedRowKeys;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public StoryService(TableServiceClient tableServiceClient, BlobServiceClient blobServiceClient)
    {
        _tableServiceClient = tableServiceClient;
        _blobServiceClient = blobServiceClient;
    }

    public async Task<StoryResponse?> GetRandomStoryAsync()
    {
        var keys = await GetRowKeysAsync();
        if (keys.Count == 0) return null;

        var randomKey = keys[Random.Shared.Next(keys.Count)];
        var tableClient = _tableServiceClient.GetTableClient("stories");
        var entity = (await tableClient.GetEntityAsync<StoryEntity>("story", randomKey)).Value;

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

    private async Task<List<string>> GetRowKeysAsync()
    {
        if (_cachedRowKeys is not null) return _cachedRowKeys;

        await _lock.WaitAsync();
        try
        {
            if (_cachedRowKeys is not null) return _cachedRowKeys;

            var tableClient = _tableServiceClient.GetTableClient("stories");
            var keys = new List<string>();
            await foreach (var entity in tableClient.QueryAsync<StoryEntity>(
                filter: "PartitionKey eq 'story'",
                select: ["RowKey"]))
            {
                keys.Add(entity.RowKey);
            }
            _cachedRowKeys = keys;
            return keys;
        }
        finally
        {
            _lock.Release();
        }
    }
}
