using SherlockHolmes.Api.Models;
using SherlockHolmes.Api.Services;

namespace SherlockHolmes.Api.Endpoints;

public static class StoryEndpoints
{
    public static IEndpointRouteBuilder MapStoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var stories = endpoints.MapGroup("/api/stories");

        stories.MapGet("", async (IStoryService storyService) =>
            Results.Ok(await storyService.GetAllStoriesMetadataAsync()));

        stories.MapGet("/random", async (IStoryService storyService) =>
        {
            var story = await storyService.GetRandomStoryAsync();
            return story is null ? Results.StatusCode(503) : Results.Ok(story);
        });

        stories.MapGet("/{id}", async (string id, IStoryService storyService) =>
        {
            var story = await storyService.GetStoryAsync(id);
            return story is null ? Results.NotFound() : Results.Ok(story);
        });

        stories.MapGet("/{id}/metadata", async (string id, IStoryService storyService) =>
        {
            var metadata = await storyService.GetStoryMetadataAsync(id);
            return metadata is null ? Results.NotFound() : Results.Ok(metadata);
        });

        stories.MapPost("/{id}/chat", HandleChatAsync)
            .RequireRateLimiting("chat");

        return endpoints;
    }

    private static async Task HandleChatAsync(
        string id,
        ChatRequest request,
        IChatService chatService,
        HttpResponse response,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        static async Task WriteErrorAsync(HttpResponse response, int statusCode, string message)
        {
            if (response.HasStarted) return;
            response.StatusCode = statusCode;
            response.ContentType = "text/plain; charset=utf-8";
            await response.WriteAsync(message);
        }

        try
        {
            var headersWritten = false;
            await foreach (var chunk in chatService.StreamReplyAsync(
                id, request.History, request.Message, cancellationToken))
            {
                if (!headersWritten)
                {
                    response.ContentType = "text/plain; charset=utf-8";
                    headersWritten = true;
                }
                await response.WriteAsync(chunk, cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (StoryNotFoundException)
        {
            await WriteErrorAsync(response, StatusCodes.Status404NotFound,
                "We couldn't find this case in the archive. Try drawing another.");
        }
        catch (ChatException ex)
        {
            await WriteErrorAsync(response, ex.StatusCode, ex.UserMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected — nothing to do.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error in chat endpoint for story {StoryId}", id);
            await WriteErrorAsync(response, StatusCodes.Status500InternalServerError,
                "Something went wrong while drafting a reply. Please try again.");
        }
    }
}
