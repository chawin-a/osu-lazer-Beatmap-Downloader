namespace LazerBeatmapLister.Models;

public record ApiConfig(
    string ClientRealm,
    int ClientId,
    string ClientSecret,
    int FetchCount,
    int DownloadThreads,
    string SongsPath,
    string OsuLazerExe,
    List<string> DownloadMirrors,
    Dictionary<string, string> Servers);

public record BeatmapSetInfo(
    int Id,
    string Artist,
    string Title);

public record DownloadProgress(
    int Completed,
    int Total,
    int Id,
    string Artist,
    string Title);

public record DownloadResult(
    int Downloaded,
    int Skipped,
    int Failed,
    List<string> DownloadedFiles);
