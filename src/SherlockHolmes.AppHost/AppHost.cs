var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = storage.AddTables("tables");
var blobs = storage.AddBlobs("blobs");

var useLocalLlm = bool.TryParse(builder.Configuration["UseLocalLlm"], out var ull) && ull;

var migration = builder.AddProject<Projects.SherlockHolmes_Migration>("migration")
    .WithReference(tables)
    .WithReference(blobs)
    .WaitFor(tables);

var api = builder.AddProject<Projects.SherlockHolmes_Api>("api")
    .WithReference(tables)
    .WithReference(blobs)
    .WaitFor(migration)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

if (useLocalLlm)
{
    var modelId = builder.Configuration["LocalLlm:ModelId"] ?? "llama3.1:8b";

    var ollama = builder.AddContainer("ollama", "ollama/ollama")
        .WithContainerName("ollama")
        .WithVolume("ollama-data", "/root/.ollama")
        .WithHttpEndpoint(port: 11434, targetPort: 11434, name: "http")
        .WithLifetime(ContainerLifetime.Persistent);

    api.WaitFor(ollama)
        .WithEnvironment("UseLocalLlm", "true")
        .WithEnvironment("LocalLlm__ModelId", modelId)
        .WithEnvironment(ctx =>
        {
            var url = ollama.GetEndpoint("http").Url;
            ctx.EnvironmentVariables["LocalLlm__Endpoint"] = $"{url}/v1";
        });
}
else
{
    var foundryEndpoint = builder.AddParameter("foundry-endpoint", secret: true);
    var foundryApiKey = builder.AddParameter("foundry-api-key", secret: true);
    var foundryDeployment = builder.AddParameter("foundry-deployment");

    api.WithEnvironment("AzureOpenAI__Endpoint", foundryEndpoint)
        .WithEnvironment("AzureOpenAI__ApiKey", foundryApiKey)
        .WithEnvironment("AzureOpenAI__DeploymentName", foundryDeployment);
}

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(api)
    .WaitFor(api);

api.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
