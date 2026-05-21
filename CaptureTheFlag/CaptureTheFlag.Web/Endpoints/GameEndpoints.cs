using CaptureTheFlag.Web.Contracts;
using CaptureTheFlag.Web.Data;
using CaptureTheFlag.Web.Enums;
using CaptureTheFlag.Web.Json;
using CaptureTheFlag.Web.Logging;
using CaptureTheFlag.Web.Models;
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
        group.MapDelete("/{id:int}/devices/{deviceId:int}", RemoveLobbyDevice);
        group.MapGet("/{id:int}/devices/{deviceId:int}/player-info", GetPlayerInfo);

        var status = group.MapGroup("/{id:int}/status").WithTags("Games");
        status.MapPost("/waiting-for-players", ToWaitingForPlayers);
        status.MapPost("/in-progress", ToInProgress);
        status.MapPost("/finished", ToFinished);
    }

    private static async Task<IResult> ListGames(GameDbContext db, ILogger<GamesApiLog> log, HttpContext http, CancellationToken cancellationToken)
    {
        var games = await db.Games
            .AsNoTracking()
            .Include(g => g.Devices)
            .OrderByDescending(g => g.CreateTime)
            .ToListAsync(cancellationToken);

        log.LogInformation(
            "GamesApi ListGames Count={Count} TraceId={TraceId}",
            games.Count,
            http.TraceIdentifier);

        return TypedResults.Ok(games.Select(ToDto).ToList());
    }

    private static async Task<IResult> GetGame(int id, GameDbContext db, ILogger<GamesApiLog> log, HttpContext http, CancellationToken cancellationToken)
    {
        var game = await db.Games
            .AsNoTracking()
            .Include(g => g.Devices)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (game is null)
        {
            log.LogWarning("GamesApi GetGame not found GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        log.LogInformation(
            "GamesApi GetGame GameId={GameId} Status={Status} DeviceCount={DeviceCount} TraceId={TraceId}",
            id,
            game.Status,
            game.Devices.Count,
            http.TraceIdentifier);

        return TypedResults.Ok(ToDto(game));
    }

    private static async Task<IResult> CreateGame(
        CreateGameRequest request,
        GameDbContext db,
        ILogger<GamesApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
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

        log.LogInformation(
            "GamesApi CreateGame GameId={GameId} Status={Status} TraceId={TraceId}",
            game.Id,
            game.Status,
            http.TraceIdentifier);

        return TypedResults.Created($"/api/games/{game.Id}", ToDto(game));
    }

    private static async Task<IResult> RegisterDevice(
        int id,
        RegisterDeviceRequest request,
        GameDbContext db,
        ILogger<GamesApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var (device, error) = await GameDeviceRegistration.TryRegisterAsync(id, request.PlayerName, db, cancellationToken);
        if (error is not null)
        {
            log.LogWarning(
                "GamesApi RegisterDevice rejected GameId={GameId} PlayerName={PlayerName} TraceId={TraceId}",
                id,
                request.PlayerName,
                http.TraceIdentifier);
            return error;
        }

        log.LogInformation(
            "GamesApi RegisterDevice GameId={GameId} DbDeviceId={DbDeviceId} AssignedDeviceId={AssignedDeviceId} PlayerName={PlayerName} TraceId={TraceId}",
            id,
            device!.Id,
            device.AssignedDeviceId,
            device.PlayerName,
            http.TraceIdentifier);

        var response = new RegisterDeviceResponse(device.Id, id, device.PlayerName, device.AssignedDeviceId!.Value);
        return TypedResults.Created($"/api/games/{id}/devices/{device.Id}/player-info", response);
    }

    private static async Task<IResult> RemoveLobbyDevice(
        int id,
        int deviceId,
        GameDbContext db,
        ILogger<GamesApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            log.LogWarning(
                "GamesApi RemoveLobbyDevice game not found GameId={GameId} DbDeviceId={DbDeviceId} TraceId={TraceId}",
                id,
                deviceId,
                http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        if (game.Status is not GameStatus.WaitingForPlayers)
        {
            log.LogWarning(
                "GamesApi RemoveLobbyDevice wrong phase GameId={GameId} Status={Status} DbDeviceId={DbDeviceId} TraceId={TraceId}",
                id,
                game.Status,
                deviceId,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "Players can only be removed while the lobby is open (WaitingForPlayers)." });
        }

        var device = await db.GameDevices.FirstOrDefaultAsync(
            d => d.Id == deviceId && d.GameId == id,
            cancellationToken);

        if (device is null)
        {
            log.LogWarning(
                "GamesApi RemoveLobbyDevice device not found GameId={GameId} DbDeviceId={DbDeviceId} TraceId={TraceId}",
                id,
                deviceId,
                http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        db.GameDevices.Remove(device);
        await db.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "GamesApi RemoveLobbyDevice GameId={GameId} DbDeviceId={DbDeviceId} PlayerName={PlayerName} TraceId={TraceId}",
            id,
            deviceId,
            device.PlayerName,
            http.TraceIdentifier);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetPlayerInfo(
        int id,
        int deviceId,
        GameDbContext db,
        ILogger<GamesApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await db.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            log.LogWarning(
                "GamesApi GetPlayerInfo game not found GameId={GameId} DbDeviceId={DbDeviceId} TraceId={TraceId}",
                id,
                deviceId,
                http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        if (game.Status is not (GameStatus.InProgress or GameStatus.Finished))
        {
            log.LogWarning(
                "GamesApi GetPlayerInfo wrong phase GameId={GameId} Status={Status} TraceId={TraceId}",
                id,
                game.Status,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "Player info is available after the game has started." });
        }

        var device = await db.GameDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == deviceId && d.GameId == id, cancellationToken);

        if (device is null)
        {
            log.LogWarning(
                "GamesApi GetPlayerInfo device not found GameId={GameId} DbDeviceId={DbDeviceId} TraceId={TraceId}",
                id,
                deviceId,
                http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        if (device.AssignedDeviceId is null || device.CombatScore is null || device.EnemyFlagId is null)
        {
            log.LogWarning(
                "GamesApi GetPlayerInfo not assigned GameId={GameId} DbDeviceId={DbDeviceId} TraceId={TraceId}",
                id,
                deviceId,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "Player info is not assigned for this device." });
        }

        var info = GamePlayerAssignment.ToPlayerInfo(device);
        log.LogInformation(
            "GamesApi GetPlayerInfo GameId={GameId} DbDeviceId={DbDeviceId} WireDeviceId={WireDeviceId} TraceId={TraceId}",
            id,
            deviceId,
            info.DeviceId,
            http.TraceIdentifier);

        return Results.Json(info, DeviceWireJson.Options);
    }

    private static async Task<IResult> ToWaitingForPlayers(
        int id,
        GameDbContext db,
        ILogger<GamesApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            log.LogWarning("GamesApi ToWaitingForPlayers not found GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        if (game.Status is not GameStatus.Created)
        {
            log.LogWarning(
                "GamesApi ToWaitingForPlayers bad status GameId={GameId} Status={Status} TraceId={TraceId}",
                id,
                game.Status,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "Game must be in Created status." });
        }

        game.Status = GameStatus.WaitingForPlayers;
        await db.SaveChangesAsync(cancellationToken);

        log.LogInformation("GamesApi ToWaitingForPlayers GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);

        return TypedResults.Ok(await ReloadDtoAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> ToInProgress(
        int id,
        GameDbContext db,
        ILogger<GamesApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await db.Games
            .Include(g => g.Devices)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

        if (game is null)
        {
            log.LogWarning("GamesApi ToInProgress not found GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        if (game.Status is not GameStatus.WaitingForPlayers)
        {
            log.LogWarning(
                "GamesApi ToInProgress bad status GameId={GameId} Status={Status} TraceId={TraceId}",
                id,
                game.Status,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "Game must be in WaitingForPlayers status." });
        }

        var devices = game.Devices.ToList();
        if (devices.Count < 1)
        {
            log.LogWarning("GamesApi ToInProgress no devices GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "At least one device must register before starting the game." });
        }

        try
        {
            GamePlayerAssignment.Assign(devices);
        }
        catch (InvalidOperationException ex)
        {
            log.LogWarning(ex, "GamesApi ToInProgress assign failed GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = ex.Message });
        }

        var now = DateTime.UtcNow;
        game.Status = GameStatus.InProgress;
        game.StartTime ??= now;

        await db.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "GamesApi ToInProgress GameId={GameId} PlayerCount={PlayerCount} TraceId={TraceId}",
            id,
            devices.Count,
            http.TraceIdentifier);

        return TypedResults.Ok(await ReloadDtoAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> ToFinished(
        int id,
        GameDbContext db,
        ILogger<GamesApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            log.LogWarning("GamesApi ToFinished not found GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        if (game.Status is not GameStatus.InProgress)
        {
            log.LogWarning(
                "GamesApi ToFinished bad status GameId={GameId} Status={Status} TraceId={TraceId}",
                id,
                game.Status,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "Game must be in InProgress status." });
        }

        var now = DateTime.UtcNow;
        game.Status = GameStatus.Finished;
        game.EndTime = now;

        await db.SaveChangesAsync(cancellationToken);

        log.LogInformation("GamesApi ToFinished GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);

        return TypedResults.Ok(await ReloadDtoAsync(db, id, cancellationToken));
    }

    private static async Task<IResult> DeleteGame(int id, GameDbContext db, ILogger<GamesApiLog> log, HttpContext http, CancellationToken cancellationToken)
    {
        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (game is null)
        {
            log.LogWarning("GamesApi DeleteGame not found GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        db.Games.Remove(game);
        await db.SaveChangesAsync(cancellationToken);

        log.LogInformation("GamesApi DeleteGame GameId={GameId} TraceId={TraceId}", id, http.TraceIdentifier);

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
                d.CombatReportScore,
                d.EnemyFlagId))
            .ToList();

        return new GameDto(game.Id, game.CreateTime, game.StartTime, game.EndTime, game.Status, devices);
    }
}
