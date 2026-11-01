using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SherlockHolmes.Tests.Api.Fakes;

/// <summary>
/// Records the ChatHistory passed to streaming completion and replays scripted chunks.
/// Non-streaming calls are not used by ChatService and throw.
/// </summary>
internal sealed class FakeChatCompletionService : IChatCompletionService
{
    private readonly string[] _chunks;

    public FakeChatCompletionService(params string[] chunks)
    {
        _chunks = chunks;
    }

    public ChatHistory? ReceivedHistory { get; private set; }

    public int StreamCallCount { get; private set; }

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamCallCount++;
        ReceivedHistory = chatHistory;

        foreach (var chunk in _chunks)
        {
            await Task.Yield();
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, chunk);
        }
    }
}
