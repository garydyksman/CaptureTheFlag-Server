var builder = DistributedApplication.CreateBuilder(args);

var webApi = builder.AddProject<Projects.CaptureTheFlag_Web>("webapi");
var blazorApp = builder.AddProject<Projects.CaptureTheFlag_App>("app");

blazorApp.WithReference(webApi);

builder.Build().Run();