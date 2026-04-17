using CaptureTheFlag.Web.Data;
using CaptureTheFlag.Web.Enums;
using CaptureTheFlag.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CaptureTheFlag.Web.Services;

/// <summary>
/// Hackathon: treat the app as having a single session. The <b>open</b> game is the newest row not in
/// <see cref="GameStatus.Finished"/>; for display, fall back to the latest row so <c>Finished</c> is visible.
/// </summary>
public static class CurrentGameQuery
{
    public static async Task<Game?> GetOpenAsync(GameDbContext db, CancellationToken cancellationToken) =>
        await db.Games
            .AsNoTracking()
            .Include(g => g.Devices)
            .Where(g => g.Status != GameStatus.Finished)
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public static async Task<Game?> GetDisplayAsync(GameDbContext db, CancellationToken cancellationToken)
    {
        var open = await GetOpenAsync(db, cancellationToken);
        if (open is not null)
        {
            return open;
        }

        return await db.Games
            .AsNoTracking()
            .Include(g => g.Devices)
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Most recently finished game (for device API status 3).</summary>
    public static async Task<Game?> GetLatestFinishedAsync(GameDbContext db, CancellationToken cancellationToken) =>
        await db.Games
            .AsNoTracking()
            .Include(g => g.Devices)
            .Where(g => g.Status == GameStatus.Finished)
            .OrderByDescending(g => g.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
