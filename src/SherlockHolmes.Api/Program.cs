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

builder.Services.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
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

app.MapGet("/api/stories/random", async (IStoryService storyService) =>
{
    var story = await storyService.GetRandomStoryAsync();
    return story is null ? Results.StatusCode(503) : Results.Ok(story);
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
    CancellationToken cancellationToken) =>
{
    response.ContentType = "text/plain; charset=utf-8";

    try
    {
        await foreach (var chunk in chatService.StreamReplyAsync(
            id, request.History, request.Message, cancellationToken))
        {
            await response.WriteAsync(chunk, cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
    catch (StoryNotFoundException)
    {
        if (!response.HasStarted)
        {
            response.StatusCode = StatusCodes.Status404NotFound;
        }
    }
});

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();
