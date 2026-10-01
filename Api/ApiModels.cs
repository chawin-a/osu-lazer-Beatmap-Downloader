using System.Text.Json.Serialization;

namespace LazerBeatmapLister.Api;

public sealed class OAuthTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }
}

public sealed class BeatmapSetSearchResponse
{
    [JsonPropertyName("beatmapsets")]
    public List<OsuBeatmapSet>? Beatmapsets { get; set; }

    [JsonPropertyName("cursor_string")]
    public string? CursorString { get; set; }
}

public sealed class OsuBeatmapSet
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("creator")]
    public string? Creator { get; set; }
}
