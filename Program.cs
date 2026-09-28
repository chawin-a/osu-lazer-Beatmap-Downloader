using Realms;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LazerBeatmapLister;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

class MainForm : Form
{
    // ============================================================
    // UI
    // ============================================================

    readonly TextBox pathBox = new()
    {
        ReadOnly = true,
        Dock = DockStyle.Fill
    };

    readonly Button browseBtn = new()
    {
        Text = "Browse...",
        AutoSize = true
    };

    readonly Button loadBtn = new()
    {
        Text = "Load",
        AutoSize = true,
        Enabled = false
    };

    readonly CheckBox skipLocal = new()
    {
        Text = "Skip local maps (ID <= 0)",
        Checked = true,
        AutoSize = true
    };

    readonly TextBox output = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10),
        WordWrap = false
    };

    readonly Button copyBtn = new()
    {
        Text = "Copy",
        AutoSize = true,
        Enabled = false
    };

    readonly Button saveBtn = new()
    {
        Text = "Save .txt",
        AutoSize = true,
        Enabled = false
    };

    readonly Button exportBtn = new()
    {
        Text = "Export to Songs...",
        AutoSize = true,
        Enabled = false
    };

    readonly Button findMissingBtn = new()
    {
        Text = "Find New Songs",
        AutoSize = true,
        Enabled = false
    };

    readonly Button downloadMissingBtn = new()
    {
        Text = "Download New Songs...",
        AutoSize = true,
        Enabled = false
    };

    readonly Button saveMissingBtn = new()
    {
        Text = "Save New Songs .txt",
        AutoSize = true,
        Enabled = false
    };

    readonly NumericUpDown fetchCount = new()
    {
        Minimum = 50,
        Maximum = 5000,
        Increment = 50,
        Value = 500,
        Width = 75
    };

    readonly NumericUpDown downloadThreads = new()
    {
        Minimum = 1,
        Maximum = 10,
        Value = 5,
        Width = 55
    };

    readonly Label status = new()
    {
        Text = "Select your client.realm file.",
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };

    readonly Label apiStatus = new()
    {
        Text = "",
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };

    // ============================================================
    // State
    // ============================================================

    List<int> ids = new();

    List<BeatmapSetInfo> missingMaps = new();

    ApiConfig? config;

    // ============================================================
    // HTTP
    // ============================================================

    static readonly HttpClient http = CreateHttpClient();

    static HttpClient CreateHttpClient()
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

    // ============================================================
    // Constructor
    // ============================================================

    public MainForm()
    {
        Text = "Lazer Beatmap Lister";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;

        // --------------------------------------------------------
        // Top
        // --------------------------------------------------------

        var top = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Padding = new Padding(8)
        };

        top.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100));

        top.ColumnStyles.Add(
            new ColumnStyle(SizeType.AutoSize));

        top.ColumnStyles.Add(
            new ColumnStyle(SizeType.AutoSize));

        top.Controls.Add(pathBox, 0, 0);
        top.Controls.Add(browseBtn, 1, 0);
        top.Controls.Add(loadBtn, 2, 0);

        top.Controls.Add(skipLocal, 0, 1);
        top.SetColumnSpan(skipLocal, 3);

        // --------------------------------------------------------
        // API panel
        // --------------------------------------------------------

        var apiPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true
        };

        apiPanel.Controls.Add(new Label
        {
            Text = "Check:",
            AutoSize = true,
            Margin = new Padding(3, 7, 3, 3)
        });

        apiPanel.Controls.Add(fetchCount);

        apiPanel.Controls.Add(new Label
        {
            Text = "new beatmapsets",
            AutoSize = true,
            Margin = new Padding(3, 7, 15, 3)
        });

        apiPanel.Controls.Add(new Label
        {
            Text = "Downloads:",
            AutoSize = true,
            Margin = new Padding(3, 7, 3, 3)
        });

        apiPanel.Controls.Add(downloadThreads);

        apiPanel.Controls.Add(new Label
        {
            Text = "parallel",
            AutoSize = true,
            Margin = new Padding(3, 7, 15, 3)
        });

        apiPanel.Controls.Add(findMissingBtn);
        apiPanel.Controls.Add(downloadMissingBtn);
        apiPanel.Controls.Add(saveMissingBtn);
        apiPanel.Controls.Add(apiStatus);

        // --------------------------------------------------------
        // Bottom
        // --------------------------------------------------------

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(8)
        };

        bottom.Controls.Add(copyBtn);
        bottom.Controls.Add(saveBtn);
        bottom.Controls.Add(exportBtn);
        bottom.Controls.Add(status);

        Controls.Add(output);
        Controls.Add(apiPanel);
        Controls.Add(top);
        Controls.Add(bottom);

        output.BringToFront();

        // --------------------------------------------------------
        // Default Lazer directory
        // --------------------------------------------------------

        string defaultDir = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
            "osu");

        // --------------------------------------------------------
        // Browse client.realm
        // --------------------------------------------------------

        browseBtn.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Select client.realm",

                Filter =
                    "Realm database (*.realm)|*.realm|" +
                    "All files (*.*)|*.*",

                InitialDirectory =
                    Directory.Exists(defaultDir)
                        ? defaultDir
                        : "",

                FileName = "client.realm"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            pathBox.Text = dlg.FileName;
            loadBtn.Enabled = true;

            _ = LoadAsync();
        };

        // --------------------------------------------------------
        // Load
        // --------------------------------------------------------

        loadBtn.Click += async (_, _) =>
        {
            await LoadAsync();
        };

        skipLocal.CheckedChanged += (_, _) =>
        {
            if (loadBtn.Enabled)
                _ = LoadAsync();
        };

        // --------------------------------------------------------
        // Copy
        // --------------------------------------------------------

        copyBtn.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(output.Text))
                return;

            Clipboard.SetText(output.Text);
            status.Text = "Copied to clipboard.";
        };

        // --------------------------------------------------------
        // Save installed IDs
        // --------------------------------------------------------

        saveBtn.Click += (_, _) =>
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                FileName = "beatmap-set-ids.txt"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            File.WriteAllLines(
                dlg.FileName,
                ids.Select(x => x.ToString()));

            status.Text =
                $"Saved to {dlg.FileName}";
        };

        // --------------------------------------------------------
        // Find new
        // --------------------------------------------------------

        findMissingBtn.Click += async (_, _) =>
        {
            await FindNewSongsAsync();
        };

        // --------------------------------------------------------
        // Download
        // --------------------------------------------------------

        downloadMissingBtn.Click += async (_, _) =>
        {
            await DownloadNewSongsAsync();
        };

        // --------------------------------------------------------
        // Save new list
        // --------------------------------------------------------

        saveMissingBtn.Click += (_, _) =>
        {
            if (missingMaps.Count == 0)
                return;

            using var dlg = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt",
                FileName = "new-beatmap-set-ids.txt"
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            File.WriteAllLines(
                dlg.FileName,
                missingMaps.Select(x =>
                    $"{x.Id} | {x.Artist} - {x.Title}"));

            status.Text =
                $"Saved {missingMaps.Count:N0} new beatmapsets.";
        };

        // --------------------------------------------------------
        // Export
        // --------------------------------------------------------

        exportBtn.Click += async (_, _) =>
        {
            using var dlg = new FolderBrowserDialog
            {
                Description =
                    "Select the osu!stable Songs folder " +
                    "(or any output folder)",

                UseDescriptionForTitle = true
            };

            if (dlg.ShowDialog(this) != DialogResult.OK)
                return;

            string src = pathBox.Text;
            string dest = dlg.SelectedPath;
            bool skip = skipLocal.Checked;

            var progress =
                new Progress<string>(
                    m => status.Text = m);

            browseBtn.Enabled =
            loadBtn.Enabled =
            exportBtn.Enabled = false;

            try
            {
                var result =
                    await Task.Run(() =>
                        ExportSets(
                            src,
                            dest,
                            skip,
                            progress));

                status.Text =
                    $"Exported {result.exported}, " +
                    $"skipped {result.skipped} existing, " +
                    $"{result.missing} missing files";
            }
            catch (Exception e)
            {
                status.Text = "Export failed.";

                MessageBox.Show(
                    this,
                    e.Message,
                    "Export failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                browseBtn.Enabled = true;
                loadBtn.Enabled = true;
                exportBtn.Enabled = ids.Count > 0;
            }
        };
    }

    // ============================================================
    // LOAD REALM
    // ============================================================

    async Task LoadAsync()
    {
        string path = pathBox.Text;

        if (!File.Exists(path))
        {
            MessageBox.Show(
                this,
                "client.realm does not exist.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return;
        }

        bool skip = skipLocal.Checked;

        browseBtn.Enabled =
        loadBtn.Enabled =
        copyBtn.Enabled =
        saveBtn.Enabled =
        exportBtn.Enabled =
        findMissingBtn.Enabled = false;

        output.Clear();

        status.Text =
            "Reading client.realm...";

        try
        {
            ids =
                await Task.Run(() =>
                    ReadSetIds(
                        path,
                        skip));

            output.Text =
                string.Join(
                    Environment.NewLine,
                    ids);

            status.Text =
                $"{ids.Count:N0} installed beatmapsets.";

            copyBtn.Enabled =
            saveBtn.Enabled =
            exportBtn.Enabled =
            findMissingBtn.Enabled =
                ids.Count > 0;
        }
        catch (Exception e)
        {
            status.Text = "Failed.";

            MessageBox.Show(
                this,
                e.Message,
                "Failed to read realm",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            browseBtn.Enabled = true;
            loadBtn.Enabled = true;

            findMissingBtn.Enabled =
                ids.Count > 0;
        }
    }

    // ============================================================
    // FIND NEW SONGS
    // ============================================================

    async Task FindNewSongsAsync()
    {
        if (ids.Count == 0)
        {
            MessageBox.Show(
                this,
                "Load your client.realm first.",
                "No Realm data",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        try
        {
            config = LoadConfig();

            // Apply config defaults.
            fetchCount.Value =
                Math.Clamp(
                    config.FetchCount,
                    fetchCount.Minimum,
                    fetchCount.Maximum);

            downloadThreads.Value =
                Math.Clamp(
                    config.DownloadThreads,
                    downloadThreads.Minimum,
                    downloadThreads.Maximum);

            apiStatus.Text =
                "osu! API";

            int count =
                (int)fetchCount.Value;

            browseBtn.Enabled = false;
            loadBtn.Enabled = false;
            findMissingBtn.Enabled = false;
            downloadMissingBtn.Enabled = false;
            saveMissingBtn.Enabled = false;

            output.Clear();
            missingMaps.Clear();

            status.Text =
                "Requesting osu! OAuth token...";

            string token =
                await GetAccessTokenAsync(
                    config.ClientId,
                    config.ClientSecret);

            status.Text =
                "Searching osu! API...";

            missingMaps =
                await SearchNewBeatmapsetsAsync(
                    token,
                    new HashSet<int>(ids),
                    count,
                    new Progress<string>(
                        x => status.Text = x));

            foreach (var map in missingMaps)
            {
                output.AppendText(
                    $"{map.Id} | " +
                    $"{map.Artist} - {map.Title}" +
                    Environment.NewLine);
            }

            apiStatus.Text =
                $"{missingMaps.Count:N0} new";

            status.Text =
                $"Found {missingMaps.Count:N0} " +
                "beatmapsets not in your Lazer database.";

            downloadMissingBtn.Enabled =
                missingMaps.Count > 0;

            saveMissingBtn.Enabled =
                missingMaps.Count > 0;
        }
        catch (Exception e)
        {
            status.Text =
                "osu! API search failed.";

            MessageBox.Show(
                this,
                e.Message,
                "osu! API error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            browseBtn.Enabled = true;
            loadBtn.Enabled = true;

            findMissingBtn.Enabled =
                ids.Count > 0;

            downloadMissingBtn.Enabled =
                missingMaps.Count > 0;

            saveMissingBtn.Enabled =
                missingMaps.Count > 0;
        }
    }

    // ============================================================
    // OSU OAUTH
    // ============================================================

    static async Task<string> GetAccessTokenAsync(
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

    // ============================================================
    // OSU SEARCH
    // ============================================================

    static async Task<List<BeatmapSetInfo>>
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
                    // osu!standard
                    "m=osu",

                    // Ranked
                    "s=ranked",

                    // Maximum page size
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

            // osu! asks API consumers to stay <=
            // 60 requests/minute. One second between
            // pages keeps us comfortably under that.
            await Task.Delay(
                TimeSpan.FromSeconds(1));
        }

        return result;
    }

    // ============================================================
    // DOWNLOAD
    // ============================================================

    async Task DownloadNewSongsAsync()
    {
        if (missingMaps.Count == 0)
            return;

        using var dlg =
            new FolderBrowserDialog
            {
                Description =
                    "Select where to download " +
                    "the new .osz files.",

                UseDescriptionForTitle = true
            };

        if (dlg.ShowDialog(this) !=
            DialogResult.OK)
        {
            return;
        }

        string destination =
            dlg.SelectedPath;

        int threads =
            (int)downloadThreads.Value;

        browseBtn.Enabled = false;
        loadBtn.Enabled = false;
        findMissingBtn.Enabled = false;
        downloadMissingBtn.Enabled = false;
        saveMissingBtn.Enabled = false;

        try
        {
            var progress =
                new Progress<DownloadProgress>(
                    p =>
                    {
                        status.Text =
                            $"[{p.Completed}/{p.Total}] " +
                            $"{p.Id} | " +
                            $"{p.Artist} - " +
                            $"{p.Title}";

                        apiStatus.Text =
                            $"{p.Completed}/{p.Total}";
                    });

            var result =
                await DownloadBeatmapsAsync(
                    missingMaps,
                    destination,
                    threads,
                    progress);

            status.Text =
                $"Finished. " +
                $"Downloaded {result.Downloaded:N0}, " +
                $"skipped {result.Skipped:N0}, " +
                $"failed {result.Failed:N0}.";

            MessageBox.Show(
                this,
                $"Downloaded: {result.Downloaded:N0}\n" +
                $"Already existed: {result.Skipped:N0}\n" +
                $"Failed: {result.Failed:N0}",
                "Download complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception e)
        {
            status.Text =
                "Download failed.";

            MessageBox.Show(
                this,
                e.Message,
                "Download failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            browseBtn.Enabled = true;
            loadBtn.Enabled = true;

            findMissingBtn.Enabled =
                ids.Count > 0;

            downloadMissingBtn.Enabled =
                missingMaps.Count > 0;

            saveMissingBtn.Enabled =
                missingMaps.Count > 0;
        }
    }

    static async Task<DownloadResult>
        DownloadBeatmapsAsync(
            List<BeatmapSetInfo> maps,
            string destination,
            int maxParallel,
            IProgress<DownloadProgress>? progress)
    {
        Directory.CreateDirectory(
            destination);

        int completed = 0;
        int downloaded = 0;
        int skipped = 0;
        int failed = 0;

        var options =
            new ParallelOptions
            {
                MaxDegreeOfParallelism =
                    maxParallel
            };

        await Parallel.ForEachAsync(
            maps,
            options,
            async (map, cancellationToken) =>
            {
                try
                {
                    string title =
                        SanitizeFileName(
                            $"{map.Artist} - {map.Title}");

                    string fileName =
                        $"{map.Id} {title}.osz";

                    string destinationFile =
                        Path.Combine(
                            destination,
                            fileName);

                    if (File.Exists(
                            destinationFile))
                    {
                        Interlocked.Increment(
                            ref skipped);
                    }
                    else
                    {
                        await DownloadBeatmapAsync(
                            map.Id,
                            destinationFile,
                            cancellationToken);

                        Interlocked.Increment(
                            ref downloaded);
                    }
                }
                catch
                {
                    Interlocked.Increment(
                        ref failed);
                }
                finally
                {
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
            });

        return new DownloadResult(
            downloaded,
            skipped,
            failed);
    }

    static async Task DownloadBeatmapAsync(
        int beatmapSetId,
        string destination,
        CancellationToken cancellationToken)
    {
        /*
         * IMPORTANT:
         *
         * The official osu! API documentation marks
         * certain routes as "lazer", and those routes
         * are not available to normal Client Credentials
         * tokens.
         *
         * Therefore this downloader uses the public
         * beatmapset download URL rather than pretending
         * that the Client Credentials token grants
         * Lazer download access.
         */

        string url =
            $"https://osu.ppy.sh/beatmapsets/" +
            $"{beatmapSetId}/download";

        string tempPath =
            destination + ".download";

        try
        {
            using var response =
                await http.GetAsync(
                    url,
                    HttpCompletionOption
                        .ResponseHeadersRead,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var input =
                await response.Content
                    .ReadAsStreamAsync(
                        cancellationToken);

            await using var output =
                new FileStream(
                    tempPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    useAsync: true);

            await input.CopyToAsync(
                output,
                cancellationToken);

            await output.FlushAsync(
                cancellationToken);

            File.Move(
                tempPath,
                destination,
                overwrite: true);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
                // Ignore cleanup failure.
            }

            throw;
        }
    }

    // ============================================================
    // REALM
    // ============================================================

    static List<int> ReadSetIds(
        string source,
        bool skipLocal)
    {
        string copy =
            Path.Combine(
                Path.GetTempPath(),
                $"client-copy-{Guid.NewGuid():N}.realm");

        File.Copy(source, copy);

        try
        {
            var config =
                new RealmConfiguration(copy)
                {
                    IsReadOnly = true,
                    IsDynamic = true
                };

            using var realm =
                Realm.GetInstance(config);

            var result =
                new SortedSet<int>();

            foreach (var set in
                     realm.DynamicApi
                         .All("BeatmapSet"))
            {
                if (set.DynamicApi
                    .Get<bool>("DeletePending"))
                {
                    continue;
                }

                int id =
                    set.DynamicApi
                        .Get<int>("OnlineID");

                if (skipLocal && id <= 0)
                    continue;

                result.Add(id);
            }

            return result.ToList();
        }
        finally
        {
            try
            {
                File.Delete(copy);
                File.Delete(copy + ".lock");
            }
            catch
            {
                // Ignore cleanup error.
            }
        }
    }

    // ============================================================
    // EXPORT LAZER -> STABLE
    // ============================================================

    static (
        int exported,
        int skipped,
        int missing)
        ExportSets(
            string source,
            string destRoot,
            bool skipLocal,
            IProgress<string> progress)
    {
        string filesRoot =
            Path.Combine(
                Path.GetDirectoryName(source)!,
                "files");

        if (!Directory.Exists(filesRoot))
        {
            throw new DirectoryNotFoundException(
                "Could not find the files folder next " +
                "to client.realm:\n" +
                filesRoot);
        }

        string copy =
            Path.Combine(
                Path.GetTempPath(),
                $"client-copy-{Guid.NewGuid():N}.realm");

        File.Copy(source, copy);

        int exported = 0;
        int skipped = 0;
        int missing = 0;
        int seen = 0;

        try
        {
            var config =
                new RealmConfiguration(copy)
                {
                    IsReadOnly = true,
                    IsDynamic = true
                };

            using var realm =
                Realm.GetInstance(config);

            foreach (var set in
                     realm.DynamicApi
                         .All("BeatmapSet"))
            {
                if (set.DynamicApi
                    .Get<bool>("DeletePending"))
                {
                    continue;
                }

                int id =
                    set.DynamicApi
                        .Get<int>("OnlineID");

                if (skipLocal && id <= 0)
                    continue;

                string artist =
                    "Unknown Artist";

                string title =
                    "Unknown Title";

                var first =
                    set.DynamicApi
                        .GetList<IRealmObject>(
                            "Beatmaps")
                        .FirstOrDefault();

                var meta =
                    first?.DynamicApi
                        .Get<IRealmObject>(
                            "Metadata");

                if (meta != null)
                {
                    artist =
                        meta.DynamicApi
                            .Get<string>("Artist");

                    title =
                        meta.DynamicApi
                            .Get<string>("Title");
                }

                string name =
                    id > 0
                        ? $"{id} {artist} - {title}"
                        : $"{artist} - {title}";

                string dir =
                    Path.Combine(
                        destRoot,
                        SanitizeFolderName(name));

                progress.Report(
                    $"{++seen}: {name}");

                if (Directory.Exists(dir))
                {
                    skipped++;
                    continue;
                }

                Directory.CreateDirectory(dir);

                string dirFull =
                    Path.GetFullPath(dir)
                    + Path.DirectorySeparatorChar;

                foreach (var usage in
                         set.DynamicApi
                             .GetList<IRealmObject>(
                                 "Files"))
                {
                    string filename =
                        usage.DynamicApi
                            .Get<string>(
                                "Filename");

                    string hash =
                        usage.DynamicApi
                            .Get<IRealmObject>(
                                "File")
                            .DynamicApi
                            .Get<string>("Hash");

                    string from =
                        Path.Combine(
                            filesRoot,
                            hash[..1],
                            hash[..2],
                            hash);

                    string to =
                        Path.GetFullPath(
                            Path.Combine(
                                dir,
                                filename));

                    if (!to.StartsWith(
                            dirFull,
                            StringComparison
                                .OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!File.Exists(from))
                    {
                        missing++;
                        continue;
                    }

                    Directory.CreateDirectory(
                        Path.GetDirectoryName(to)!);

                    File.Copy(
                        from,
                        to,
                        overwrite: true);
                }

                exported++;
            }
        }
        finally
        {
            try
            {
                File.Delete(copy);
                File.Delete(copy + ".lock");
            }
            catch
            {
                // Ignore cleanup error.
            }
        }

        return (
            exported,
            skipped,
            missing);
    }

    // ============================================================
    // CONFIG.YAML
    // ============================================================

    static ApiConfig LoadConfig()
    {
        string configPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "config.yaml");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                "config.yaml was not found.\n\n" +
                "Expected location:\n" +
                configPath +
                "\n\n" +
                "Example:\n\n" +
                "client_id: 123456\n" +
                "client_secret: YOUR_CLIENT_SECRET\n" +
                "fetch_count: 500\n" +
                "download_threads: 5",
                configPath);
        }

        string clientIdText = "";
        string clientSecret = "";

        int fetchCount = 500;
        int downloadThreads = 5;

        foreach (string rawLine in
                 File.ReadLines(configPath))
        {
            string line =
                rawLine.Trim();

            if (line.Length == 0)
                continue;

            if (line.StartsWith("#"))
                continue;

            int colon =
                line.IndexOf(':');

            if (colon <= 0)
                continue;

            string key =
                line[..colon]
                    .Trim()
                    .ToLowerInvariant();

            string value =
                line[(colon + 1)..]
                    .Trim();

            value =
                RemoveYamlQuotes(value);

            switch (key)
            {
                case "client_id":
                    clientIdText = value;
                    break;

                case "client_secret":
                    clientSecret = value;
                    break;

                case "fetch_count":
                    if (int.TryParse(
                            value,
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
                            value,
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
                "Invalid client_id in config.yaml.");
        }

        if (string.IsNullOrWhiteSpace(
                clientSecret))
        {
            throw new Exception(
                "client_secret is missing " +
                "from config.yaml.");
        }

        return new ApiConfig(
            clientId,
            clientSecret,
            fetchCount,
            downloadThreads);
    }

    static string RemoveYamlQuotes(
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

    // ============================================================
    // HELPERS
    // ============================================================

    static string SanitizeFolderName(
        string name)
    {
        foreach (char c in
                 Path.GetInvalidFileNameChars())
        {
            name =
                name.Replace(c, '_');
        }

        name =
            name.Trim()
                .TrimEnd('.');

        return name.Length > 120
            ? name[..120].TrimEnd()
            : name;
    }

    static string SanitizeFileName(
        string name)
    {
        foreach (char c in
                 Path.GetInvalidFileNameChars())
        {
            name =
                name.Replace(c, '_');
        }

        name = name.Trim();

        if (name.Length == 0)
            name = "Unknown";

        return name.Length > 150
            ? name[..150].TrimEnd()
            : name;
    }

    // ============================================================
    // DATA TYPES
    // ============================================================

    record ApiConfig(
        int ClientId,
        string ClientSecret,
        int FetchCount,
        int DownloadThreads);

    record BeatmapSetInfo(
        int Id,
        string Artist,
        string Title);

    record DownloadProgress(
        int Completed,
        int Total,
        int Id,
        string Artist,
        string Title);

    record DownloadResult(
        int Downloaded,
        int Skipped,
        int Failed);

    // ============================================================
    // OAUTH JSON
    // ============================================================

    sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    // ============================================================
    // BEATMAP SEARCH JSON
    // ============================================================

    sealed class BeatmapSetSearchResponse
    {
        [JsonPropertyName("beatmapsets")]
        public List<OsuBeatmapSet>? Beatmapsets { get; set; }

        [JsonPropertyName("cursor_string")]
        public string? CursorString { get; set; }
    }

    sealed class OsuBeatmapSet
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
}
