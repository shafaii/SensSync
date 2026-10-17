using System.Globalization;
using System.Runtime.InteropServices;
using SensSync.Core;

namespace SensSync.Desktop;

internal sealed class MainForm : Form
{
    private readonly Func<GameId, SourceDiscovery> readSettings;
    private readonly Action<string> copy;
    private readonly bool monitorGames;
    private readonly CheckBox darkMode = new() { Name = "DarkMode", Text = "Dark &mode", AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly Label detection = new() { Name = "Detection", Text = "Waiting for a game", AutoSize = true };
    private readonly Label sourceGame = new() { Name = "SourceGame", Text = "Open a game", AutoSize = true, Anchor = AnchorStyles.Left };
    private readonly TextBox sensitivity = new() { Name = "Sensitivity", AccessibleName = "Read sensitivity", ReadOnly = true, Dock = DockStyle.Fill };
    private readonly ComboBox dpi = Combo("Dpi", "Mouse DPI");
    private readonly ComboBox target = Combo("Target", "Target game");
    private readonly ComboBox profile = Combo("Profile", "Saved account or hero profile");
    private readonly ComboBox runningGame = Combo("RunningGame", "Running source game");
    private readonly TextBox result = new() { Name = "Result", ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Text = "—" };
    private readonly Label status = new() { Name = "Status", AutoSize = true, Dock = DockStyle.Fill };
    private readonly Label copyFeedback = new() { Name = "CopyFeedback", AutoSize = true, AccessibleName = "Copy status", Margin = new(10, 12, 0, 0) };
    private readonly Button copyButton = new() { Name = "Copy", Text = "&Copy sensitivity", AutoSize = true, Enabled = false };
    private readonly System.Windows.Forms.Timer feedbackTimer = new() { Interval = 3000 };
    private readonly Font titleFont = new("Segoe UI", 19, FontStyle.Bold);
    private readonly Font resultFont = new("Segoe UI", 25, FontStyle.Bold);
    private readonly TableLayoutPanel profileRow;
    private readonly TableLayoutPanel runningRow;
    private WindowsGameMonitor? monitor;
    private GameDetectionState state = new(null, false, false, true);
    private GameId? source;
    private SourceReading? reading;
    private SourceDiscovery discovery = new([]);
    private DateTimeOffset lastRead;
    private string? copiedValue;
    private string? exactValue;
    private bool updating;

    public MainForm(Func<GameId, SourceDiscovery>? readSettings = null, Action<string>? copy = null, bool monitorGames = true)
    {
        this.readSettings = readSettings ?? GameSettingsLocations.Read;
        this.copy = copy ?? Clipboard.SetText;
        this.monitorGames = monitorGames;
        Name = Text = "SensSync";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(520, 590);
        MinimumSize = new Size(536, 629);
        StartPosition = FormStartPosition.CenterScreen;
        var body = new TableLayoutPanel { Name = "Body", Dock = DockStyle.Fill, ColumnCount = 1, Padding = new(24, 18, 24, 16), AutoScroll = true };
        body.ColumnStyles.Add(new(SizeType.Percent, 100));
        Controls.Add(body);
        void Add(Control control)
        {
            int row = body.RowCount++;
            body.RowStyles.Add(new(SizeType.AutoSize));
            control.Margin = new(0, 0, 0, 6);
            body.Controls.Add(control, 0, row);
        }
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        header.ColumnStyles.Add(new(SizeType.Percent, 100));
        header.ColumnStyles.Add(new(SizeType.AutoSize));
        header.Controls.Add(new Label { Text = "SensSync", Font = titleFont, AutoSize = true }, 0, 0);
        header.Controls.Add(darkMode, 1, 0);
        Add(header);
        var badges = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        badges.Controls.Add(new Label { Name = "ReadOnlyBadge", Text = "Read-only", AutoSize = true, Padding = new(7, 3, 7, 3) });
        detection.Margin = new(10, 3, 0, 0);
        badges.Controls.Add(detection);
        Add(badges);
        var inputs = UiTheme.Card("InputsCard");
        inputs.Controls.Add(Field("Source game", sourceGame));
        runningRow = Field("&Running game", runningGame);
        inputs.Controls.Add(runningRow);
        inputs.Controls.Add(Field("Read sensitivity", sensitivity));
        dpi.Items.AddRange(new object[] { "400", "800", "1200", "1600", "2400", "3200", "6400" });
        dpi.SelectedItem = "1600";
        inputs.Controls.Add(Field("Mouse &DPI", dpi));
        profileRow = Field("Saved &profile", profile);
        inputs.Controls.Add(profileRow);
        var refresh = new Button { Name = "Refresh", Text = "&Refresh reading", AutoSize = true };
        Add(inputs);
        var output = UiTheme.Card("ResultCard");
        target.Items.AddRange(ConversionTarget.All.Cast<object>().ToArray());
        target.SelectedIndex = 0;
        output.Controls.Add(Field("&Target game", target));
        result.Font = resultFont;
        output.Controls.Add(result);
        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        actions.Controls.AddRange([copyButton, copyFeedback]);
        output.Controls.Add(actions);
        Add(output);
        Add(status);
        var footer = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        var details = new Button { Name = "Details", Text = "De&tails…", AutoSize = true };
        footer.Controls.Add(refresh);
        footer.Controls.Add(details);
        Add(footer);
        profileRow.Visible = runningRow.Visible = false;
        status.MaximumSize = new(470, 0);
        detection.MaximumSize = new(350, 0);
        body.SizeChanged += (_, _) => status.MaximumSize = new(Math.Max(180, body.ClientSize.Width - body.Padding.Horizontal), 0);
        UiTheme.StyleButtons(this, copyButton);
        foreach (var combo in new[] { dpi, target, profile, runningGame }) UiTheme.ConfigureCombo(combo);
        darkMode.CheckedChanged += (_, _) => ApplyTheme();
        copyButton.EnabledChanged += (_, _) => ApplyTheme();
        HandleCreated += (_, _) => ApplyTheme();
        dpi.SelectedIndexChanged += (_, _) => Recalculate();
        target.SelectedIndexChanged += (_, _) => Recalculate();
        profile.SelectedIndexChanged += (_, _) => { if (!updating) { reading = profile.SelectedItem is ProfileItem item ? item.Reading : null; Recalculate(); } };
        runningGame.SelectedIndexChanged += (_, _) => { if (!updating && runningGame.SelectedItem is GameItem item) SetSource(item.Game); };
        refresh.Click += (_, _) => { if (monitor is not null) monitor.Poll(); RefreshReading(); };
        copyButton.Click += (_, _) => CopyResult();
        feedbackTimer.Tick += (_, _) => { feedbackTimer.Stop(); copyFeedback.Text = ""; };
        details.Click += (_, _) => { using var dialog = CreateDetailsDialog(); dialog.ShowDialog(this); };
        Shown += (_, _) =>
        {
            if (this.monitorGames) { monitor = new WindowsGameMonitor(); monitor.Changed += UpdateDetection; monitor.Poll(); }
        };
        ApplyTheme();
        Recalculate();
    }

    private static ComboBox Combo(string name, string accessible) => new() { Name = name, AccessibleName = accessible, DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private static TableLayoutPanel Field(string label, Control control)
    {
        var row = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = new(0, 3, 0, 5) };
        row.ColumnStyles.Add(new(SizeType.Absolute, 135));
        row.ColumnStyles.Add(new(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        row.Controls.Add(control, 1, 0);
        return row;
    }
    private void ApplyTheme() => UiTheme.Apply(this, darkMode.Checked, copyButton);
    private sealed record GameItem(GameId Game) { public override string ToString() => GameDetectionPresentation.Name(Game); }
    private sealed record ProfileItem(SourceReading Reading) { public override string ToString() => Reading.Label; }

    internal void UpdateDetection(GameDetectionState next)
    {
        bool focusChanged = next.LastRelevantGame != state.LastRelevantGame;
        state = next;
        var games = next.RunningGames.ToArray();
        updating = true;
        runningGame.Items.Clear();
        runningGame.Items.AddRange(games.Select(g => (object)new GameItem(g)).ToArray());
        GameId? chosen = focusChanged && next.LastRelevantGame is { } focused && games.Contains(focused) ? focused
            : source is { } previous && games.Contains(previous) ? previous
            : next.LastRelevantGame is { } last && games.Contains(last) ? last : games.Length == 1 ? games[0] : null;
        runningGame.SelectedItem = chosen is { } selected ? runningGame.Items.Cast<GameItem>().First(i => i.Game == selected) : null;
        runningRow.Visible = games.Length > 1;
        updating = false;
        detection.Text = games.Length == 0 ? (next.DetectionAvailable ? "No game detected" : "Detection unavailable")
            : next.DetectionAvailable ? "Game detected" : "Detection incomplete";
        if (chosen != source) SetSource(chosen);
        else if (source is not null && DateTimeOffset.UtcNow - lastRead >= TimeSpan.FromSeconds(2)) RefreshReading();
        else if (source is null) Recalculate();
    }

    private void SetSource(GameId? game)
    {
        source = game;
        reading = null;
        discovery = new([]);
        profile.Items.Clear();
        profileRow.Visible = false;
        sourceGame.Text = game is { } id ? GameDetectionPresentation.Name(id) : "Open a game";
        RefreshReading();
    }

    internal void RefreshReading()
    {
        if (source is null) { reading = null; Recalculate(); return; }
        lastRead = DateTimeOffset.UtcNow;
        var oldReading = reading;
        string? oldError = discovery.Error;
        string? oldPath = reading?.Path;
        string? oldLabel = reading?.Label;
        try { discovery = readSettings(source.Value); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { discovery = new([], "Saved sensitivity could not be read. Try Refresh."); }
        updating = true;
        profile.Items.Clear();
        foreach (var item in discovery.Readings.Where(r => r.Game == source)) profile.Items.Add(new ProfileItem(item));
        var readings = profile.Items.Cast<ProfileItem>().Select(i => i.Reading).ToArray();
        bool ambiguous = readings.Select(r => (r.Sensitivity, r.DegreesPerCount)).Distinct().Count() > 1;
        reading = readings.FirstOrDefault(r => r.Path == oldPath && r.Label == oldLabel) ?? (!ambiguous ? readings.FirstOrDefault() : null);
        profileRow.Visible = ambiguous;
        profile.SelectedItem = reading is null ? null : profile.Items.Cast<ProfileItem>().First(i => i.Reading == reading);
        updating = false;
        if (reading != oldReading || discovery.Error != oldError || copiedValue is null) Recalculate();
    }

    private void Recalculate()
    {
        feedbackTimer.Stop();
        copyFeedback.Text = "";
        copiedValue = exactValue = null;
        copyButton.Enabled = false;
        result.Text = "—";
        sensitivity.Text = reading?.Sensitivity.ToString("0.#########", CultureInfo.InvariantCulture) ?? "—";
        if (reading is null)
        {
            status.Text = source is null ? (state.RunningGames.Any() ? "Choose the running game you want to convert from." : "Open a supported game to read its saved sensitivity.")
                : profileRow.Visible ? "Choose your saved account or hero profile." : discovery.Error ?? "Saved sensitivity is unavailable.";
            return;
        }
        try
        {
            var destination = (ConversionTarget)target.SelectedItem!;
            result.AccessibleName = $"Equivalent {destination.Name} sensitivity";
            double mouseDpi = double.Parse(dpi.Text, CultureInfo.InvariantCulture);
            LinearSensitivityModel.RequirePositive(reading.DegreesPerCount, "Saved turn rate");
            // Physical conversion stays internal; the user sees only the game settings.
            double value = destination.FromCmPer360(914.4 / mouseDpi / reading.DegreesPerCount, mouseDpi);
            string clipboard = value.ToString("0.#########", CultureInfo.InvariantCulture);
            if (double.Parse(clipboard, CultureInfo.InvariantCulture) == 0 && value != 0)
                throw new ArgumentException("The result is too small to copy accurately.");
            copiedValue = clipboard;
            exactValue = value.ToString("G17", CultureInfo.InvariantCulture);
            result.Text = (value == 0 || value >= 0.00005) && value < 1000000 ? value.ToString("0.0000", CultureInfo.InvariantCulture) : value.ToString("0.###E+0", CultureInfo.InvariantCulture);
            status.Text = source == GameId.Battlefield6 || destination == ConversionTarget.Battlefield6
                ? "Saved sensitivity read · BF6 conversion is experimental."
                : "Saved sensitivity read. Ready to copy.";
            copyButton.Enabled = true;
        }
        catch (ArgumentException e) { status.Text = e.Message; }
    }

    private void CopyResult()
    {
        if (copiedValue is null) return;
        try { copy(copiedValue); copyFeedback.Text = "Copied!"; feedbackTimer.Start(); }
        catch (ExternalException) { copyFeedback.Text = ""; status.Text = "Clipboard is busy. Try Copy again."; }
    }

    internal DetailsForm CreateDetailsDialog() => new(
        $"Source: {sourceGame.Text}\r\n{reading?.Label}\r\n{reading?.Path}\r\n\r\nRead from saved settings. Unsaved in-game changes may differ. Values refresh while the game is running.\r\n" +
        (discovery.Readings.Count > 1 && !profileRow.Visible ? "All readable profiles report the same sensitivity and turn rate." : ""),
        $"Read sensitivity: {sensitivity.Text}\r\nMouse DPI: {dpi.Text}\r\nTarget: {target.Text}\r\nFull result: {exactValue ?? "Unavailable"}\r\nClipboard value: {copiedValue ?? "Unavailable"}",
        $"{reading?.Notes}\r\n{((ConversionTarget)target.SelectedItem!).Notes}\r\nNo settings are applied automatically. Horizontal hipfire only; scoped aim may differ.",
        $"{GameDetectionPresentation.Details(state, reading?.Game == GameId.Cs2)}\r\n{discovery.Error}\r\nSHA-256: {reading?.Sha256}", darkMode.Checked);

    protected override void Dispose(bool disposing)
    {
        if (disposing) { monitor?.Dispose(); feedbackTimer.Dispose(); }
        base.Dispose(disposing);
        if (disposing) { titleFont.Dispose(); resultFont.Dispose(); }
    }
}
