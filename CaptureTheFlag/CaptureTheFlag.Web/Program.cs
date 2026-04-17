using System.Text.Json.Serialization;
using CaptureTheFlag.Web.Data;
using CaptureTheFlag.Web.Endpoints;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("capturetheflag")));

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CaptureTheFlag API",
        Version = "v1",
        Description =
            "HTTP API for Capture The Flag games. "
            + "Admin flows use `/api/games/*`. ESP32 / nanoFramework clients use `/api/game/*` "
            + "(UTF-8 JSON, camelCase, numeric `status` 0–3 on GET /api/game; see tag **DeviceGame (ESP32)**)."
    });

    options.EnableAnnotations();

    // Align OpenAPI nullability with C# non-nullable reference types where possible.
    options.SchemaGeneratorOptions.SupportNonNullableReferenceTypes = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "DevelopmentPermissive",
        policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CaptureTheFlag API v1");
    });

    app.UseCors("DevelopmentPermissive");
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();

app.MapGameEndpoints();
app.MapDeviceGameEndpoints();

app.MapDefaultEndpoints();

app.Run();
