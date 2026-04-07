using SherlockHolmes.Api.Models;

namespace SherlockHolmes.Api.Services;

public interface IStoryService
{
    Task<StoryResponse?> GetRandomStoryAsync();
    Task<StoryMetadataResponse?> GetStoryMetadataAsync(string id);
}
