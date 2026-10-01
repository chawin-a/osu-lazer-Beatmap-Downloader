using System.Net.Http.Headers;
using System.Text.Json;
using LazerBeatmapLister.Models;

namespace LazerBeatmapLister.Api;

public sealed class OsuApiClient
{
    private static readonly HttpClient http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "LazerBeatmapLister/1.0");

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        return client;
    }

    public async Task<string> GetAccessTokenAsync(
        int clientId,
        string clientSecret)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "https://osu.ppy.sh/oauth/token");

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        request.Content =
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["client_id"] =
                        clientId.ToString(),

                    ["client_secret"] =
                        clientSecret,

                    ["grant_type"] =
                        "client_credentials",

                    ["scope"] =
                        "public"
                });

        using var response =
            await http.SendAsync(request);

        string json =
            await response.Content
                .ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"osu! OAuth failed.\n\n" +
                $"HTTP {(int)response.StatusCode} " +
                $"{response.StatusCode}\n\n" +
                json);
        }

        var token =
            JsonSerializer.Deserialize<
                OAuthTokenResponse>(
                    json);

        if (token == null ||
            string.IsNullOrWhiteSpace(
                token.AccessToken))
        {
            throw new Exception(
                "osu! returned an invalid OAuth response.\n\n" +
                json);
        }

        return token.AccessToken;
    }

    public async Task<List<BeatmapSetInfo>>
        SearchNewBeatmapsetsAsync(
            string token,
            HashSet<int> installedIds,
            int wantedCount,
            IProgress<string>? progress = null)
    {
        var result =
            new List<BeatmapSetInfo>();

        var seen =
            new HashSet<int>();

        string? cursor = null;

        using var client =
            new HttpClient
            {
                Timeout =
                    TimeSpan.FromMinutes(5)
            };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "LazerBeatmapLister/1.0");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                token);

        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        int page = 0;

        while (result.Count < wantedCount)
        {
            page++;

            progress?.Report(
                $"osu! API: searching page {page}...");

            var query =
                new List<string>
                {
                    "m=osu",
                    "s=ranked",
                    "limit=100"
                };

            if (!string.IsNullOrWhiteSpace(cursor))
            {
                query.Add(
                    "cursor_string=" +
                    Uri.EscapeDataString(cursor));
            }

            string url =
                "https://osu.ppy.sh/api/v2/beatmapsets/search?" +
                string.Join("&", query);

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    url);

            using var response =
                await client.SendAsync(request);

            string json =
                await response.Content
                    .ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"osu! beatmap search failed.\n\n" +
                    $"HTTP {(int)response.StatusCode} " +
                    $"{response.StatusCode}\n\n" +
                    json);
            }

            var data =
                JsonSerializer.Deserialize<
                    BeatmapSetSearchResponse>(
                    json);

            if (data?.Beatmapsets == null)
                break;

            foreach (var map in data.Beatmapsets)
            {
                if (map.Id <= 0)
                    continue;

                if (!seen.Add(map.Id))
                    continue;

                if (installedIds.Contains(map.Id))
                    continue;

                result.Add(
                    new BeatmapSetInfo(
                        map.Id,
                        map.Artist ??
                            "Unknown Artist",
                        map.Title ??
                            "Unknown Title"));

                if (result.Count >= wantedCount)
                    break;
            }

            if (result.Count >= wantedCount)
                break;

            cursor =
                data.CursorString;

            if (string.IsNullOrWhiteSpace(cursor))
                break;

            await Task.Delay(
                TimeSpan.FromSeconds(1));
        }

        return result;
    }

    public async Task DownloadBeatmapAsync(
        int beatmapSetId,
        string destination,
        ApiConfig config,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        foreach (string mirrorName in
                 config.DownloadMirrors)
        {
            if (!config.Servers.TryGetValue(
                    mirrorName,
                    out string? template))
            {
                continue;
            }

            string url =
                template.Replace(
                    "{beatmap_id}",
                    beatmapSetId.ToString());

            try
            {
                await DownloadFromMirrorAsync(
                    url,
                    destination,
                    cancellationToken);

                // SUCCESS
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                Console.WriteLine(
                    $"{mirrorName} failed: " +
                    ex.Message);
            }
        }

        throw new Exception(
            $"All download mirrors failed for " +
            $"beatmapset {beatmapSetId}.",
            lastException);
    }
    private static async Task DownloadFromMirrorAsync(
    string url,
    string destination,
    CancellationToken cancellationToken)
    {
        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                url);

        // Same headers as the Rust implementation.
        request.Headers.UserAgent.ParseAdd(
            "Mozilla/5.0");

        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        using var response =
            await http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        Console.WriteLine(
            $"HTTP {(int)response.StatusCode} " +
            $"{response.StatusCode}");

        string temporary =
            destination + ".download";


        Console.WriteLine(
            $"Destination: {destination}");

        // --------------------------------------------------------
        // Download
        // --------------------------------------------------------

        await using var input =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);

        await using var output =
            new FileStream(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true);

        long totalSize =
            response.Content.Headers.ContentLength
            ?? 0;

        long downloaded = 0;

        byte[] buffer =
            new byte[64 * 1024];

        while (true)
        {
            int read =
                await input.ReadAsync(
                    buffer.AsMemory(
                        0,
                        buffer.Length),
                    cancellationToken);

            if (read == 0)
                break;

            await output.WriteAsync(
                buffer.AsMemory(
                    0,
                    read),
                cancellationToken);

            downloaded += read;

            double progress =
                totalSize > 0
                    ? (double)downloaded / totalSize
                    : 0;
        }

        await output.FlushAsync(
            cancellationToken);

        // The using scope is still active here,
        // so dispose the stream before moving.
        output.Close();

        FileInfo tempInfo = new FileInfo(temporary);

        if (tempInfo.Length < 200)
        {
            Console.WriteLine(
                $"Invalid download: " +
                $"{tempInfo.Length} bytes");

            try
            {
                File.Delete(temporary);
            }
            catch
            {
                // Ignore cleanup failure.
            }

            throw new Exception(
                $"Downloaded file is too small: " +
                $"{tempInfo.Length} bytes.");
        }


        File.Move(
            temporary,
            destination,
            overwrite: true);
    }
}
