using CaptureTheFlag.Web.Enums;

namespace CaptureTheFlag.Web.Contracts;

public record GameDeviceDto(
    int Id,
    string PlayerName,
    DateTime AddedOn,
    byte? DeviceId,
    string? Team,
    byte? CombatScore,
    int CombatReportScore,
    byte? EnemyFlagId);

public record RegisterDeviceRequest(string? PlayerName);

public record RegisterDeviceResponse(int Id, int GameId, string PlayerName, byte DeviceId);

public record GameDto(
    int Id,
    DateTime CreateTime,
    DateTime? StartTime,
    DateTime? EndTime,
    GameStatus Status,
    IReadOnlyList<GameDeviceDto> Devices);

public record CreateGameRequest(
    GameStatus? Status,
    DateTime? StartTime,
    DateTime? EndTime);
