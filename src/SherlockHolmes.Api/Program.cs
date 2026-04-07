using SherlockHolmes.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");

// Add application services.
builder.Services.AddSingleton<IStoryService, StoryService>();

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

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();
