using System.Globalization;
using System.Runtime.InteropServices;
using SensSync.Core;

namespace SensSync.Desktop;

internal sealed class MainForm : Form
{
    private readonly Func<DiscoveryResult> discover;
    private readonly Action<string> copy;
    private readonly bool monitorGames;
    private readonly ComboBox source = new() { Name = "Source", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly ComboBox account = new() { Name = "Account", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox dpi = Input("Dpi", "Enter mouse DPI, e.g. 800");
    private readonly TextBox sensitivity = Input("Sensitivity", "CS2 sensitivity");
    private readonly TextBox yaw = Input("Yaw", "Degrees per count at sensitivity 1");
    private readonly TextBox distance = Input("Distance", "Distance for one full turn");
    private readonly TextBox result = new() { Name = "Result", ReadOnly = true, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, TabStop = true };
    private readonly Label physical = new() { Name = "Physical", AutoSize = true };
    private readonly Label status = new() { Name = "Status", AutoSize = true, Dock = DockStyle.Fill };
    private readonly Label detection = new() { Name = "Detection", AutoSize = true, Dock = DockStyle.Fill };
    private readonly Label note = new() { Name = "Note", AutoSize = true, Dock = DockStyle.Fill };
    private readonly Button copyButton = new() { Name = "Copy", Text = "&Copy Valorant value", AutoSize = true, Enabled = false, Padding = new(10, 5, 10, 5) };
    private readonly Button details = new() { Name = "Details", Text = "&Details…", AutoSize = true };
    private readonly CheckBox darkMode = new() { Name = "DarkMode", Text = "Dark &mode", AccessibleName = "Dark mode", AutoSize = true, Anchor = AnchorStyles.Right };
    private readonly CheckBox advanced = new() { Name = "Advanced", Text = "&Advanced: custom yaw", AutoSize = true };
    private readonly Label copyFeedback = new() { Name = "CopyFeedback", AutoSize = true, AccessibleName = "Copy status", Anchor = AnchorStyles.Left };
    private readonly System.Windows.Forms.Timer feedbackTimer = new() { Interval = 3000 };
    private string? exactValue;
    private readonly TableLayoutPanel accountRow = Row();
    private readonly TableLayoutPanel sensitivityRow = Row();
    private readonly TableLayoutPanel yawRow = Row();
    private readonly TableLayoutPanel distanceRow = Row();
    private readonly FlowLayoutPanel fileActions = new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
    private WindowsGameMonitor? monitor;
    private GameDetectionState detectionState = new(null, false, false, true);
    private Cs2Snapshot? snapshot;
    private string? copiedValue;
    private string readMessage = "Choose a configuration or enter sensitivity manually.";
    private string overrideMessage = "";
    private string discoveryWarnings = "";
    private bool updating;
    private readonly Font titleFont = new("Segoe UI", 19, FontStyle.Bold);
    private readonly Font resultFont = new("Segoe UI", 23, FontStyle.Bold);

    public MainForm(Func<DiscoveryResult>? discover = null, Action<string>? copy = null, bool monitorGames = true)
    {
        this.discover = discover ?? SteamLocations.Discover;
        this.copy = copy ?? Clipboard.SetText;
        this.monitorGames = monitorGames;
        Text = "SensSync";
        Name = "SensSync";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(520, 690);
        MinimumSize = new Size(536, 729);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UiTheme.Background;
        ForeColor = UiTheme.Ink;

        var body = new TableLayoutPanel { Name = "Body", Dock = DockStyle.Fill, ColumnCount = 1,
            Padding = new(24, 18, 24, 16), AutoScroll = true };
        body.ColumnStyles.Add(new(SizeType.Percent, 100));
        Controls.Add(body);
        void Add(Control control)
        {
            int row = body.RowCount++;
            body.RowStyles.Add(new(SizeType.AutoSize));
            control.Margin = new Padding(0, 0, 0, 8);
            body.Controls.Add(control, 0, row);
        }
        var header = UiTheme.Stack();
        var titleRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
        titleRow.ColumnStyles.Add(new(SizeType.Percent, 100));
        titleRow.ColumnStyles.Add(new(SizeType.AutoSize));
        titleRow.Controls.Add(new Label { Text = "SensSync", Font = titleFont, AutoSize = true, Margin = Padding.Empty }, 0, 0);
        titleRow.Controls.Add(darkMode, 1, 0);
        header.Controls.Add(titleRow);
        header.Controls.Add(UiTheme.Caption("Your sensitivity, carried across games."));
        var badges = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new(0, 8, 0, 0), WrapContents = true };
        badges.Controls.Add(new Label { Name = "ReadOnlyBadge", Text = "Read-only", AutoSize = true, BackColor = UiTheme.Tint,
            ForeColor = UiTheme.Ink, Padding = new(7, 3, 7, 3), Margin = new(0, 0, 10, 0) });
        detection.Text = "No game detected";
        detection.Dock = DockStyle.None;
        detection.Margin = new(0, 3, 0, 0);
        detection.ForeColor = UiTheme.Muted;
        badges.Controls.Add(detection);
        header.Controls.Add(badges);
        Add(header);

        source.Items.AddRange(["Saved CS2 settings", "Manual CS2 sensitivity", "Physical distance (cm/360)"]);
        UiTheme.ConfigureCombo(source);
        UiTheme.ConfigureCombo(account);
        source.AccessibleName = "Conversion source";
        account.AccessibleName = "CS2 account";
        Add(Field("&Source", source));
        var inputs = UiTheme.Card("InputsCard");
        inputs.Controls.Add(Field("Mouse &DPI", dpi));
        FillRow(accountRow, "CS2 &account", account);
        inputs.Controls.Add(accountRow);
        var refresh = new Button { Name = "Refresh", Text = "&Refresh", AutoSize = true };
        var browse = new Button { Name = "Browse", Text = "Choose &file…", AutoSize = true };
        fileActions.Margin = Padding.Empty;
        fileActions.Controls.AddRange([refresh, browse]);
        inputs.Controls.Add(fileActions);
        FillRow(sensitivityRow, "CS2 se&nsitivity", sensitivity);
        FillRow(yawRow, "CS2 &yaw", yaw);
        FillRow(distanceRow, "&Distance (cm/360)", distance);
        inputs.Controls.Add(sensitivityRow);
        inputs.Controls.Add(distanceRow);
        inputs.Controls.Add(advanced);
        inputs.Controls.Add(yawRow);
        Add(inputs);

        var output = UiTheme.Card("ResultCard");
        output.BackColor = UiTheme.Tint;
        result.BackColor = output.BackColor;
        result.ForeColor = UiTheme.Ink;
        result.Font = resultFont;
        result.AccessibleName = "Equivalent Valorant sensitivity, rounded display";
        result.Text = "—";
        output.Controls.Add(UiTheme.Caption("VALORANT · horizontal hipfire"));
        output.Controls.Add(result);
        physical.ForeColor = UiTheme.Muted;
        output.Controls.Add(physical);
        var copyRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new(0, 10, 0, 0) };
        copyButton.Text = "&Copy sensitivity";
        copyRow.Controls.Add(copyButton);
        copyFeedback.Margin = new(10, 12, 0, 0);
        copyRow.Controls.Add(copyFeedback);
        output.Controls.Add(copyRow);
        Add(output);
        status.ForeColor = UiTheme.Muted;
        Add(status);
        note.ForeColor = UiTheme.Muted;
        note.Text = "No settings are applied automatically.\nFOV and scoped aim may feel different.";
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new(SizeType.AutoSize));
        note.Anchor = AnchorStyles.Left;
        details.Anchor = AnchorStyles.Right;
        footer.Controls.Add(note, 0, 0);
        footer.Controls.Add(details, 1, 0);
        Add(footer);
        UiTheme.StyleButtons(this, copyButton);
        copyButton.EnabledChanged += (_, _) => ApplyTheme();
        darkMode.CheckedChanged += (_, _) => ApplyTheme();
        HandleCreated += (_, _) => ApplyTheme();
        ApplyTheme();
        void ResizeLabels()
        {
            int width = Math.Max(180, body.ClientSize.Width - body.Padding.Horizontal - 8);
            status.MaximumSize = new Size(width, 0);
            note.MaximumSize = new Size(Math.Max(100, width - details.PreferredSize.Width - 12), 0);
            detection.MaximumSize = new Size(Math.Max(100, width - 110), 0);
        }
        body.SizeChanged += (_, _) => ResizeLabels();
        advanced.CheckedChanged += (_, _) => yawRow.Visible = source.SelectedIndex == 1 && advanced.Checked;
        feedbackTimer.Tick += (_, _) => { feedbackTimer.Stop(); copyFeedback.Text = ""; };
        source.SelectedIndexChanged += (_, _) => SwitchSource();
        account.SelectedIndexChanged += (_, _) => { if (!updating) ReadSelected(); };
        foreach (var input in new[] { dpi, sensitivity, yaw, distance }) input.TextChanged += (_, _) => Recalculate();
        refresh.Click += (_, _) => RefreshConfigs();
        browse.Click += (_, _) => Browse();
        details.Click += (_, _) => ShowDetails();
        copyButton.Click += (_, _) => CopyResult();
        source.SelectedIndex = 0;
        Shown += (_, _) =>
        {
            RefreshConfigs();
            if (this.monitorGames)
            {
                monitor = new WindowsGameMonitor();
                monitor.Changed += UpdateDetection;
                monitor.Poll();
            }
            dpi.Focus();
        };
        ResizeLabels();
    }

    private void ApplyTheme() => UiTheme.Apply(this, darkMode.Checked, copyButton);

    private static TextBox Input(string name, string placeholder) => new()
    { Name = name, AccessibleName = name == "Dpi" ? "Mouse DPI" : name, PlaceholderText = placeholder, Dock = DockStyle.Fill };
    private static TableLayoutPanel Row() => new() { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill, Margin = new(0, 3, 0, 5) };
    private static TableLayoutPanel Field(string caption, Control control)
    {
        var row = Row();
        FillRow(row, caption, control);
        return row;
    }
    private static void FillRow(TableLayoutPanel row, string caption, Control control)
    {
        row.ColumnStyles.Add(new(SizeType.Absolute, 135));
        row.ColumnStyles.Add(new(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new(0, 5, 6, 5) }, 0, 0);
        control.Margin = new Padding(0, 3, 0, 3);
        row.Controls.Add(control, 1, 0);
    }

    private void SwitchSource()
    {
        bool saved = source.SelectedIndex == 0;
        bool cm = source.SelectedIndex == 2;
        accountRow.Visible = fileActions.Visible = saved;
        sensitivityRow.Visible = !cm;
        advanced.Visible = !cm && !saved;
        yawRow.Visible = !cm && !saved && advanced.Checked;
        distanceRow.Visible = cm;
        sensitivity.ReadOnly = yaw.ReadOnly = saved;
        if (saved) ReadSelected();
        else
        {
            if (source.SelectedIndex == 1 && string.IsNullOrWhiteSpace(yaw.Text)) yaw.Text = "0.022";
            Recalculate();
        }
    }

    internal void RefreshConfigs()
    {
        var selected = account.SelectedItem as Cs2ConfigCandidate;
        DiscoveryResult found;
        try { found = discover(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { found = new([], ["Configuration discovery failed. Choose a file or use manual input."]); }
        discoveryWarnings = string.Join(Environment.NewLine, found.Warnings);
        var candidates = found.Candidates.ToList();
        if (selected is { SteamRoot: null } && File.Exists(selected.Path) && !candidates.Any(c => c.Path.Equals(selected.Path, StringComparison.OrdinalIgnoreCase)))
            candidates.Add(selected);
        var chosen = SteamDiscovery.ChooseUnambiguous(candidates, selected?.Path);
        updating = true;
        account.Items.Clear();
        account.Items.Add(candidates.Count == 0 ? "No configuration found" : "Choose a CS2 account…");
        foreach (var candidate in candidates) account.Items.Add(candidate);
        account.SelectedIndex = chosen is null ? 0 : account.Items.IndexOf(chosen);
        updating = false;
        ReadSelected();
    }

    private void ReadSelected()
    {
        snapshot = null;
        overrideMessage = "";
        if (account.SelectedItem is Cs2ConfigCandidate candidate)
        {
            var read = Cs2Configuration.Read(candidate.Path);
            snapshot = read.Snapshot;
            readMessage = read.Error ?? "Saved CS2 settings loaded. This is not a live in-game reading.";
            if (snapshot is not null) overrideMessage = SteamLocations.DescribeOverrides(candidate);
        }
        else readMessage = account.Items.Count > 2
            ? "Multiple configurations found. Choose your CS2 account first."
            : "No configuration selected. Choose a file or use a manual source.";
        if (source.SelectedIndex == 0)
        {
            updating = true;
            sensitivity.Text = snapshot?.Settings.Sensitivity.ToString("G", CultureInfo.InvariantCulture) ?? "";
            yaw.Text = snapshot?.Settings.Yaw.ToString("G", CultureInfo.InvariantCulture) ?? "";
            updating = false;
        }
        UpdateDetection(detectionState);
        Recalculate();
    }

    private void Recalculate()
    {
        if (updating) return;
        feedbackTimer.Stop();
        copyFeedback.Text = "";
        exactValue = null;
        copiedValue = null;
        copyButton.Enabled = false;
        result.Text = "—";
        physical.Text = "Enter your mouse DPI to begin";
        try
        {
            if (source.SelectedIndex == 0 && snapshot is null) { status.Text = readMessage; physical.Text = "Choose a saved configuration to begin"; return; }
            double mouseDpi = Number(dpi.Text, "mouse DPI");
            double cm = source.SelectedIndex == 2 ? Number(distance.Text, "distance in cm/360")
                : new LinearSensitivityModel(Number(yaw.Text, "CS2 yaw")).ToCmPer360(Number(sensitivity.Text, "CS2 sensitivity"), mouseDpi);
            var model = new LinearSensitivityModel(LinearSensitivityModel.ValorantYawCandidate);
            double valorant = model.FromCmPer360(cm, mouseDpi);
            string display = valorant.ToString("0.#########", CultureInfo.InvariantCulture);
            if (!double.TryParse(display, CultureInfo.InvariantCulture, out double rounded) || rounded <= 0)
                throw new ArgumentException("The result is too small to copy at nine decimal places.");
            copiedValue = display;
            exactValue = valorant.ToString("G17", CultureInfo.InvariantCulture);
            result.Text = valorant >= 0.00005 && valorant < 1000000
                ? valorant.ToString("0.0000", CultureInfo.InvariantCulture)
                : valorant.ToString("0.###E+0", CultureInfo.InvariantCulture);
            physical.Text = $"≈ {cm.ToString("0.##", CultureInfo.CurrentCulture)} cm / 360°";
            status.Text = source.SelectedIndex == 0
                ? "Saved values · startup overrides may differ. See Details."
                : "Ready to copy into Valorant.";
            if (!string.IsNullOrEmpty(discoveryWarnings) && source.SelectedIndex == 0)
                status.Text += " Discovery was incomplete; see Details.";
            copyButton.Enabled = true;
        }
        catch (ArgumentException e) { status.Text = e.Message; physical.Text = "Complete the source values above"; }
    }

    private static double Number(string text, string label)
    {
        if (!(double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ||
              double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) || !double.IsFinite(value) || value <= 0)
            throw new ArgumentException($"Enter a finite {label} greater than zero.");
        return value;
    }

    private void CopyResult()
    {
        if (copiedValue is null) return;
        try
        {
            copy(copiedValue);
            copyFeedback.Text = "Copied!";
            feedbackTimer.Stop();
            feedbackTimer.Start();
        }
        catch (ExternalException) { feedbackTimer.Stop(); copyFeedback.Text = ""; status.Text = "Clipboard is busy. Try Copy again or copy the value from Details."; }
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Title = "Choose an existing CS2 convars configuration", Filter = "CS2 configuration (*.vcfg)|*.vcfg", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var candidate = new Cs2ConfigCandidate(dialog.FileName, "selected file", null);
        account.Items.Add(candidate);
        account.SelectedItem = candidate;
    }

    private void ShowDetails()
    {
        using var dialog = CreateDetailsDialog();
        dialog.ShowDialog(this);
    }

    internal DetailsForm CreateDetailsDialog()
    {
        bool saved = source.SelectedIndex == 0;
        string sourceInfo = saved
            ? readMessage + (snapshot is null ? "" : $"\r\n\r\nSaved file: {snapshot.Path}\r\nRead at: {snapshot.ReadAt.ToLocalTime():g}")
            : source.SelectedIndex == 1 ? "Manually entered CS2 sensitivity and yaw." : "Manually entered physical distance (cm/360).";
        string values = $"Mouse DPI (entered): {dpi.Text}\r\n" +
            (source.SelectedIndex == 2 ? $"Distance: {distance.Text} cm/360" : $"CS2 sensitivity: {sensitivity.Text}\r\nCS2 yaw: {yaw.Text}") +
            (saved && snapshot is not null ? $"\r\nPitch: {snapshot.Settings.Pitch}\r\nVertical scale: {snapshot.Settings.VerticalScale}" : "") +
            $"\r\n\r\nValorant (full calculation precision): {exactValue ?? "Unavailable"}\r\nClipboard value: {copiedValue ?? "Unavailable"}";
        string notes = "Horizontal hipfire only. FOV, scoped aim and vertical response may differ. External acceleration can invalidate this calculation.\r\n\r\n" +
            "Valorant yaw 0.07 is a research-backed candidate, pending actual-game validation. Copy rounds to nine decimal places; Valorant may round further.\r\n\r\n" +
            "No game files are modified. Mouse DPI is entered manually; it is never detected or changed.";
        string diagnostics = saved
            ? (snapshot is null ? "No saved configuration loaded." : $"SHA-256: {snapshot.Sha256}") +
                "\r\n\r\n" + overrideMessage + "\r\n\r\n" + discoveryWarnings
            : "Saved-file diagnostics do not apply to this source.";
        diagnostics = GameDetectionPresentation.Details(detectionState, snapshot is not null) + "\r\n\r\n" + diagnostics;
        return new DetailsForm(sourceInfo, values, notes, diagnostics.Trim(), darkMode.Checked);
    }

    internal void UpdateDetection(GameDetectionState state)
    {
        detectionState = state;
        detection.Text = GameDetectionPresentation.Summary(state, snapshot is not null);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { monitor?.Dispose(); feedbackTimer.Dispose(); }
        base.Dispose(disposing);
        if (disposing) { titleFont.Dispose(); resultFont.Dispose(); }
    }
}
