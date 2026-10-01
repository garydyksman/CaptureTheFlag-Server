using CaptureTheFlag.Web.Contracts;
using CaptureTheFlag.Web.Enums;
using CaptureTheFlag.Web.Models;
using Microsoft.Extensions.Logging;

namespace CaptureTheFlag.Web.Services;

public static class GamePlayerAssignment
{
    private static readonly string[] TeamColors = ["Red", "Blue", "Green", "Yellow", "Orange", "Purple", "Pink", "Cyan"];

    /// <summary>
    /// Assigns teams based on registered flagnodes, distributes players across teams, assigns combat scores.
    /// If no flagnodes: creates 2 teams (Red/Blue) and distributes players evenly.
    /// If flagnodes exist: teams are based on flagnode count, players distributed round-robin.
    /// </summary>
    /// <exception cref="InvalidOperationException">More than 254 devices or no players registered.</exception>
    public static void Assign(IReadOnlyList<GameDevice> devices)
    {
        if (devices.Count > 254)
        {
            throw new InvalidOperationException("A game supports at most 254 devices.");
        }

        var flagnodes = devices.Where(d => d.DeviceType == DeviceType.FlagNode).OrderBy(d => d.AssignedDeviceId).ToList();
        var players = devices.Where(d => d.DeviceType == DeviceType.Player).OrderBy(d => d.AddedOn).ThenBy(d => d.Id).ToList();

        if (players.Count == 0)
        {
            throw new InvalidOperationException("At least one player must be registered before starting the game.");
        }

        // Determine team setup
        string[] activeTeams;
        Console.WriteLine($"[DEBUG] Flagnodes: {flagnodes.Count}, Players: {players.Count}");
        if (flagnodes.Count == 0)
        {
            // No flagnodes: use 2 teams (Red/Blue) to ensure players can compete
            activeTeams = ["Red", "Blue"];
            Console.WriteLine("[DEBUG] No flagnodes - using Red vs Blue teams");
        }
        else
        {
            // Assign team colors to flagnodes
            activeTeams = new string[flagnodes.Count];
            for (var i = 0; i < flagnodes.Count; i++)
            {
                var teamColor = TeamColors[i % TeamColors.Length];
                var flagnode = flagnodes[i];
                flagnode.Team = teamColor;
                flagnode.PlayerName = $"{teamColor} Flag";
                flagnode.CombatScore = 0;
                flagnode.EnemyFlagId = 0;
                activeTeams[i] = teamColor;
            }
            Console.WriteLine($"[DEBUG] {flagnodes.Count} flagnodes - teams: {string.Join(", ", activeTeams)}");
        }

        // Distribute players across teams (round-robin)
        for (var i = 0; i < players.Count; i++)
        {
            var teamIndex = i % activeTeams.Length;
            var player = players[i];
            var assignedTeam = activeTeams[teamIndex];
            player.Team = assignedTeam;
            player.CombatScore = (byte)Random.Shared.Next(1, 11);
            player.EnemyFlagId = 0;

            Console.WriteLine($"[DEBUG] Player {i}: {player.PlayerName} → Team {assignedTeam} (teamIndex={teamIndex}, activeTeams.Length={activeTeams.Length})");
        }
    }

    public static PlayerInfo ToPlayerInfo(GameDevice d) =>
        new()
        {
            DeviceId = d.AssignedDeviceId!.Value,
            Name = d.PlayerName,
            Team = d.Team ?? string.Empty,
            Type = d.DeviceType == DeviceType.FlagNode ? "flag" : "player",
            CombatScore = d.CombatScore ?? 0,
            EnemyFlagId = d.EnemyFlagId ?? 0
        };
}
