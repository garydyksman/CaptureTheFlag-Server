using CaptureTheFlag.Web.Contracts;
using CaptureTheFlag.Web.Data;
using CaptureTheFlag.Web.Enums;
using CaptureTheFlag.Web.Json;
using CaptureTheFlag.Web.Logging;
using CaptureTheFlag.Web.Models;
using CaptureTheFlag.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace CaptureTheFlag.Web.Endpoints;

/// <summary>
/// Single-session device API for ESP32 / nanoFramework <c>IGameHttpClient</c>.
/// JSON: UTF-8, camelCase, <c>GET /api/game</c> uses numeric <c>status</c> 0–3; <c>players</c> is always an array.
/// </summary>
public static class DeviceGameEndpoints
{
    public static void MapDeviceGameEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/game").WithTags("DeviceGame (ESP32)");

        group.MapGet(string.Empty, GetCurrentGame)
            .WithName("DeviceGame_GetGame")
            .WithSummary("Current game state for devices.")
            .Produces<GameInfo>(StatusCodes.Status200OK);

        group.MapPost("/register/flagnode", RegisterFlagnode)
            .WithName("DeviceGame_RegisterFlagnode")
            .WithSummary("Register a flagnode device by MAC address.")
            .Produces<PlayerSetup>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/register/player", RegisterPlayer)
            .WithName("DeviceGame_RegisterPlayer")
            .WithSummary("Register a player device by MAC address with optional player name.")
            .Produces<PlayerSetup>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/register", Register)
            .WithName("DeviceGame_Register")
            .WithSummary("LEGACY: Register in lobby, or reconnect during play: same playerName returns existing deviceId and saved scores.")
            .Produces<PlayerSetup>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/combat", ReportCombat)
            .WithName("DeviceGame_Combat")
            .WithSummary("Report combat outcome (fire-and-forget).")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/deliver", ReportDeliver)
            .WithName("DeviceGame_Deliver")
            .WithSummary("Deliver a key (Base64) to a device.")
            .Produces<DeliverAcceptedResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/respawn/{deviceId:int}", GetRespawnNumber)
            .WithName("DeviceGame_Respawn")
            .WithSummary("Respawn: returns updated combat score for a device.")
            .Produces<RespawnScoreResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetCurrentGame(
        GameDbContext db,
        ILogger<DeviceApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var open = await CurrentGameQuery.GetOpenAsync(db, cancellationToken);
        Game? lastFinished = null;
        if (open is null)
        {
            lastFinished = await CurrentGameQuery.GetLatestFinishedAsync(db, cancellationToken);
        }

        var snapshot = BuildGameInfo(open, lastFinished);
        var dbStatusLabel = open?.Status.ToString()
            ?? (lastFinished is not null ? $"EndedGame#{lastFinished.Id}" : "none");
        log.LogInformation(
            "DeviceApi GetGame OpenGameId={OpenGameId} DbStatus={DbStatus} SnapshotStatus={SnapshotStatus} PlayerCount={PlayerCount} TraceId={TraceId}",
            open?.Id,
            dbStatusLabel,
            snapshot.Status,
            snapshot.Players.Count,
            http.TraceIdentifier);

        return Results.Json(snapshot, DeviceWireJson.Options, contentType: "application/json; charset=utf-8");
    }

    /// <summary>Builds device contract: status 0–3, players never null.</summary>
    private static GameInfo BuildGameInfo(Game? open, Game? lastFinished)
    {
        if (open is not null)
        {
            return open.Status switch
            {
                GameStatus.Created => new GameInfo { Status = 0, Players = [] },
                GameStatus.WaitingForPlayers => new GameInfo { Status = 1, Players = MapPlayers(open.Devices) },
                GameStatus.InProgress => new GameInfo { Status = 2, Players = MapPlayers(open.Devices) },
                _ => new GameInfo { Status = 0, Players = [] }
            };
        }

        if (lastFinished is not null)
        {
            return new GameInfo { Status = 3, Players = MapPlayers(lastFinished.Devices) };
        }

        return new GameInfo { Status = 0, Players = [] };
    }

    private static List<PlayerInfo> MapPlayers(IEnumerable<GameDevice> devices) =>
        devices
            .Where(d => d.AssignedDeviceId.HasValue)
            .OrderBy(d => d.AssignedDeviceId)
            .Select(d => new PlayerInfo
            {
                DeviceId = d.AssignedDeviceId!.Value,
                Name = d.PlayerName,
                Team = d.Team ?? string.Empty,
                Type = d.DeviceType == DeviceType.FlagNode ? "flag" : "player",
                CombatScore = d.CombatScore ?? 0,
                EnemyFlagId = d.EnemyFlagId ?? 0
            })
            .ToList();

