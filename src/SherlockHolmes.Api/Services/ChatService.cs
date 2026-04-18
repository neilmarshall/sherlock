using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SherlockHolmes.Api.Models;

namespace SherlockHolmes.Api.Services;

public class ChatService : IChatService
{
    private readonly Kernel _kernel;
    private readonly IStoryService _storyService;

    public ChatService(Kernel kernel, IStoryService storyService)
    {
        _kernel = kernel;
        _storyService = storyService;
    }

    public async IAsyncEnumerable<string> StreamReplyAsync(
        string storyId,
        IReadOnlyList<ChatTurn> history,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var body = await _storyService.GetStoryBodyAsync(storyId)
            ?? throw new StoryNotFoundException(storyId);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(
            "You are a literary assistant answering questions strictly about the following Sherlock Holmes story. " +
            "Keep answers grounded in the text. If asked about anything unrelated to this story, politely decline.\n\n" +
            "Story:\n\n" + body);

        foreach (var turn in history)
        {
            if (string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                chatHistory.AddAssistantMessage(turn.Content);
            else if (string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase))
                chatHistory.AddUserMessage(turn.Content);
        }

        chatHistory.AddUserMessage(userMessage);

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(
            chatHistory, kernel: _kernel, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
                yield return chunk.Content;
        }
    }
}
