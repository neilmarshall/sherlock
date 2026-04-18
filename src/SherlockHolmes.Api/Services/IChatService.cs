using SherlockHolmes.Api.Models;

namespace SherlockHolmes.Api.Services;

public interface IChatService
{
    IAsyncEnumerable<string> StreamReplyAsync(
        string storyId,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        CancellationToken cancellationToken);
}

public class StoryNotFoundException : Exception
{
    public StoryNotFoundException(string storyId)
        : base($"Story '{storyId}' not found.") { }
}
