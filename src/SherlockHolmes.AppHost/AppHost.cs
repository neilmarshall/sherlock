var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = storage.AddTables("tables");
var blobs = storage.AddBlobs("blobs");

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

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(api)
    .WaitFor(api);

api.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
