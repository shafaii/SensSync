using SensSync.Core;
using SensSync.Desktop;

namespace SensSync.Tests;

public sealed class WindowTests
{
    [Fact]
    public void SavedConfigurationRequiresDpiAndInvalidInputClearsCopy()
    {
        using var files = new TestFiles();
        string path = files.Write("test.vcfg", TestFiles.Standard);
        var candidate = new Cs2ConfigCandidate(path, "test", null);
        RunSta(() =>
        {
            string? clipboard = null;
            using var form = new MainForm(() => new([candidate], []), value => clipboard = value, false);
            form.RefreshConfigs();
            Assert.False(Find<Button>(form, "Copy").Enabled);
            Find<TextBox>(form, "Dpi").Text = "800";
            Assert.Equal("0.3143", Find<TextBox>(form, "Result").Text);
            Assert.True(Find<Button>(form, "Copy").Enabled);
            form.Show();
            Find<Button>(form, "Copy").PerformClick();
            Assert.Equal("0.314285714", clipboard);
            Assert.Equal("Copied!", Find<Label>(form, "CopyFeedback").Text);
            Find<TextBox>(form, "Dpi").Text = "NaN";
            Assert.False(Find<Button>(form, "Copy").Enabled);
            Assert.Equal("—", Find<TextBox>(form, "Result").Text);
            Assert.Empty(Find<Label>(form, "CopyFeedback").Text);
            form.Close();
        });
    }

    [Fact]
    public void AmbiguousAccountsDoNotLeakFirstAccountIntoResult()
    {
        using var files = new TestFiles();
        var first = new Cs2ConfigCandidate(files.Write("one.vcfg", TestFiles.Standard), "one", null);
        var second = new Cs2ConfigCandidate(files.Write("two.vcfg", TestFiles.Standard.Replace("\"1.0\"", "\"2.0\"")), "two", null);
        RunSta(() =>
        {
            using var form = new MainForm(() => new([first, second], []), _ => { }, false);
            form.RefreshConfigs();
            Find<TextBox>(form, "Dpi").Text = "800";
            Assert.False(Find<Button>(form, "Copy").Enabled);
            Find<ComboBox>(form, "Account").SelectedItem = second;
            Assert.Equal("0.6286", Find<TextBox>(form, "Result").Text);
        });
    }