    private static async Task<IResult> RegisterFlagnode(
        RegisterFlagnodeRequest body,
        GameDbContext db,
        ILogger<DeviceApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await CurrentGameQuery.GetOpenAsync(db, cancellationToken);
        if (game is null)
        {
            log.LogWarning("DeviceApi RegisterFlagnode rejected: no open game TraceId={TraceId}", http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        var (device, error) = await GameDeviceRegistration.TryRegisterFlagnodeAsync(game.Id, body.MacAddress, db, cancellationToken);
        if (error is not null)
        {
            log.LogWarning(
                "DeviceApi RegisterFlagnode rejected GameId={GameId} MacAddress={MacAddress} TraceId={TraceId}",
                game.Id,
                body.MacAddress,
                http.TraceIdentifier);
            return error;
        }

        var setup = new PlayerSetup { DeviceId = device!.AssignedDeviceId!.Value };
        log.LogInformation(
            "DeviceApi RegisterFlagnode ok GameId={GameId} MacAddress={MacAddress} AssignedDeviceId={AssignedDeviceId} TraceId={TraceId}",
            game.Id,
            body.MacAddress,
            setup.DeviceId,
            http.TraceIdentifier);

        return Results.Json(setup, DeviceWireJson.Options, contentType: "application/json; charset=utf-8", statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> RegisterPlayer(
        RegisterPlayerRequest body,
        GameDbContext db,
        ILogger<DeviceApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await CurrentGameQuery.GetOpenAsync(db, cancellationToken);
        if (game is null)
        {
            log.LogWarning("DeviceApi RegisterPlayer rejected: no open game TraceId={TraceId}", http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        var (device, error) = await GameDeviceRegistration.TryRegisterPlayerAsync(game.Id, body.MacAddress, body.PlayerName, db, cancellationToken);
        if (error is not null)
        {
            log.LogWarning(
                "DeviceApi RegisterPlayer rejected GameId={GameId} MacAddress={MacAddress} PlayerName={PlayerName} TraceId={TraceId}",
                game.Id,
                body.MacAddress,
                body.PlayerName,
                http.TraceIdentifier);
            return error;
        }

        var setup = new PlayerSetup { DeviceId = device!.AssignedDeviceId!.Value };
        log.LogInformation(
            "DeviceApi RegisterPlayer ok GameId={GameId} MacAddress={MacAddress} PlayerName={PlayerName} AssignedDeviceId={AssignedDeviceId} TraceId={TraceId}",
            game.Id,
            body.MacAddress,
            device.PlayerName,
            setup.DeviceId,
            http.TraceIdentifier);

        return Results.Json(setup, DeviceWireJson.Options, contentType: "application/json; charset=utf-8", statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> Register(
        RegisterPlayerNameRequest body,
        GameDbContext db,
        ILogger<DeviceApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await CurrentGameQuery.GetOpenAsync(db, cancellationToken);
        if (game is null)
        {
            log.LogWarning("DeviceApi Register rejected: no open game TraceId={TraceId}", http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        var phaseBefore = game.Status;
        var (device, error) = await GameDeviceRegistration.TryRegisterAsync(game.Id, body.PlayerName, db, cancellationToken);
        if (error is not null)
        {
            log.LogWarning(
                "DeviceApi Register rejected GameId={GameId} Phase={Phase} PlayerName={PlayerName} TraceId={TraceId}",
                game.Id,
                phaseBefore,
                body.PlayerName,
                http.TraceIdentifier);
            return error;
        }

        var setup = new PlayerSetup { DeviceId = device!.AssignedDeviceId!.Value };
        var reconnect = phaseBefore == GameStatus.InProgress;
        log.LogInformation(
            "DeviceApi Register ok GameId={GameId} Phase={Phase} Reconnect={Reconnect} PlayerName={PlayerName} AssignedDeviceId={AssignedDeviceId} TraceId={TraceId}",
            game.Id,
            phaseBefore,
            reconnect,
            body.PlayerName,
            setup.DeviceId,
            http.TraceIdentifier);

        return Results.Json(setup, DeviceWireJson.Options, contentType: "application/json; charset=utf-8", statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> ReportCombat(
        CombatReportRequest body,
        GameDbContext db,
        ILogger<DeviceApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await db.Games
            .Include(g => g.Devices)
            .Where(g => g.Status != GameStatus.Finished)
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null || game.Status is not GameStatus.InProgress)
        {
            log.LogWarning(
                "DeviceApi Combat rejected GameId={GameId} Status={Status} WinnerId={WinnerId} LoserId={LoserId} TraceId={TraceId}",
                game?.Id,
                game?.Status.ToString() ?? "null",
                body.WinnerId,
                body.LoserId,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "Combat can only be reported while a game is Active (status 2)." });
        }

        var winner = game.Devices.FirstOrDefault(d => d.AssignedDeviceId == body.WinnerId);
        var loser = game.Devices.FirstOrDefault(d => d.AssignedDeviceId == body.LoserId);
        if (winner is null || loser is null)
        {
            log.LogWarning(
                "DeviceApi Combat rejected unknown devices GameId={GameId} WinnerId={WinnerId} LoserId={LoserId} TraceId={TraceId}",
                game.Id,
                body.WinnerId,
                body.LoserId,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "winnerId and loserId must match registered device ids in the current game." });
        }

        var win = winner.CombatScore;
        var loserDrop = body.LoserHadKey ? 2 : 1;
        var lose = loser.CombatScore;
        var accumulatedCombat = winner.CombatReportScore + 1;
        winner.CombatReportScore = accumulatedCombat;

        await db.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "DeviceApi Combat ok GameId={GameId} WinnerId={WinnerId} LoserId={LoserId} LoserHadKey={LoserHadKey} NewWinnerScore={NewWinnerScore} NewLoserScore={NewLoserScore} WinnerAccumulatedCombat={WinnerAccumulatedCombat} TraceId={TraceId}",
            game.Id,
            body.WinnerId,
            body.LoserId,
            body.LoserHadKey,
            win,
            lose,
            accumulatedCombat,
            http.TraceIdentifier);

        return TypedResults.Ok();
    }

    private static async Task<IResult> ReportDeliver(
        DeliverReportRequest body,
        GameDbContext db,
        ILogger<DeviceApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var game = await CurrentGameQuery.GetOpenAsync(db, cancellationToken);
        if (game is null || game.Status is not GameStatus.InProgress)
        {
            log.LogInformation(
                "DeviceApi Deliver not active GameId={GameId} Status={Status} TargetDeviceId={TargetDeviceId} HasKey={HasKey} TraceId={TraceId}",
                game?.Id,
                game?.Status.ToString() ?? "null",
                body.DeviceId,
                !string.IsNullOrWhiteSpace(body.Key),
                http.TraceIdentifier);
            return Results.Json(
                new DeliverAcceptedResponse { Accepted = false },
                DeviceWireJson.Options,
                contentType: "application/json; charset=utf-8");
        }

        if (!string.IsNullOrWhiteSpace(body.Key))
        {
            try
            {
                _ = Convert.FromBase64String(body.Key);
            }
            catch (FormatException)
            {
                log.LogWarning(
                    "DeviceApi Deliver invalid Base64 GameId={GameId} TargetDeviceId={TargetDeviceId} TraceId={TraceId}",
                    game.Id,
                    body.DeviceId,
                    http.TraceIdentifier);
                return TypedResults.BadRequest(new { message = "key must be valid Base64 when provided." });
            }
        }

        var exists = await db.GameDevices.AnyAsync(
            d => d.GameId == game.Id && d.AssignedDeviceId == body.DeviceId,
            cancellationToken);

        var accepted = exists;
        log.LogInformation(
            "DeviceApi Deliver GameId={GameId} TargetDeviceId={TargetDeviceId} Accepted={Accepted} HasKey={HasKey} TraceId={TraceId}",
            game.Id,
            body.DeviceId,
            accepted,
            !string.IsNullOrWhiteSpace(body.Key),
            http.TraceIdentifier);

        return Results.Json(
            new DeliverAcceptedResponse { Accepted = accepted },
            DeviceWireJson.Options,
            contentType: "application/json; charset=utf-8");
    }

    private static async Task<IResult> GetRespawnNumber(
        int deviceId,
        GameDbContext db,
        ILogger<DeviceApiLog> log,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (deviceId is < 0 or > 255)
        {
            log.LogWarning("DeviceApi Respawn rejected bad deviceId={DeviceId} TraceId={TraceId}", deviceId, http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "deviceId must be between 0 and 255." });
        }

        if (deviceId is 0)
        {
            log.LogWarning("DeviceApi Respawn rejected deviceId=0 TraceId={TraceId}", http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "deviceId 0 is not used for registered devices." });
        }

        var game = await db.Games
            .Include(g => g.Devices)
            .Where(g => g.Status != GameStatus.Finished)
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null || game.Status is not GameStatus.InProgress)
        {
            log.LogWarning(
                "DeviceApi Respawn rejected not active GameId={GameId} Status={Status} DeviceId={DeviceId} TraceId={TraceId}",
                game?.Id,
                game?.Status.ToString() ?? "null",
                deviceId,
                http.TraceIdentifier);
            return TypedResults.BadRequest(new { message = "Respawn is only available while a game is Active (status 2)." });
        }

        var device = game.Devices.FirstOrDefault(d => d.AssignedDeviceId == (byte)deviceId);
        if (device is null)
        {
            log.LogWarning(
                "DeviceApi Respawn not found GameId={GameId} DeviceId={DeviceId} TraceId={TraceId}",
                game.Id,
                deviceId,
                http.TraceIdentifier);
            return TypedResults.NotFound();
        }

        var score = (byte)Random.Shared.Next(1, 10);
        device.CombatScore = score;
        await db.SaveChangesAsync(cancellationToken);

        log.LogInformation(
            "DeviceApi Respawn ok GameId={GameId} DeviceId={DeviceId} NewCombatScore={NewCombatScore} TraceId={TraceId}",
            game.Id,
            deviceId,
            score,
            http.TraceIdentifier);

        var response = new RespawnScoreResponse { DeviceId = (byte)deviceId, CombatScore = score };
        return Results.Json(response, DeviceWireJson.Options, contentType: "application/json; charset=utf-8");
    }
}
