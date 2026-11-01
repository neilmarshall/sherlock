using SherlockHolmes.Api.Models;
using SherlockHolmes.Api.Services;

namespace SherlockHolmes.Tests.Api.Fakes;

/// <summary>Minimal IStoryService that only serves story bodies, for ChatService tests.</summary>
internal sealed class FakeStoryService : IStoryService
{
    private readonly IReadOnlyDictionary<string, string> _bodies;

    public FakeStoryService(IReadOnlyDictionary<string, string> bodies)
    {
        _bodies = bodies;
    }

    public Task<string?> GetStoryBodyAsync(string id)
        => Task.FromResult(_bodies.TryGetValue(id, out var body) ? body : null);

    public Task<StoryResponse?> GetRandomStoryAsync() => throw new NotSupportedException();
    public Task<StoryResponse?> GetStoryAsync(string id) => throw new NotSupportedException();
    public Task<IReadOnlyList<StoryMetadataResponse>> GetAllStoriesMetadataAsync() => throw new NotSupportedException();
    public Task<StoryMetadataResponse?> GetStoryMetadataAsync(string id) => throw new NotSupportedException();
}
