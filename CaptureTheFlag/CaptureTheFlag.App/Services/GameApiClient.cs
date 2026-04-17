using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaptureTheFlag.App.Services;

public sealed class GameApiClient(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<GameListItemDto>> GetGamesAsync(CancellationToken cancellationToken = default)
    {
        var list = await http.GetFromJsonAsync<List<GameListItemDto>>("api/games", JsonOptions, cancellationToken).ConfigureAwait(false);
        return list ?? [];
    }

    public async Task<GameListItemDto?> GetGameAsync(int id, CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<GameListItemDto>($"api/games/{id}", JsonOptions, cancellationToken).ConfigureAwait(false);

    public async Task CreateGameAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/games", new { }, JsonOptions, cancellationToken).ConfigureAwait(false);
        await EnsureOkAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task OpenLobbyAsync(int gameId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/games/{gameId}/status/waiting-for-players",
            new { },
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        await EnsureOkAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task StartGameAsync(int gameId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/games/{gameId}/status/in-progress",
            new { },
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        await EnsureOkAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task FinishGameAsync(int gameId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync(
            $"api/games/{gameId}/status/finished",
            new { },
            JsonOptions,
            cancellationToken).ConfigureAwait(false);
        await EnsureOkAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteGameAsync(int gameId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/games/{gameId}", cancellationToken).ConfigureAwait(false);
        await EnsureOkAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureOkAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body)
                ? $"API error {(int)response.StatusCode} {response.ReasonPhrase}"
                : $"API error {(int)response.StatusCode}: {body}");
    }
}
