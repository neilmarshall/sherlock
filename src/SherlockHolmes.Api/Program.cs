using System.ClientModel;
using System.ClientModel.Primitives;
using System.Threading.RateLimiting;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SherlockHolmes.Api.Endpoints;
using SherlockHolmes.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
builder.AddAzureTableServiceClient("tables");
builder.AddAzureBlobServiceClient("blobs");

// Add application services.
builder.Services.AddSingleton<IStoryService, StoryService>();

// Chat completion via Semantic Kernel. Toggle between Azure AI Foundry and a local
// OpenAI-compatible server (e.g. Ollama at http://localhost:11434/v1) via UseLocalLlm.
if (builder.Configuration.GetValue<bool>("UseLocalLlm"))
{
    var local = builder.Configuration.GetSection("LocalLlm");
    var localEndpoint = local["Endpoint"]
        ?? throw new InvalidOperationException("Configuration 'LocalLlm:Endpoint' is missing.");
    var localModelId = local["ModelId"]
        ?? throw new InvalidOperationException("Configuration 'LocalLlm:ModelId' is missing.");
    // Most local servers ignore the API key, but the SDK requires a non-empty value.
    var localApiKey = local["ApiKey"] ?? "not-needed";

    // Dedicated HttpClient for the local LLM: streaming a reply from an 8B model on CPU
    // can run minutes long, so we replace the standard 10s/30s resilience defaults with
    // a stupidly long 5-minute per-attempt budget.
    #pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental
    builder.Services.AddHttpClient("local-llm")
        .RemoveAllResilienceHandlers()
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(12);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(20);
        });
    #pragma warning restore EXTEXP0001

    #pragma warning disable SKEXP0010 // OpenAIChatCompletionService(string, Uri, ...) is experimental
    builder.Services.AddSingleton<IChatCompletionService>(sp =>
    {
        var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("local-llm");
        return new OpenAIChatCompletionService(
            modelId: localModelId,
            endpoint: new Uri(localEndpoint),
            apiKey: localApiKey,
            httpClient: httpClient,
            loggerFactory: sp.GetService<ILoggerFactory>());
    });
    #pragma warning restore SKEXP0010
}
else
{
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
}

builder.Services.AddKernel();
builder.Services.AddSingleton<IChatService, ChatService>();

// Behind nginx: trust X-Forwarded-* so the rate limiter (and logs) see the real client IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Per-IP sliding window on the chat endpoint to cap LLM spend / load from abuse.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            ctx.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
        }
        ctx.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.HttpContext.Response.WriteAsync(
            "You've hit the chat rate limit. Please try again in a little while.", ct);
    };

    options.AddPolicy("chat", httpContext =>
    {
        var key = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromHours(1),
            SegmentsPerWindow = 6,
            QueueLimit = 0,
        });
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapStoryEndpoints();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();
