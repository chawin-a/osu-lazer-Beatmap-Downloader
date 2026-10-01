using LazerBeatmapLister.Models;

namespace LazerBeatmapLister.Services;

public static class ConfigService
{
    public static ApiConfig LoadConfig()
    {
        string configPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "config.yaml");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"config.yaml was not found:\n{configPath}");
        }

        string clientRealm = "";
        string clientIdText = "";
        string clientSecret = "";

        string songsPath = "";

        int fetchCount = 500;
        int downloadThreads = 5;

        var mirrors = new List<string>();

        var servers =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        bool readingMirrors = false;
        bool readingServers = false;

        foreach (string rawLine in
                 File.ReadLines(configPath))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 ||
                line.StartsWith("#"))
            {
                continue;
            }

            // ------------------------------------------------
            // Section
            // ------------------------------------------------

            if (line == "download_mirrors:")
            {
                readingMirrors = true;
                readingServers = false;
                continue;
            }

            if (line == "server:")
            {
                readingServers = true;
                readingMirrors = false;
                continue;
            }

            // ------------------------------------------------
            // Mirror list
            // ------------------------------------------------

            if (readingMirrors &&
                line.StartsWith("-"))
            {
                string mirror =
                    line[1..].Trim();

                mirror =
                    RemoveYamlQuotes(mirror);

                if (!string.IsNullOrWhiteSpace(mirror))
                {
                    mirrors.Add(mirror);
                }

                continue;
            }

            // ------------------------------------------------
            // Server URLs
            // ------------------------------------------------

            if (readingServers &&
                !line.StartsWith("-"))
            {
                int colon =
                    line.IndexOf(':');

                if (colon > 0)
                {
                    string key =
                        line[..colon]
                            .Trim();

                    string value =
                        line[(colon + 1)..]
                            .Trim();

                    value =
                        RemoveYamlQuotes(value);

                    servers[key] = value;

                    continue;
                }
            }

            // ------------------------------------------------
            // Normal key/value
            // ------------------------------------------------

            readingMirrors = false;
            readingServers = false;

            int separator =
                line.IndexOf(':');

            if (separator <= 0)
                continue;

            string normalKey =
                line[..separator]
                    .Trim()
                    .ToLowerInvariant();

            string normalValue =
                line[(separator + 1)..]
                    .Trim();

            normalValue =
                RemoveYamlQuotes(normalValue);

            switch (normalKey)
            {
                case "client_realm":
                    clientRealm = normalValue;
                    break;

                case "client_id":
                    clientIdText = normalValue;
                    break;

                case "client_secret":
                    clientSecret = normalValue;
                    break;

                case "songs_path":
                    songsPath = normalValue;
                    break;

                case "fetch_count":
                    if (int.TryParse(
                            normalValue,
                            out int count))
                    {
                        fetchCount =
                            Math.Clamp(
                                count,
                                50,
                                5000);
                    }

                    break;

                case "download_threads":
                    if (int.TryParse(
                            normalValue,
                            out int threads))
                    {
                        downloadThreads =
                            Math.Clamp(
                                threads,
                                1,
                                10);
                    }

                    break;
            }
        }

        if (!int.TryParse(
                clientIdText,
                out int clientId) ||
            clientId <= 0)
        {
            throw new Exception(
                "Invalid client_id.");
        }

        if (string.IsNullOrWhiteSpace(
                clientSecret))
        {
            throw new Exception(
                "client_secret is missing.");
        }

        if (string.IsNullOrWhiteSpace(
                songsPath))
        {
            throw new Exception(
                "songs_path is missing.");
        }

        if (mirrors.Count == 0)
        {
            mirrors.Add("beatconnect");
            mirrors.Add("nerinyan");
            mirrors.Add("catboy");
            mirrors.Add("osu_direct");
            mirrors.Add("osu_ppy");
        }

        return new ApiConfig(
            clientRealm,
            clientId,
            clientSecret,
            fetchCount,
            downloadThreads,
            songsPath,
            mirrors,
            servers);
    }

    private static string RemoveYamlQuotes(
        string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' &&
              value[^1] == '"') ||
             (value[0] == '\'' &&
              value[^1] == '\'')))
        {
            return value[1..^1];
        }

        return value;
    }
}