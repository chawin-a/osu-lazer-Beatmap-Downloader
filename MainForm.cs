using System.Net.Http.Headers;
using LazerBeatmapLister.Api;
using LazerBeatmapLister.Models;
using LazerBeatmapLister.Realm;
using LazerBeatmapLister.Services;

namespace LazerBeatmapLister;

public class MainForm : Form
{
    // ============================================================
    // SERVICES
    // ============================================================

    private readonly OsuApiClient osuApi = new();

    private readonly BeatmapDownloadService downloadService;

    // ============================================================
    // UI
    // ============================================================

    private readonly TextBox pathBox = new()
    {
        ReadOnly = true,
        Dock = DockStyle.Fill
    };

    private readonly Button browseBtn = new()
    {
        Text = "Browse...",
        AutoSize = true
    };

    private readonly Button loadBtn = new()
    {
        Text = "Load",
        AutoSize = true,
        Enabled = false
    };

    private readonly CheckBox skipLocal = new()
    {
        Text = "Skip local maps (ID <= 0)",
        Checked = true,
        AutoSize = true
    };

    private readonly TextBox output = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 10),
        WordWrap = false
    };

    private readonly Button copyBtn = new()
    {
        Text = "Copy",
        AutoSize = true,
        Enabled = false
    };

    private readonly Button findMissingBtn = new()
    {
        Text = "Find New Songs",
        AutoSize = true,
        Enabled = false
    };

    private readonly Button downloadMissingBtn = new()
    {
        Text = "Download New Songs...",
        AutoSize = true,
        Enabled = false
    };

    private readonly NumericUpDown fetchCount = new()
    {
        Minimum = 50,
        Maximum = 5000,
        Increment = 50,
        Value = 500,
        Width = 75
    };

    private readonly NumericUpDown downloadThreads = new()
    {
        Minimum = 1,
        Maximum = 10,
        Value = 5,
        Width = 55
    };

    private readonly Label status = new()
    {
        Text = "Select your client.realm file.",
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };

    private readonly Label apiStatus = new()
    {
        Text = "",
        AutoSize = true,
        Anchor = AnchorStyles.Left
    };

    // ============================================================
    // STATE
    // ============================================================

    private List<int> ids = new();

    private List<BeatmapSetInfo> missingMaps = new();

    private ApiConfig? config;

    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public MainForm()
    {
        downloadService =
            new BeatmapDownloadService(
                osuApi);

        Text =
            "Lazer Beatmap Lister";

        Width = 1000;
        Height = 700;

        StartPosition =
            FormStartPosition.CenterScreen;

        BuildUi();
        RegisterEvents();
        LoadDefaultRealm();
        LoadConfigIntoUI();
    }

    // ============================================================
    // UI
    // ============================================================

    private void BuildUi()
    {
        var top =
            new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                Padding = new Padding(8)
            };

        top.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100));

        top.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        top.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.AutoSize));

        top.Controls.Add(
            pathBox,
            0,
            0);

        top.Controls.Add(
            browseBtn,
            1,
            0);

        top.Controls.Add(
            loadBtn,
            2,
            0);

        top.Controls.Add(
            skipLocal,
            0,
            1);

        top.SetColumnSpan(
            skipLocal,
            3);

        var apiPanel =
            new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(8),
                WrapContents = true
            };

        apiPanel.Controls.Add(
            new Label
            {
                Text = "Check:",
                AutoSize = true,
                Margin =
                    new Padding(
                        3,
                        7,
                        3,
                        3)
            });

        apiPanel.Controls.Add(
            fetchCount);

        apiPanel.Controls.Add(
            new Label
            {
                Text = "new beatmapsets",
                AutoSize = true,
                Margin =
                    new Padding(
                        3,
                        7,
                        15,
                        3)
            });

        apiPanel.Controls.Add(
            new Label
            {
                Text = "Downloads:",
                AutoSize = true,
                Margin =
                    new Padding(
                        3,
                        7,
                        3,
                        3)
            });

        apiPanel.Controls.Add(
            downloadThreads);

        apiPanel.Controls.Add(
            new Label
            {
                Text = "parallel",
                AutoSize = true,
                Margin =
                    new Padding(
                        3,
                        7,
                        15,
                        3)
            });

        apiPanel.Controls.Add(
            findMissingBtn);

        apiPanel.Controls.Add(
            downloadMissingBtn);


        apiPanel.Controls.Add(
            apiStatus);

        var bottom =
            new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                AutoSize = true,
                Padding = new Padding(8)
            };

        bottom.Controls.Add(copyBtn);
        bottom.Controls.Add(status);

        Controls.Add(output);
        Controls.Add(apiPanel);
        Controls.Add(top);
        Controls.Add(bottom);

        output.BringToFront();
    }

    // ============================================================
    // EVENTS
    // ============================================================

    private void RegisterEvents()
    {
        browseBtn.Click +=
            BrowseBtn_Click;

        loadBtn.Click +=
            async (_, _) =>
            {
                await LoadAsync();
            };

        skipLocal.CheckedChanged +=
            (_, _) =>
            {
                if (loadBtn.Enabled)
                    _ = LoadAsync();
            };

        copyBtn.Click +=
            CopyBtn_Click;

        findMissingBtn.Click +=
            async (_, _) =>
            {
                await FindMissingAsync();
            };

        downloadMissingBtn.Click +=
            async (_, _) =>
            {
                await DownloadMissingAsync();
            };


    }

    // ============================================================
    // BROWSE
    // ============================================================

    private async void BrowseBtn_Click(
        object? sender,
        EventArgs e)
    {
        string defaultDir =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData),
                "osu");

        using var dlg =
            new OpenFileDialog
            {
                Title =
                    "Select client.realm",

                Filter =
                    "Realm database (*.realm)|*.realm|" +
                    "All files (*.*)|*.*",

                InitialDirectory =
                    Directory.Exists(defaultDir)
                        ? defaultDir
                        : "",

                FileName =
                    "client.realm"
            };

        if (dlg.ShowDialog(this) !=
            DialogResult.OK)
        {
            return;
        }

        pathBox.Text =
            dlg.FileName;

        loadBtn.Enabled =
            true;

        await LoadAsync();
    }

    // ============================================================
    // LOAD REALM
    // ============================================================

    private async Task LoadAsync()
    {
        string path =
            pathBox.Text;

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

        bool skip =
            skipLocal.Checked;

        SetRealmButtonsEnabled(false);

        output.Clear();

        status.Text =
            "Reading client.realm...";

        try
        {
            ids =
                await Task.Run(() =>
                    RealmReader.ReadSetIds(
                        path,
                        skip));

            output.Text =
                string.Join(
                    Environment.NewLine,
                    ids);

            status.Text =
                $"{ids.Count:N0} installed beatmapsets.";

            copyBtn.Enabled =
                ids.Count > 0;

            findMissingBtn.Enabled =
                ids.Count > 0;
        }
        catch (Exception e)
        {
            status.Text =
                "Failed.";

            MessageBox.Show(
                this,
                e.Message,
                "Failed to read realm",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            browseBtn.Enabled =
                true;

            loadBtn.Enabled =
                true;

            findMissingBtn.Enabled =
                ids.Count > 0;
        }
    }

    // ============================================================
    // COPY
    // ============================================================

    private void CopyBtn_Click(
        object? sender,
        EventArgs e)
    {
        if (string.IsNullOrEmpty(
                output.Text))
        {
            return;
        }

        Clipboard.SetText(
            output.Text);

        status.Text =
            "Copied to clipboard.";
    }

    // ============================================================
    // SAVE INSTALLED IDS
    // ============================================================

    private void SaveBtn_Click(
        object? sender,
        EventArgs e)
    {
        using var dlg =
            new SaveFileDialog
            {
                Filter =
                    "Text file (*.txt)|*.txt",

                FileName =
                    "beatmap-set-ids.txt"
            };

        if (dlg.ShowDialog(this) !=
            DialogResult.OK)
        {
            return;
        }

        File.WriteAllLines(
            dlg.FileName,
            ids.Select(
                x => x.ToString()));

        status.Text =
            $"Saved to {dlg.FileName}";
    }

    // ============================================================
    // FIND NEW SONGS
    // ============================================================

    private async Task FindMissingAsync()
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
            // Load config.yaml
            config = ConfigService.LoadConfig();

            int count = (int)fetchCount.Value;

            apiStatus.Text = "osu! API";

            browseBtn.Enabled = false;
            loadBtn.Enabled = false;
            findMissingBtn.Enabled = false;
            downloadMissingBtn.Enabled = false;

            output.Clear();
            missingMaps.Clear();

            status.Text =
                $"Requesting osu! OAuth token...";

            string token =
                await osuApi.GetAccessTokenAsync(
                    config.ClientId,
                    config.ClientSecret);

            status.Text =
                $"Searching for {count:N0} new beatmapsets...";

            missingMaps =
                await osuApi.SearchNewBeatmapsetsAsync(
                    token,
                    [.. ids],
                    count,
                    new Progress<string>(
                        message => status.Text = message));

            foreach (var map in missingMaps)
            {
                output.AppendText(
                    $"{map.Id} | " +
                    $"{map.Artist} - " +
                    $"{map.Title}" +
                    Environment.NewLine);
            }

            apiStatus.Text =
                $"{missingMaps.Count:N0} new";

            status.Text =
                $"Found {missingMaps.Count:N0} " +
                "beatmapsets not in your Lazer database.";

            downloadMissingBtn.Enabled =
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

        }
    }

    // ============================================================
    // DOWNLOAD NEW SONGS
    // ============================================================

    private async Task DownloadMissingAsync()
    {
        if (config == null)
        {
            try
            {
                config = ConfigService.LoadConfig();
            }
            catch (Exception e)
            {
                MessageBox.Show(
                    this,
                    e.Message,
                    "Configuration Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                return;
            }
        }

        if (missingMaps.Count == 0)
            return;

        using var dlg =
            new FolderBrowserDialog
            {
                Description =
                    "Select where to download " +
                    "the new .osz files.",

                UseDescriptionForTitle =
                    true
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

        browseBtn.Enabled =
            false;

        loadBtn.Enabled =
            false;

        findMissingBtn.Enabled =
            false;

        downloadMissingBtn.Enabled =
            false;


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
                await downloadService
                    .DownloadBeatmapsAsync(
                        missingMaps,
                        destination,
                        threads,
                        config,
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
            browseBtn.Enabled =
                true;

            loadBtn.Enabled =
                true;

            findMissingBtn.Enabled =
                ids.Count > 0;

            downloadMissingBtn.Enabled =
                missingMaps.Count > 0;

        }
    }

    // ============================================================
    // BUTTON HELPERS
    // ============================================================

    private void SetRealmButtonsEnabled(
        bool enabled)
    {
        browseBtn.Enabled =
            enabled;

        loadBtn.Enabled =
            enabled;

        copyBtn.Enabled =
            enabled && ids.Count > 0;

        findMissingBtn.Enabled =
            enabled && ids.Count > 0;
    }

    private void LoadDefaultRealm()
    {
        try
        {
            config = ConfigService.LoadConfig();

            string path =
                Environment.ExpandEnvironmentVariables(
                    config.ClientRealm);

            pathBox.Text = path;

            if (File.Exists(path))
            {
                loadBtn.Enabled = true;

                _ = LoadAsync();
            }
            else
            {
                status.Text =
                    $"client.realm not found: {path}";
            }
        }
        catch (Exception e)
        {
            status.Text =
                "config.yaml not loaded.";


            MessageBox.Show(
                this,
                e.Message,
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
    private void LoadConfigIntoUI()
    {
        try
        {
            config = ConfigService.LoadConfig();

            fetchCount.Value = Math.Clamp(
                config.FetchCount,
                (int)fetchCount.Minimum,
                (int)fetchCount.Maximum);

            downloadThreads.Value = Math.Clamp(
                config.DownloadThreads,
                (int)downloadThreads.Minimum,
                (int)downloadThreads.Maximum);

            status.Text =
                $"Config loaded. Checking {fetchCount.Value:N0} beatmapsets.";
        }
        catch (Exception e)
        {
            status.Text =
                "config.yaml not loaded.";

            MessageBox.Show(
                this,
                e.Message,
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
