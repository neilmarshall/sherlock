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

public class ChatException : Exception
{
    public int StatusCode { get; }
    public string UserMessage { get; }

    public ChatException(int statusCode, string userMessage, Exception? inner = null)
        : base(userMessage, inner)
    {
        StatusCode = statusCode;
        UserMessage = userMessage;
    }
}
