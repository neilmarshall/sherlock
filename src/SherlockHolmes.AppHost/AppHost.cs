var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = storage.AddTables("tables");
var blobs = storage.AddBlobs("blobs");

var foundryEndpoint = builder.AddParameter("foundry-endpoint", secret: true);
var foundryApiKey = builder.AddParameter("foundry-api-key", secret: true);
var foundryDeployment = builder.AddParameter("foundry-deployment");

var migration = builder.AddProject<Projects.SherlockHolmes_Migration>("migration")
    .WithReference(tables)
    .WithReference(blobs)
    .WaitFor(tables);

var api = builder.AddProject<Projects.SherlockHolmes_Api>("api")
    .WithReference(tables)
    .WithReference(blobs)
    .WithEnvironment("AzureOpenAI__Endpoint", foundryEndpoint)
    .WithEnvironment("AzureOpenAI__ApiKey", foundryApiKey)
    .WithEnvironment("AzureOpenAI__DeploymentName", foundryDeployment)
    .WaitFor(migration)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(api)
    .WaitFor(api);

api.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
