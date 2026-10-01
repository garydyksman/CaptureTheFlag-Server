namespace CaptureTheFlag.Web.Contracts;

/// <summary>
/// Player row in <c>GET /api/game</c> (<c>players</c> array). JSON uses camelCase (<c>deviceId</c>, <c>name</c>, …).
/// </summary>
public sealed class PlayerInfo
{
    public byte DeviceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "player" or "flag"
    public byte CombatScore { get; set; }
    public byte EnemyFlagId { get; set; }
}
