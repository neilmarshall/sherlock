using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Microsoft.SemanticKernel;
using SherlockHolmes.Api.Models;
using SherlockHolmes.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");

// Add application services.
builder.Services.AddSingleton<IStoryService, StoryService>();

// Azure AI Foundry chat completion via Semantic Kernel.
var ai = builder.Configuration.GetSection("AzureOpenAI");
var endpoint = ai["Endpoint"]
    ?? throw new InvalidOperationException("Configuration 'AzureOpenAI:Endpoint' is missing.");
var apiKey = ai["ApiKey"]
    ?? throw new InvalidOperationException("Configuration 'AzureOpenAI:ApiKey' is missing.");
var deployment = ai["DeploymentName"]
    ?? throw new InvalidOperationException("Configuration 'AzureOpenAI:DeploymentName' is missing.");

// Dedicated HttpClient for Azure OpenAI: streaming completions can run longer than the
// default 10s/30s standard resilience timeouts, so we opt out and apply 30s/90s instead.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental
builder.Services.AddHttpClient("azure-openai")
    .RemoveAllResilienceHandlers()
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(90);
    });
#pragma warning restore EXTEXP0001

builder.Services.AddSingleton(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("azure-openai");
    var options = new AzureOpenAIClientOptions
    {
        Transport = new HttpClientPipelineTransport(httpClient),
    };
    return new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey), options);
});

builder.Services.AddAzureOpenAIChatCompletion(deployment);
builder.Services.AddKernel();
builder.Services.AddSingleton<IChatService, ChatService>();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/stories", async (IStoryService storyService) =>
{
    var stories = await storyService.GetAllStoriesMetadataAsync();
    return Results.Ok(stories);
});

app.MapGet("/api/stories/random", async (IStoryService storyService) =>
{
    var story = await storyService.GetRandomStoryAsync();
    return story is null ? Results.StatusCode(503) : Results.Ok(story);
});

app.MapGet("/api/stories/{id}", async (string id, IStoryService storyService) =>
{
    var story = await storyService.GetStoryAsync(id);
    return story is null ? Results.NotFound() : Results.Ok(story);
});

app.MapGet("/api/stories/{id}/metadata", async (string id, IStoryService storyService) =>
{
    var metadata = await storyService.GetStoryMetadataAsync(id);
    return metadata is null ? Results.NotFound() : Results.Ok(metadata);
});

app.MapPost("/api/stories/{id}/chat", async (
    string id,
    ChatRequest request,
    IChatService chatService,
    HttpResponse response,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
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
});

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();
