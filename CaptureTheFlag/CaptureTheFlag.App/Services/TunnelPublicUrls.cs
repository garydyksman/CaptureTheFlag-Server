namespace CaptureTheFlag.App.Services;

/// <summary>
/// Reads Aspire dev tunnel / service discovery URLs injected as configuration (see Aspire dashboard).
/// </summary>
public sealed class TunnelPublicUrls(IConfiguration configuration)
{
    /// <summary>Public HTTPS (or HTTP) base URL for the <c>webapi</c> resource, when a dev tunnel is active.</summary>
    public string? WebApiPublicBase =>
        FirstNonEmpty(
            configuration["services:webapi:https:0"],
            configuration["services:webapi:http:0"]);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
            {
                return v.TrimEnd('/');
            }
        }

        return null;
    }
}
