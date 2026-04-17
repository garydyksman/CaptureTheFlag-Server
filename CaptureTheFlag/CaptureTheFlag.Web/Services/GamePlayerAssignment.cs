using CaptureTheFlag.Web.Contracts;
using CaptureTheFlag.Web.Models;

namespace CaptureTheFlag.Web.Services;

public static class GamePlayerAssignment
{
    public const byte RedFlagId = 1;
    public const byte BlueFlagId = 2;

    /// <summary>
    /// Assigns protocol device ids, teams, combat scores (1–10 random per player), and enemy flag ids. Mutates entities in memory.
    /// </summary>
    /// <exception cref="InvalidOperationException">More than 254 devices.</exception>
    public static void Assign(IReadOnlyList<GameDevice> devicesOrderedByJoin)
    {
        var ordered = devicesOrderedByJoin.OrderBy(d => d.AddedOn).ThenBy(d => d.Id).ToList();
        if (ordered.Count > 254)
        {
            throw new InvalidOperationException("A game supports at most 254 devices.");
        }

        var n = ordered.Count;
        var redCount = (n + 1) / 2;

        for (var i = 0; i < n; i++)
        {
            var d = ordered[i];
            d.AssignedDeviceId ??= (byte)(i + 1);
            d.Team = i < redCount ? "Red" : "Blue";
            d.CombatScore = (byte)Random.Shared.Next(1, 11);
            d.EnemyFlagId = d.Team == "Red" ? BlueFlagId : RedFlagId;
        }
    }

    public static PlayerInfo ToPlayerInfo(GameDevice d) =>
        new()
        {
            DeviceId = d.AssignedDeviceId!.Value,
            Name = d.PlayerName,
            Team = d.Team ?? string.Empty,
            CombatScore = d.CombatScore!.Value,
            EnemyFlagId = d.EnemyFlagId!.Value
        };
}
