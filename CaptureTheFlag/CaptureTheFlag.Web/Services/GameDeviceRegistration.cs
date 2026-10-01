using CaptureTheFlag.Web.Data;
using CaptureTheFlag.Web.Enums;
using CaptureTheFlag.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CaptureTheFlag.Web.Services;

public static class GameDeviceRegistration
{
    private static readonly string[] TeamColors = ["Red", "Blue", "Green", "Yellow", "Orange", "Purple", "Pink", "Cyan"];

    /// <summary>
    /// Register a flagnode device. Assigns in lobby; returns existing if MAC already registered (supports reconnection).
    /// Flagnodes are assigned deviceIds 200-255.
    /// </summary>
    public static async Task<(GameDevice? Device, IResult? Error)> TryRegisterFlagnodeAsync(
        int gameId,
        string macAddress,
        GameDbContext db,
        CancellationToken cancellationToken)
    {
        var mac = macAddress.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(mac))
        {
            return (null, TypedResults.BadRequest(new { message = "MacAddress is required." }));
        }

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null)
        {
            return (null, TypedResults.NotFound());
        }

        var existing = await db.GameDevices.FirstOrDefaultAsync(
            d => d.GameId == gameId && d.MacAddress == mac,
            cancellationToken);

        if (existing is not null)
        {
            return (existing, null);
        }

        if (game.Status == GameStatus.InProgress)
        {
            return (null, TypedResults.Conflict(new { message = "Cannot register a new flagnode while the game is active." }));
        }

        if (game.Status is not GameStatus.WaitingForPlayers)
        {
            return (null, TypedResults.Conflict(new { message = "Flagnode registration is only allowed while the game is in Waiting (lobby open)." }));
        }

        var flagnodeCount = await db.GameDevices.CountAsync(
            d => d.GameId == gameId && d.DeviceType == DeviceType.FlagNode,
            cancellationToken);

        if (flagnodeCount >= 8)
        {
            return (null, TypedResults.BadRequest(new { message = "Maximum 8 flagnodes per game." }));
        }

        var assignedIds = await db.GameDevices
            .Where(d => d.GameId == gameId && d.DeviceType == DeviceType.FlagNode && d.AssignedDeviceId != null)
            .Select(d => (int)d.AssignedDeviceId!.Value)
            .ToListAsync(cancellationToken);

        var nextAssigned = assignedIds.Count == 0 ? 200 : assignedIds.Max() + 1;
        if (nextAssigned > 255)
        {
            return (null, TypedResults.BadRequest(new { message = "No available flagnode device ids (200-255)." }));
        }

        var device = new GameDevice
        {
            GameId = gameId,
            MacAddress = mac,
            DeviceType = DeviceType.FlagNode,
            PlayerName = $"Flagnode {nextAssigned}", // Temp name, will be updated on game start with team color
            AddedOn = DateTime.UtcNow,
            AssignedDeviceId = (byte)nextAssigned
        };

        db.GameDevices.Add(device);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (null, TypedResults.Conflict(new { message = "Duplicate MAC address." }));
        }

        return (device, null);
    }

    /// <summary>
    /// Register a player device. Assigns in lobby; returns existing if MAC already registered (supports reconnection).
    /// Players are assigned deviceIds 1-199.
    /// </summary>
    public static async Task<(GameDevice? Device, IResult? Error)> TryRegisterPlayerAsync(
        int gameId,
        string macAddress,
        string? playerNameRaw,
        GameDbContext db,
        CancellationToken cancellationToken)
    {
        var mac = macAddress.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(mac))
        {
            return (null, TypedResults.BadRequest(new { message = "MacAddress is required." }));
        }

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null)
        {
            return (null, TypedResults.NotFound());
        }

        var existing = await db.GameDevices.FirstOrDefaultAsync(
            d => d.GameId == gameId && d.MacAddress == mac,
            cancellationToken);

        if (existing is not null)
        {
            return (existing, null);
        }

        if (game.Status == GameStatus.InProgress)
        {
            return (null, TypedResults.Conflict(new { message = "Cannot register a new player while the game is active." }));
        }

        if (game.Status is not GameStatus.WaitingForPlayers)
        {
            return (null, TypedResults.Conflict(new { message = "Player registration is only allowed while the game is in Waiting (lobby open)." }));
        }

        var playerCount = await db.GameDevices.CountAsync(
            d => d.GameId == gameId && d.DeviceType == DeviceType.Player,
            cancellationToken);

        if (playerCount >= 199)
        {
            return (null, TypedResults.BadRequest(new { message = "Maximum 199 players per game." }));
        }

        var assignedIds = await db.GameDevices
            .Where(d => d.GameId == gameId && d.DeviceType == DeviceType.Player && d.AssignedDeviceId != null)
            .Select(d => (int)d.AssignedDeviceId!.Value)
            .ToListAsync(cancellationToken);

        var nextAssigned = assignedIds.Count == 0 ? 1 : assignedIds.Max() + 1;
        if (nextAssigned > 199)
        {
            return (null, TypedResults.BadRequest(new { message = "No available player device ids (1-199)." }));
        }

        var name = string.IsNullOrWhiteSpace(playerNameRaw)
            ? $"Player {nextAssigned}"
            : playerNameRaw.Trim();

        if (name.Length > 200)
        {
            return (null, TypedResults.BadRequest(new { message = "PlayerName must be 200 characters or fewer." }));
        }

        var device = new GameDevice
        {
            GameId = gameId,
            MacAddress = mac,
            DeviceType = DeviceType.Player,
            PlayerName = name,
            AddedOn = DateTime.UtcNow,
            AssignedDeviceId = (byte)nextAssigned
        };

        db.GameDevices.Add(device);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (null, TypedResults.Conflict(new { message = "Duplicate MAC address." }));
        }

        return (device, null);
    }

    /// <summary>
    /// LEGACY: Registers in <see cref="GameStatus.WaitingForPlayers"/> (new device id). During <see cref="GameStatus.InProgress"/>,
    /// the same <see cref="GameDevice.PlayerName"/> reconnects: returns the existing assignment and persisted scores (no new row).
    /// </summary>
    [Obsolete("Use TryRegisterPlayerAsync or TryRegisterFlagnodeAsync instead")]
    public static async Task<(GameDevice? Device, IResult? Error)> TryRegisterAsync(
        int gameId,
        string? playerNameRaw,
        GameDbContext db,
        CancellationToken cancellationToken)
    {
        var name = (playerNameRaw ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return (null, TypedResults.BadRequest(new { message = "PlayerName is required." }));
        }

        if (name.Length > 200)
        {
            return (null, TypedResults.BadRequest(new { message = "PlayerName must be 200 characters or fewer." }));
        }

        var game = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken);
        if (game is null)
        {
            return (null, TypedResults.NotFound());
        }

        if (game.Status == GameStatus.InProgress)
        {
            var existing = await db.GameDevices.FirstOrDefaultAsync(
                d => d.GameId == gameId && d.PlayerName == name,
                cancellationToken);

            if (existing is null)
            {
                return (null, TypedResults.Conflict(new { message = "Cannot register a new player while the game is active." }));
            }

            if (!existing.AssignedDeviceId.HasValue)
            {
                return (null, TypedResults.Conflict(new { message = "Player record is missing device assignment." }));
            }

            return (existing, null);
        }

        if (game.Status is not GameStatus.WaitingForPlayers)
        {
            return (null, TypedResults.Conflict(new { message = "Registration is only allowed while the game is in Waiting (lobby open)." }));
        }

        var duplicate = await db.GameDevices.AnyAsync(d => d.GameId == gameId && d.PlayerName == name, cancellationToken);
        if (duplicate)
        {
            return (null, TypedResults.Conflict(new { message = "A device with this player name is already registered in the game." }));
        }

        var count = await db.GameDevices.CountAsync(d => d.GameId == gameId, cancellationToken);
        if (count >= 254)
        {
            return (null, TypedResults.BadRequest(new { message = "This game already has the maximum number of devices (254)." }));
        }

        var assignedIds = await db.GameDevices
            .Where(d => d.GameId == gameId && d.AssignedDeviceId != null)
            .Select(d => (int)d.AssignedDeviceId!.Value)
            .ToListAsync(cancellationToken);
        var nextAssigned = assignedIds.Count == 0 ? 1 : assignedIds.Max() + 1;
        if (nextAssigned > 254)
        {
            return (null, TypedResults.BadRequest(new { message = "No available device ids (max 254)." }));
        }

        var device = new GameDevice
        {
            GameId = gameId,
            MacAddress = "LEGACY",
            DeviceType = DeviceType.Player,
            PlayerName = name,
            AddedOn = DateTime.UtcNow,
            AssignedDeviceId = (byte)nextAssigned
        };

        db.GameDevices.Add(device);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (null, TypedResults.Conflict(new { message = "Duplicate registration." }));
        }

        return (device, null);
    }
}
