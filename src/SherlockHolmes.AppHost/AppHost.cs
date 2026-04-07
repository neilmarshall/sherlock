var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = storage.AddTables("tables");
var blobs = storage.AddBlobs("blobs");

var api = builder.AddProject<Projects.SherlockHolmes_Api>("api")
    .WithReference(tables)
    .WithReference(blobs)
    .WaitFor(tables)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(api)
    .WaitFor(api);

api.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
