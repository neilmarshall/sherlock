using SherlockHolmes.Api.Models;

namespace SherlockHolmes.Api.Services;

public interface IStoryService
{
    Task<StoryResponse?> GetRandomStoryAsync();
    Task<StoryResponse?> GetStoryAsync(string id);
    Task<IReadOnlyList<StoryMetadataResponse>> GetAllStoriesMetadataAsync();
    Task<StoryMetadataResponse?> GetStoryMetadataAsync(string id);
    Task<string?> GetStoryBodyAsync(string id);
}
