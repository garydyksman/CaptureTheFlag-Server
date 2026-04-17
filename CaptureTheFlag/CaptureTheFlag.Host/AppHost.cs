var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var captureTheFlagDb = postgres.AddDatabase("capturetheflag");

var webApi = builder.AddProject<Projects.CaptureTheFlag_Web>("webapi")
    .WithExternalHttpEndpoints()
    .WaitFor(captureTheFlagDb)
    .WithReference(captureTheFlagDb);

var blazorApp = builder.AddProject<Projects.CaptureTheFlag_App>("app")
    .WaitFor(webApi)
    .WithExternalHttpEndpoints();

// Expose both projects on the internet (install devtunnel CLI; URLs appear in Aspire dashboard).
var devTunnel = builder.AddDevTunnel("ctf-public")
    .WithAnonymousAccess()
    .WithReference(webApi)
    .WithReference(blazorApp);

// Blazor calls the API using the tunneled address (ESP32 + server share the same public base URL).
blazorApp.WithReference(webApi, devTunnel);

builder.Build().Run();
