using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaptureTheFlag.Web.Json;

/// <summary>
/// JSON for ESP32 / nanoFramework <c>IGameHttpClient</c>: UTF-8, camelCase, numeric enums where applicable.
/// </summary>
public static class DeviceWireJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
