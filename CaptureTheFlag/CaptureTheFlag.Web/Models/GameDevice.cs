namespace CaptureTheFlag.Web.Models;

public class GameDevice
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public Game Game { get; set; } = null!;
    public string PlayerName { get; set; } = string.Empty;
    public DateTime AddedOn { get; set; }

    /// <summary>Wire protocol id (1–254) assigned when the game moves to InProgress.</summary>
    public byte? AssignedDeviceId { get; set; }

    public string? Team { get; set; }
    public byte? CombatScore { get; set; }
    public int CombatReportScore { get; set; }
    public byte? EnemyFlagId { get; set; }
}
