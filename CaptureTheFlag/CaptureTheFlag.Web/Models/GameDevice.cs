using CaptureTheFlag.Web.Enums;

namespace CaptureTheFlag.Web.Models;

public class GameDevice
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    /// <summary>MAC address of the physical device (unique per game).</summary>
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>Type of device (Player or FlagNode).</summary>
    public DeviceType DeviceType { get; set; }

    /// <summary>Display name. For players, either provided or auto-generated. For flagnodes, auto-generated from team color.</summary>
    public string PlayerName { get; set; } = string.Empty;

    public DateTime AddedOn { get; set; }

    /// <summary>Wire protocol id (1–254) assigned when the game moves to InProgress.</summary>
    public byte? AssignedDeviceId { get; set; }

    public string? Team { get; set; }
    public byte? CombatScore { get; set; }
    public int CombatReportScore { get; set; }
    public byte? EnemyFlagId { get; set; }
    public byte[]? FlagKey { get; set; }
}

