using Azure;
using Azure.Data.Tables;
using SherlockHolmes.Api.Models;

namespace SherlockHolmes.UnitTests.Api.Fakes;

/// <summary>
/// In-memory stand-in for the Azure Table service. Only the "stories" table is supported.
/// Azure's clients expose protected parameterless constructors and virtual members, so they can be
/// subclassed without any Azure SDK mocking library.
/// </summary>
internal sealed class FakeTableServiceClient : TableServiceClient
{
    private readonly FakeTableClient _storiesTable;

    public FakeTableServiceClient(params StoryEntity[] stories)
    {
        _storiesTable = new FakeTableClient(stories);
    }

    public FakeTableClient StoriesTable => _storiesTable;

    public override TableClient GetTableClient(string tableName)
    {
        if (tableName != "stories")
            throw new InvalidOperationException($"Unexpected table '{tableName}'.");
        return _storiesTable;
    }
}

internal sealed class FakeTableClient : TableClient
{
    private readonly List<StoryEntity> _entities;

    public FakeTableClient(IEnumerable<StoryEntity> entities)
    {
        _entities = entities.ToList();
    }

    /// <summary>Number of QueryAsync calls received (used to verify caching).</summary>
    public int QueryCount { get; private set; }

    /// <summary>Filter string passed to the most recent QueryAsync call.</summary>
    public string? LastFilter { get; private set; }

    /// <summary>Select list passed to the most recent GetEntityAsync call.</summary>
    public IEnumerable<string>? LastSelect { get; private set; }

    public override Task<Response<T>> GetEntityAsync<T>(
        string partitionKey,
        string rowKey,
        IEnumerable<string>? select = null,
        CancellationToken cancellationToken = default)
    {
        LastSelect = select;

        var entity = _entities.FirstOrDefault(e => e.PartitionKey == partitionKey && e.RowKey == rowKey)
            ?? throw new RequestFailedException(404, "Entity not found.", "ResourceNotFound", null);

        if (entity is not T typed)
            throw new NotSupportedException($"FakeTableClient only supports {typeof(StoryEntity).Name}.");

        return Task.FromResult(Response.FromValue(typed, null!));
    }

    public override AsyncPageable<T> QueryAsync<T>(
        string? filter = null,
        int? maxPerPage = null,
        IEnumerable<string>? select = null,
        CancellationToken cancellationToken = default)
    {
        QueryCount++;
        LastFilter = filter;

        var items = _entities
            .Where(e => e is T)
            .Cast<T>()
            .ToList();

        return AsyncPageable<T>.FromPages([Page<T>.FromValues(items, continuationToken: null, response: null!)]);
    }
}
