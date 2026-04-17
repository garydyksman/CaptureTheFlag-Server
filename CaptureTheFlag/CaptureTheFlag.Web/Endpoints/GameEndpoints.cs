using System.Text.Json.Serialization;
using CaptureTheFlag.Web.Contracts;
using CaptureTheFlag.Web.Data;
using CaptureTheFlag.Web.Enums;
using CaptureTheFlag.Web.Models;
using CaptureTheFlag.Web.Json;
using CaptureTheFlag.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace CaptureTheFlag.Web.Endpoints;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/games").WithTags("Games");

        group.MapGet("/", ListGames);
        group.MapGet("/{id:int}", GetGame);
        group.MapPost("/", CreateGame);
        group.MapDelete("/{id:int}", DeleteGame);

        group.MapPost("/{id:int}/devices", RegisterDevice);
        group.MapGet("/{id:int}/devices/{deviceId:int}/player-info", GetPlayerInfo);

        var status = group.MapGroup("/{id:int}/status").WithTags("Games");
        status.MapPost("/waiting-for-players", ToWaitingForPlayers);
        status.MapPost("/in-progress", ToInProgress);
        status.MapPost("/finished", ToFinished);
    }

    private static async Task<IResult> ListGames(GameDbContext db, CancellationToken cancellationToken)
    {
        var games = await db.Games
            .AsNoTracking()
            .Include(g => g.Devices)
            .OrderByDescending(g => g.CreateTime)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(games.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetGame(int id, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await db.Games
            .AsNoTracking()
            .Include(g => g.Devices)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        return game is null ? TypedResults.NotFound() : TypedResults.Ok(ToDto(game));
    }

    private static async Task<IResult> CreateGame(CreateGameRequest request, GameDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var game = new Game
        {
            CreateTime = now,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = request.Status ?? GameStatus.Created
        };

        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/games/{game.Id}", ToDto(game));
    }

    private static async Task<IResult> RegisterDevice(int id, RegisterDeviceRequest request, GameDbContext db, CancellationToken cancellationToken)
    {
        var (device, error) = await GameDeviceRegistration.TryRegisterAsync(id, request.PlayerName, db, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var response = new RegisterDeviceResponse(device!.Id, id, device.PlayerName, device.AssignedDeviceId!.Value);
        return TypedResults.Created($"/api/games/{id}/devices/{device.Id}/player-info", response);
    }

    private static async Task<IResult> GetPlayerInfo(int id, int deviceId, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        if (game.Status is not (GameStatus.InProgress or GameStatus.Finished))
        {
            return TypedResults.BadRequest(new { message = "Player info is available after the game has started." });
        }

        var device = await db.GameDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.GameId == id, cancellationToken);

        if (device is null)
        {
            return TypedResults.NotFound();
        }

        if (device.AssignedDeviceId is null || device.CombatScore is null || device.EnemyFlagId is null)
        {
            return TypedResults.BadRequest(new { message = "Player info is not assigned for this device." });
        }

        var info = GamePlayerAssignment.ToPlayerInfo(device);
        return Results.Json(info, DeviceWireJson.Options);
    }

    private static async Task<IResult> ToWaitingForPlayers(int id, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        if (game.Status is not GameStatus.Created)
        {
            return TypedResults.BadRequest(new { message = "Game must be in Created status." });
        }

        game.Status = GameStatus.WaitingForPlayers;
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(await ReloadDtoAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> ToInProgress(int id, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await db.Games
            .Include(g => g.Devices)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (game is null)
        {
            return TypedResults.NotFound();
        }

        if (game.Status is not GameStatus.WaitingForPlayers)
        {
            return TypedResults.BadRequest(new { message = "Game must be in WaitingForPlayers status." });
        }

        var devices = game.Devices.ToList();
        if (devices.Count < 1)
        {
            return TypedResults.BadRequest(new { message = "At least one device must register before starting the game." });
        }

        try
        {
            GamePlayerAssignment.Assign(devices);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(new { message = ex.Message });
        }

        var now = DateTime.UtcNow;
        game.Status = GameStatus.InProgress;
        game.StartTime ??= now;

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(await ReloadDtoAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> ToFinished(int id, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        if (game.Status is not GameStatus.InProgress)
        {
            return TypedResults.BadRequest(new { message = "Game must be in InProgress status." });
        }

        var now = DateTime.UtcNow;
        game.Status = GameStatus.Finished;
        game.EndTime = now;

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(await ReloadDtoAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> DeleteGame(int id, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        db.Games.Remove(game);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<GameDto> ReloadDtoAsync(GameDbContext db, int id, CancellationToken cancellationToken)
    {
        var game = await db.Games
            .AsNoTracking()
            .Include(g => g.Devices)
            .FirstAsync(g => g.Id == id, cancellationToken);

        return ToDto(game);
    }

    private static GameDto ToDto(Game game)
    {
        var devices = game.Devices
            .OrderBy(d => d.AddedOn)
            .Select(d => new GameDeviceDto(
                d.Id,
                d.PlayerName,
                d.AddedOn,
                d.AssignedDeviceId,
                d.Team,
                d.CombatScore,
                d.EnemyFlagId))
            .ToList();

        return new GameDto(game.Id, game.CreateTime, game.StartTime, game.EndTime, game.Status, devices);
    }
}
