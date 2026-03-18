using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace api.Services;

public sealed class IgdbGameSearchResult
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public IgdbCoverResult? Cover { get; init; }
}

public sealed class IgdbGameDetailResult
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CoverUrl { get; init; }
}

public sealed class IgdbCoverResult
{
    public string? ImageId { get; init; }
}

public sealed class IgdbService
{
    private const string IgdbGamesEndpoint = "https://api.igdb.com/v4/games";
    private const string TwitchTokenEndpoint = "https://id.twitch.tv/oauth2/token";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public IgdbService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<IgdbGameSearchResult>> SearchGamesAsync(string query, int limit = 10)
    {
        var trimmedQuery = query.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery))
        {
            return [];
        }

        using var response = await SendIgdbRequestAsync(
            $"search \"{EscapeQueryValue(trimmedQuery)}\"; fields id,name,cover.image_id; limit {Math.Clamp(limit, 1, 25)};");
        var payload = await response.Content.ReadAsStringAsync();
        EnsureSuccessStatusCode(response, payload, "IGDB game search failed");

        using var document = JsonDocument.Parse(payload);
        return document.RootElement
            .EnumerateArray()
            .Select(MapSearchResult)
            .ToList();
    }

    public async Task<IgdbGameDetailResult?> GetGameByIdAsync(long id)
    {
        using var response = await SendIgdbRequestAsync(
            $"fields name,cover.image_id; where id = {id}; limit 1;");
        var payload = await response.Content.ReadAsStringAsync();
        EnsureSuccessStatusCode(response, payload, "IGDB game lookup failed");

        using var document = JsonDocument.Parse(payload);
        var game = document.RootElement.EnumerateArray().FirstOrDefault();
        if (game.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }

        var imageId = TryGetCoverImageId(game);
        return new IgdbGameDetailResult
        {
            Id = id,
            Name = game.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty,
            CoverUrl = BuildCoverUrl(imageId)
        };
    }

    private async Task<HttpResponseMessage> SendIgdbRequestAsync(string query)
    {
        var token = await GetAccessTokenAsync();
        var clientId = GetRequiredConfiguration("IGDB_CLIENT_ID", "IGDB:ClientId");

        var request = new HttpRequestMessage(HttpMethod.Post, IgdbGamesEndpoint)
        {
            Content = new StringContent(query, Encoding.UTF8, "text/plain")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("Client-ID", clientId);

        return await _httpClient.SendAsync(request);
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) &&
            _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) &&
                _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return _accessToken;
            }

            var clientId = GetRequiredConfiguration("IGDB_CLIENT_ID", "IGDB:ClientId");
            var clientSecret = GetRequiredConfiguration("IGDB_CLIENT_SECRET", "IGDB:ClientSecret");
            var tokenUrl =
                $"{TwitchTokenEndpoint}?client_id={Uri.EscapeDataString(clientId)}&client_secret={Uri.EscapeDataString(clientSecret)}&grant_type=client_credentials";

            using var response = await _httpClient.PostAsync(tokenUrl, null);
            var payload = await response.Content.ReadAsStringAsync();
            EnsureSuccessStatusCode(response, payload, "Twitch token request failed");

            using var document = JsonDocument.Parse(payload);
            _accessToken = document.RootElement.GetProperty("access_token").GetString();
            var expiresIn = document.RootElement.GetProperty("expires_in").GetInt32();
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

            return _accessToken
                ?? throw new InvalidOperationException("Unable to retrieve an IGDB access token.");
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private string GetRequiredConfiguration(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _configuration[key] ?? Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new InvalidOperationException(
            $"Missing IGDB configuration. Expected one of: {string.Join(", ", keys)}.");
    }

    private static IgdbGameSearchResult MapSearchResult(JsonElement game)
    {
        var imageId = TryGetCoverImageId(game);
        return new IgdbGameSearchResult
        {
            Id = game.GetProperty("id").GetInt64(),
            Name = game.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty,
            Cover = imageId == null
                ? null
                : new IgdbCoverResult
                {
                    ImageId = imageId
                }
        };
    }

    private static string? TryGetCoverImageId(JsonElement game)
    {
        if (!game.TryGetProperty("cover", out var coverElement) ||
            coverElement.ValueKind != JsonValueKind.Object ||
            !coverElement.TryGetProperty("image_id", out var imageIdElement))
        {
            return null;
        }

        return imageIdElement.GetString();
    }

    private static string EscapeQueryValue(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public static string? BuildCoverUrl(string? imageId)
    {
        return string.IsNullOrWhiteSpace(imageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg";
    }

    private static void EnsureSuccessStatusCode(
        HttpResponseMessage response,
        string payload,
        string context)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = string.IsNullOrWhiteSpace(payload) ? "No response body." : payload;
        throw new InvalidOperationException(
            $"{context}. HTTP {(int)response.StatusCode} {response.ReasonPhrase}. {errorBody}");
    }
}
