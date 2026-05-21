using CaptureTheFlag.Web.Data;
using CaptureTheFlag.Web.Enums;
using CaptureTheFlag.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CaptureTheFlag.Web.Services;

public static class GameDeviceRegistration
{
    /// <summary>
    /// Registers in <see cref="GameStatus.WaitingForPlayers"/> (new device id). During <see cref="GameStatus.InProgress"/>,
    /// the same <see cref="GameDevice.PlayerName"/> reconnects: returns the existing assignment and persisted scores (no new row).
    /// </summary>
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