    [Fact]
    public void PhysicalModeHoldsDistanceWhenDpiChanges()
    {
        RunSta(() =>
        {
            using var form = new MainForm(() => new([], []), _ => { }, false);
            Find<ComboBox>(form, "Source").SelectedIndex = 2;
            Find<TextBox>(form, "Distance").Text = "40";
            Find<TextBox>(form, "Dpi").Text = "800";
            Assert.Equal("0.4082", Find<TextBox>(form, "Result").Text);
            Find<TextBox>(form, "Dpi").Text = "1600";
            Assert.Equal("0.2041", Find<TextBox>(form, "Result").Text);
        });
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void SourceLayoutsFitAndAdvancedYawRemainsAvailable(float scale)
    {
        RunSta(() =>
        {
            Application.EnableVisualStyles();
            using var files = new TestFiles();
            var candidate = new Cs2ConfigCandidate(files.Write("sample.vcfg", TestFiles.Standard), "sample", null);
            using var form = new MainForm(() => new([candidate], []), _ => { }, false);
            form.Font = new Font("Segoe UI", 10);
            form.Show();
            if (scale != 1) form.Scale(new SizeF(scale, scale));
            foreach (int mode in new[] { 0, 1, 2 })
            {
                Find<ComboBox>(form, "Source").SelectedIndex = mode;
                Find<TextBox>(form, "Dpi").Text = "800";
                if (mode == 1) Find<TextBox>(form, "Sensitivity").Text = "0.64";
                if (mode == 2) Find<TextBox>(form, "Distance").Text = "40";
                Assert.Equal(mode == 0, Find<ComboBox>(form, "Account").Visible);
                Assert.Equal(mode == 2, Find<TextBox>(form, "Distance").Visible);
                Assert.False(Find<TextBox>(form, "Yaw").Visible);
                if (mode == 1)
                {
                    Find<CheckBox>(form, "Advanced").Checked = true;
                    Assert.True(Find<TextBox>(form, "Yaw").Visible);
                    Find<TextBox>(form, "Yaw").Text = "0.033";
                    Assert.Equal("0.3017", Find<TextBox>(form, "Result").Text);
                }
                form.PerformLayout();
                Application.DoEvents();
                string? directory = Environment.GetEnvironmentVariable("SENSSYNC_UI_CAPTURE");
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(directory, $"source-{mode}-scale-{scale}.png"));
                }
                var body = Find<TableLayoutPanel>(form, "Body");
                Assert.False(body.VerticalScroll.Visible, $"Vertical scroll in mode {mode}, scale {scale}");
                Assert.False(body.HorizontalScroll.Visible);
                if (mode == 0)
                {
                    var detection = Find<Label>(form, "Detection");
                    detection.Text = "CS2 and Valorant running · last focused: Valorant";
                    Find<Label>(form, "Status").Text += " Discovery was incomplete; see Details.";
                    form.PerformLayout();
                    Assert.False(body.VerticalScroll.Visible, "Long detection and warning text must fit.");
                    detection.Text = "No game detected";
                }
                foreach (string name in new[] { "InputsCard", "ResultCard", "Details", "Note" })
                {
                    var control = Find<Control>(form, name);
                    Assert.True(body.PointToClient(control.PointToScreen(new Point(0, control.Height))).Y <= body.ClientSize.Height - body.Padding.Bottom, $"{name} clipped in mode {mode}, scale {scale}");
                }

            }
            using var details = form.CreateDetailsDialog();
            Assert.DoesNotContain("sample.vcfg", Find<TextBox>(details, "Source").Text);
            Assert.Contains("Clipboard value: 0.408214286", Find<TextBox>(details, "Detected values").Text);
            details.Font = form.Font;
            details.Show(form);
            Assert.Equal(4, Find<TabControl>(details, "DetailSections").TabPages.Count);
            string? captureDirectory = Environment.GetEnvironmentVariable("SENSSYNC_UI_CAPTURE");
            if (captureDirectory is not null && scale == 1)
            {
                Find<TabControl>(details, "DetailSections").SelectedIndex = 1;
                using var bitmap = new Bitmap(details.Width, details.Height);
                details.DrawToBitmap(bitmap, new Rectangle(Point.Empty, details.Size));
                bitmap.Save(Path.Combine(captureDirectory, "details.png"));
            }
            details.Close();
            form.Close();
        });
    }

    [Fact]
    public void RecognitionAndSensitivityReadinessAreIndependent()
    {
        RunSta(() =>
        {
            using var files = new TestFiles();
            var candidate = new Cs2ConfigCandidate(files.Write("sample.vcfg", TestFiles.Standard), "sample", null);
            using var form = new MainForm(() => new([candidate], []), _ => { }, false);
            form.Font = new Font("Segoe UI", 10);
            form.Show();
            form.RefreshConfigs();
            var tracker = new GameDetectionTracker();
            form.UpdateDetection(tracker.Observe([new(7, "Marvel-Win64-Shipping.exe")], 7));
            Assert.Contains("Marvel Rivals running", Find<Label>(form, "Detection").Text);
            Assert.Contains("sensitivity integration unsupported", Find<Label>(form, "Detection").Text);
            Assert.Equal(0, Find<ComboBox>(form, "Source").SelectedIndex);
            Find<TextBox>(form, "Dpi").Text = "800";
            Assert.Equal("0.3143", Find<TextBox>(form, "Result").Text);
            Assert.True(Find<Button>(form, "Copy").Enabled);
            form.UpdateDetection(tracker.Observe([new(7, "Marvel-Win64-Shipping.exe")], 0));
            Assert.Contains("Marvel Rivals running", Find<Label>(form, "Detection").Text);
            form.UpdateDetection(tracker.Observe([new(8, "cs2.exe")], 8));
            Assert.Contains("saved settings readable", Find<Label>(form, "Detection").Text);
            File.Delete(candidate.Path);
            form.RefreshConfigs();
            Assert.Contains("saved settings not loaded", Find<Label>(form, "Detection").Text);
            form.UpdateDetection(tracker.Observe([new(9, "VALORANT-Win64-Shipping.exe")], 9));
            Assert.Contains("conversion target only", Find<Label>(form, "Detection").Text);
            using (var details = form.CreateDetailsDialog())
                Assert.Contains("settings reading unsupported", Find<TextBox>(details, "Diagnostics").Text);
            form.UpdateDetection(tracker.Observe([], 0));
            Assert.Equal("No game detected", Find<Label>(form, "Detection").Text);
            form.UpdateDetection(tracker.Observe([], 0, false));
            Assert.Equal("Game detection unavailable", Find<Label>(form, "Detection").Text);
            // Worst-case multi-game header still fits at the default size.
            form.UpdateDetection(tracker.Observe([new(1, "cs2.exe"), new(2, "VALORANT-Win64-Shipping.exe"), new(3, "Marvel-Win64-Shipping.exe")], 3, false));
            form.PerformLayout();
            Assert.Contains("Marvel Rivals", Find<Label>(form, "Detection").Text);
            Assert.Contains("detection incomplete", Find<Label>(form, "Detection").Text);
            Assert.False(Find<TableLayoutPanel>(form, "Body").VerticalScroll.Visible);
            form.Close();
        });
    }

    [Fact]
    public void ThemeTogglePreservesInputsCopyAndThemesDetails()
    {
        RunSta(() =>
        {
            Application.EnableVisualStyles();
            string? copied = null;
            using var form = new MainForm(() => new([], []), value => copied = value, false);
            form.Font = new Font("Segoe UI", 10);
            form.Show();
            Find<ComboBox>(form, "Source").SelectedIndex = 1;
            Find<TextBox>(form, "Dpi").Text = "800";
            Find<TextBox>(form, "Sensitivity").Text = "0.64";
            var lightBackground = form.BackColor;
            var toggle = Find<CheckBox>(form, "DarkMode");
            foreach (bool dark in new[] { true, false, true })
            {
                toggle.Checked = dark;
                Application.DoEvents();
                Assert.Equal("0.2011", Find<TextBox>(form, "Result").Text);
                Assert.Equal("0.64", Find<TextBox>(form, "Sensitivity").Text);
                Find<Button>(form, "Copy").PerformClick();
                Assert.Equal("0.201142857", copied);
                if (!SystemInformation.HighContrast)
                    Assert.Equal(dark, form.BackColor != lightBackground);
                using var details = form.CreateDetailsDialog();
                details.Font = form.Font;
                details.Show(form);
                Assert.Equal(form.BackColor, details.BackColor);
                Assert.Equal(Find<TextBox>(form, "Dpi").ForeColor, Find<TextBox>(details, "Detected values").ForeColor);
                Assert.False(Find<TableLayoutPanel>(form, "Body").VerticalScroll.Visible);
                string? directory = Environment.GetEnvironmentVariable("SENSSYNC_UI_CAPTURE");
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(directory, dark ? "dark.png" : "light.png"));
                    using var detailBitmap = new Bitmap(details.Width, details.Height);
                    details.DrawToBitmap(detailBitmap, new Rectangle(Point.Empty, details.Size));
                    detailBitmap.Save(Path.Combine(directory, dark ? "details-dark.png" : "details-light.png"));
                }
                details.Close();
            }
            Find<TextBox>(form, "Dpi").Clear();
            Assert.False(Find<Button>(form, "Copy").Enabled);
            form.Close();
        });
    }

    private static T Find<T>(Control parent, string name) where T : Control => (T)Assert.Single(parent.Controls.Find(name, true));
    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Window test timed out.");
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
