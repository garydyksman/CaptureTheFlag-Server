using CaptureTheFlag.Web.Contracts;
using CaptureTheFlag.Web.Data;
using CaptureTheFlag.Web.Enums;
using CaptureTheFlag.Web.Json;
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

        group.MapPost("/register", Register)
            .WithName("DeviceGame_Register")
            .WithSummary("Register a player name; returns assigned deviceId (1–255).")
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

    private static async Task<IResult> GetCurrentGame(GameDbContext db, CancellationToken cancellationToken)
    {
        var open = await CurrentGameQuery.GetOpenAsync(db, cancellationToken);
        Game? lastFinished = null;
        if (open is null)
        {
            lastFinished = await CurrentGameQuery.GetLatestFinishedAsync(db, cancellationToken);
        }

        var snapshot = BuildGameInfo(open, lastFinished);
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
                CombatScore = d.CombatScore ?? 0,
                EnemyFlagId = d.EnemyFlagId ?? 0
            })
            .ToList();

    private static async Task<IResult> Register(RegisterPlayerNameRequest body, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await CurrentGameQuery.GetOpenAsync(db, cancellationToken);
        if (game is null)
        {
            return TypedResults.NotFound();
        }

        var (device, error) = await GameDeviceRegistration.TryRegisterAsync(game.Id, body.PlayerName, db, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var setup = new PlayerSetup { DeviceId = device!.AssignedDeviceId!.Value };
        return Results.Json(setup, DeviceWireJson.Options, contentType: "application/json; charset=utf-8", statusCode: StatusCodes.Status200OK);
    }

    private static async Task<IResult> ReportCombat(CombatReportRequest body, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await db.Games
            .Include(g => g.Devices)
            .Where(g => g.Status != GameStatus.Finished)
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null || game.Status is not GameStatus.InProgress)
        {
            return TypedResults.BadRequest(new { message = "Combat can only be reported while a game is Active (status 2)." });
        }

        var winner = game.Devices.FirstOrDefault(d => d.AssignedDeviceId == body.WinnerId);
        var loser = game.Devices.FirstOrDefault(d => d.AssignedDeviceId == body.LoserId);
        if (winner is null || loser is null)
        {
            return TypedResults.BadRequest(new { message = "winnerId and loserId must match registered device ids in the current game." });
        }

        var win = Math.Min(255, (winner.CombatScore ?? 0) + 1);
        var loserDrop = body.LoserHadKey ? 2 : 1;
        var lose = Math.Max(0, (loser.CombatScore ?? 0) - loserDrop);
        winner.CombatScore = (byte)win;
        loser.CombatScore = (byte)lose;

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok();
    }

    private static async Task<IResult> ReportDeliver(DeliverReportRequest body, GameDbContext db, CancellationToken cancellationToken)
    {
        var game = await CurrentGameQuery.GetOpenAsync(db, cancellationToken);
        if (game is null || game.Status is not GameStatus.InProgress)
        {
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
                return TypedResults.BadRequest(new { message = "key must be valid Base64 when provided." });
            }
        }

        var exists = await db.GameDevices.AnyAsync(
            d => d.GameId == game.Id && d.AssignedDeviceId == body.DeviceId,
            cancellationToken);

        var accepted = exists;
        return Results.Json(
            new DeliverAcceptedResponse { Accepted = accepted },
            DeviceWireJson.Options,
            contentType: "application/json; charset=utf-8");
    }

    private static async Task<IResult> GetRespawnNumber(int deviceId, GameDbContext db, CancellationToken cancellationToken)
    {
        if (deviceId is < 0 or > 255)
        {
            return TypedResults.BadRequest(new { message = "deviceId must be between 0 and 255." });
        }

        if (deviceId is 0)
        {
            return TypedResults.BadRequest(new { message = "deviceId 0 is not used for registered devices." });
        }

        var game = await db.Games
            .Include(g => g.Devices)
            .Where(g => g.Status != GameStatus.Finished)
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null || game.Status is not GameStatus.InProgress)
        {
            return TypedResults.BadRequest(new { message = "Respawn is only available while a game is Active (status 2)." });
        }

        var device = game.Devices.FirstOrDefault(d => d.AssignedDeviceId == (byte)deviceId);
        if (device is null)
        {
            return TypedResults.NotFound();
        }

        var score = (byte)Random.Shared.Next(1, 11);
        device.CombatScore = score;
        await db.SaveChangesAsync(cancellationToken);

        var response = new RespawnScoreResponse { DeviceId = (byte)deviceId, CombatScore = score };
        return Results.Json(response, DeviceWireJson.Options, contentType: "application/json; charset=utf-8");
    }
}
