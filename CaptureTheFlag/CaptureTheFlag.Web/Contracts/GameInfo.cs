using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace CaptureTheFlag.Web.Contracts;

/// <summary>
/// GET <c>/api/game</c> payload. <see cref="Status"/> is 0–3 (see device contract).
/// </summary>
[SwaggerSchema(Description = "Device game snapshot. `players` is always a JSON array at runtime (never null); use [] when empty.")]
public sealed class GameInfo
{
    /// <summary>0 = none / not joinable, 1 = waiting (registrations open), 2 = active, 3 = ended.</summary>
    public int Status { get; set; }

    /// <summary>AssignedDeviceId of the winning flag node. Non-null only when Status == 3.</summary>
    public byte? WinnerId { get; set; }

    /// <summary>Low byte of the server game row ID. Used by firmware to ignore GameEnd broadcasts from previous games.</summary>
    public byte GameId { get; set; }

    /// <summary>
    /// Always serialized as a JSON array; never null. Use <c>[]</c> when there are no players. Values are JSON numbers (often int32 in OpenAPI); firmware may cast to byte.
    /// </summary>
    [Required]
    [SwaggerSchema(Nullable = false, Description = "Always a JSON array at runtime; never null. Empty array when there are no players.")]
    public List<PlayerInfo> Players { get; set; } = [];
}

/// <summary>POST <c>/api/game/register</c> success body: <c>{ "deviceId": number }</c>.</summary>
public sealed class PlayerSetup
{
    public byte DeviceId { get; set; }
}

/// <summary>POST <c>/api/game/deliver</c> response.</summary>
public sealed class DeliverAcceptedResponse
{
    public bool Accepted { get; set; }
}

/// <summary>GET <c>/api/game/respawn/{deviceId}</c> response.</summary>
public sealed class RespawnScoreResponse
{
    public byte DeviceId { get; set; }
    public byte CombatScore { get; set; }
}
