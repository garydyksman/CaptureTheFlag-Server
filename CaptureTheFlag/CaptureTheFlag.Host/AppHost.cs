var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres").WithDataVolume();
var captureTheFlagDb = postgres.AddDatabase("capturetheflag");

var webApi = builder.AddProject<Projects.CaptureTheFlag_Web>("webapi")
    .WithExternalHttpEndpoints()
    .WaitFor(captureTheFlagDb)
    .WithReference(captureTheFlagDb);

var blazorApp = builder.AddProject<Projects.CaptureTheFlag_App>("app")
    .WaitFor(webApi)
    .WithExternalHttpEndpoints()
    .WithReference(webApi);

// Expose both projects on the internet (Aspire auto-creates and manages the tunnel).
// Note: Devtunnel added but NOT as a dependency to prevent blocking app startup on health checks.
var devTunnel = builder.AddDevTunnel("ctf-public")
    .WithAnonymousAccess()
    .WithReference(webApi)
    .WithReference(blazorApp);

builder.Build().Run();
