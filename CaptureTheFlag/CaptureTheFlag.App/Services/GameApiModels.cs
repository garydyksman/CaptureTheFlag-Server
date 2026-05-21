namespace CaptureTheFlag.App.Services;

public enum GamePhase
{
    Created,
    WaitingForPlayers,
    InProgress,
    Finished
}

public sealed class GameListItemDto
{
    public int Id { get; set; }
    public GamePhase Status { get; set; }

    public DateTime CreateTime { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<GameDeviceListItemDto> Devices { get; set; } = [];
}

public sealed class GameDeviceListItemDto
{
    public int Id { get; set; }
    public string PlayerName { get; set; } = "";
    public DateTime AddedOn { get; set; }
    public byte? DeviceId { get; set; }
    public string? Team { get; set; }
    public byte? CombatScore { get; set; }
    public int CombatReportScore { get; set; }
    public byte? EnemyFlagId { get; set; }
}
