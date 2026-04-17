namespace CaptureTheFlag.Web.Contracts;

public sealed class RegisterPlayerNameRequest
{
    public string? PlayerName { get; set; }
}

public sealed class CombatReportRequest
{
    public byte WinnerId { get; set; }
    public byte LoserId { get; set; }
    public bool LoserHadKey { get; set; }
}

public sealed class DeliverReportRequest
{
    public byte DeviceId { get; set; }

    /// <summary>Optional Base64 payload; omit, null, or empty when no key.</summary>
    public string? Key { get; set; }
}
