using LazerBeatmapLister.Api;
using LazerBeatmapLister.Models;

namespace LazerBeatmapLister.Services;

public sealed class BeatmapDownloadService
{
    private readonly OsuApiClient apiClient;

    public BeatmapDownloadService(
        OsuApiClient apiClient)
    {
        this.apiClient = apiClient;
    }

    public async Task<DownloadResult>
        DownloadBeatmapsAsync(
            List<BeatmapSetInfo> maps,
            string destination,
            int maxParallel,
            ApiConfig config,
            IProgress<DownloadProgress>? progress)
    {
        Directory.CreateDirectory(destination);

        int completed = 0;
        int downloaded = 0;
        int skipped = 0;
        int failed = 0;
        var downloadedFiles = new System.Collections.Concurrent.ConcurrentBag<string>();

        using var semaphore =
            new SemaphoreSlim(maxParallel);

        var tasks =
            maps.Select(async map =>
            {
                await semaphore.WaitAsync();

                try
                {
                    Console.WriteLine(
                        $"START {map.Id} | " +
                        $"active={maxParallel - semaphore.CurrentCount}");

                    string defaultFileName =
                        $"{map.Id}.osz";

                    string destinationFile =
                        Path.Combine(
                            destination,
                            defaultFileName);

                    if (File.Exists(destinationFile))
                    {
                        Console.WriteLine($"SKIP {map.Id} | file already exists");

                        Interlocked.Increment(
                            ref skipped);

                        Interlocked.Increment(
                                ref completed);

                        downloadedFiles.Add(destinationFile);

                        return;
                    }

                    try
                    {
                        await apiClient.DownloadBeatmapAsync(
                            map.Id,
                            destinationFile,
                            config,
                            CancellationToken.None);

                        Interlocked.Increment(
                            ref downloaded);

                        downloadedFiles.Add(destinationFile);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(
                            $"{map.Id} FAILED: {ex}");

                        Interlocked.Increment(
                            ref failed);
                    }

                    int done =
                        Interlocked.Increment(
                            ref completed);

                    progress?.Report(
                        new DownloadProgress(
                            done,
                            maps.Count,
                            map.Id,
                            map.Artist,
                            map.Title));
                }
                finally
                {
                    semaphore.Release();

                    Console.WriteLine(
                        $"END {map.Id}");
                }
            });

        await Task.WhenAll(tasks);

        return new DownloadResult(
            downloaded,
            skipped,
            failed,
            downloadedFiles.ToList());
    }
}
