using SensSync.Core;
using SensSync.Desktop;

namespace SensSync.Tests;

public sealed class WindowTests
{
    private static SourceReading Reading(GameId game = GameId.Cs2, double value = 0.64, string path = "synthetic.vcfg") =>
        new(game, "Synthetic profile", value, value * (game == GameId.MarvelRivals ? 0.0175 : 0.022), path, "synthetic hash", "");
    private static GameDetectionState Running(GameId game) => new(game, game == GameId.Cs2, game == GameId.Valorant, true, game == GameId.MarvelRivals, game == GameId.Battlefield6);

    [Fact]
    public void SavedConfigurationUsesDefaultDpiAndUnreadableInputClearsCopy()
    {
        RunSta(() =>
        {
            string? clipboard = null;
            SourceDiscovery data = new([Reading()]);
            using var form = new MainForm(_ => data, value => clipboard = value, false);
            form.Show();
            Assert.Equal("1600", Find<ComboBox>(form, "Dpi").Text);
            Assert.False(Find<Button>(form, "Copy").Enabled);
            form.UpdateDetection(Running(GameId.Cs2));
            Assert.Equal("0.2011", Find<TextBox>(form, "Result").Text);
            Assert.True(Find<TextBox>(form, "Sensitivity").ReadOnly);
            Find<Button>(form, "Copy").PerformClick();
            Assert.Equal("0.201142857", clipboard);
            Assert.Equal("Copied!", Find<Label>(form, "CopyFeedback").Text);
            form.RefreshReading();
            Assert.Equal("Copied!", Find<Label>(form, "CopyFeedback").Text);
            data = new([], "Unreadable saved settings");
            form.RefreshReading();
            Assert.False(Find<Button>(form, "Copy").Enabled);
            Assert.Equal("—", Find<TextBox>(form, "Result").Text);
            Assert.Empty(Find<Label>(form, "CopyFeedback").Text);
        });
    }

    [Fact]
    public void AmbiguousAccountsDoNotLeakFirstAccountIntoResult()
    {
        RunSta(() =>
        {
            using var form = new MainForm(_ => new([Reading(), Reading(value: 1, path: "second.vcfg")]), _ => { }, false);
            form.Show();
            form.UpdateDetection(Running(GameId.Cs2));
            Assert.False(Find<Button>(form, "Copy").Enabled);
            var profile = Find<ComboBox>(form, "Profile");
            Assert.True(profile.Visible);
            profile.SelectedIndex = 1;
            Assert.Equal("0.3143", Find<TextBox>(form, "Result").Text);
            form.RefreshReading();
            Assert.Equal(1, profile.SelectedIndex);
        });
    }

    [Fact]
    public void DpiPresetsKeepEquivalentGameSettingAtTheSameDpi()
    {
        RunSta(() =>
        {
            using var form = new MainForm(_ => new([Reading()]), _ => { }, false);
            form.UpdateDetection(Running(GameId.Cs2));
            foreach (string value in new[] { "400", "800", "1600", "3200" })
            {
                Find<ComboBox>(form, "Dpi").SelectedItem = value;
                Assert.Equal("0.2011", Find<TextBox>(form, "Result").Text);
            }
            Assert.Empty(form.Controls.Find("Distance", true));
            Assert.Empty(form.Controls.Find("Yaw", true));
            Assert.Empty(form.Controls.Find("Source", true));
        });
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void SourceLayoutsFitWithoutTechnicalControls(float scale)
    {
        RunSta(() =>
        {
            Application.EnableVisualStyles();
            using var form = new MainForm(game => new([Reading(game), Reading(game, 1, "second")]), _ => { }, false);
            form.Font = new Font("Segoe UI", 10);
            form.Show();
            if (scale != 1) form.Scale(new SizeF(scale, scale));
            form.UpdateDetection(new(GameId.MarvelRivals, true, true, true, true, true));
            Find<ComboBox>(form, "Profile").SelectedIndex = 0;
            Application.DoEvents();
            Assert.False(Find<TableLayoutPanel>(form, "Body").VerticalScroll.Visible);
            Assert.False(Find<TableLayoutPanel>(form, "Body").HorizontalScroll.Visible);
            using var details = form.CreateDetailsDialog();
            Assert.Equal(4, Find<TabControl>(details, "DetailSections").TabPages.Count);
        });
    }

    [Fact]
    public void RecognitionAndSensitivityReadinessAreIndependent()
    {
        RunSta(() =>
        {
            using var form = new MainForm(game => game == GameId.Cs2 ? new([Reading()]) : new([], "Unreadable Rivals settings"), _ => { }, false);
            form.UpdateDetection(Running(GameId.Cs2));
            Assert.True(Find<Button>(form, "Copy").Enabled);
            form.UpdateDetection(Running(GameId.MarvelRivals));
            Assert.Equal("Marvel Rivals", Find<Label>(form, "SourceGame").Text);
            Assert.False(Find<Button>(form, "Copy").Enabled);
            Assert.Equal("—", Find<TextBox>(form, "Sensitivity").Text);
            form.UpdateDetection(new(GameId.MarvelRivals, false, false, true));
            Assert.False(Find<Button>(form, "Copy").Enabled);
            Assert.Equal("No game detected", Find<Label>(form, "Detection").Text);
        });
    }

    [Fact]
    public void ForegroundSourceIsRetainedWhenReturningToSensSync()
    {
        RunSta(() =>
        {
            var tracker = new GameDetectionTracker();
            ObservedProcess[] processes = [new(1, "cs2.exe"), new(2, "Marvel-Win64-Shipping.exe")];
            using var form = new MainForm(game => new([Reading(game)]), _ => { }, false);
            form.UpdateDetection(tracker.Observe(processes, 2));
            form.UpdateDetection(tracker.Observe(processes, 99));
            Assert.Equal("Marvel Rivals", Find<Label>(form, "SourceGame").Text);
            form.UpdateDetection(tracker.Observe(processes, 1));
            Assert.Equal("CS2", Find<Label>(form, "SourceGame").Text);
        });
    }

    [Fact]
    public void IdenticalProfilesCanBeReadWithoutAnAccountPrompt()
    {
        RunSta(() =>
        {
            using var form = new MainForm(game => new([Reading(game), Reading(game, path: "second")]), _ => { }, false);
            form.Show();
            form.UpdateDetection(Running(GameId.MarvelRivals));
            Assert.False(Find<ComboBox>(form, "Profile").Visible);
            Assert.True(Find<Button>(form, "Copy").Enabled);
        });
    }

    [Fact]
    public void ThemeTogglePreservesInputsCopyAndThemesDetails()
    {
        RunSta(() =>
        {
            Application.EnableVisualStyles();
            string? copied = null;
            using var form = new MainForm(game => new([Reading(game, 0.75)]), value => copied = value, false);
            form.Font = new Font("Segoe UI", 10);
            form.Show();
            form.UpdateDetection(Running(GameId.MarvelRivals));
            var light = form.BackColor;
            foreach (bool dark in new[] { true, false, true })
            {
                Find<CheckBox>(form, "DarkMode").Checked = dark;
                Application.DoEvents();
                Assert.Equal("0.1875", Find<TextBox>(form, "Result").Text);
                Find<Button>(form, "Copy").PerformClick();
                Assert.Equal("0.1875", copied);
                if (!SystemInformation.HighContrast) Assert.Equal(dark, form.BackColor != light);
                using var details = form.CreateDetailsDialog();
                Assert.Equal(form.BackColor, details.BackColor);
                string? directory = Environment.GetEnvironmentVariable("SENSSYNC_UI_CAPTURE");
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                    using var bitmap = new Bitmap(form.Width, form.Height);
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(Path.Combine(directory, dark ? "simple-dark.png" : "simple-light.png"));
                }
            }
        });
    }

    [Theory]
    [InlineData(0, "0.2011", "0.201142857")]
    [InlineData(1, "0.6400", "0.64")]
    [InlineData(2, "0.8046", "0.804571429")]
    [InlineData(3, "6.7762", "6.776170102")]
    public void TargetSelectionUpdatesResultCopyDetailsAndPreservesSource(int index, string display, string clipboard)
    {
        RunSta(() =>
        {
            string? copied = null;
            using var form = new MainForm(_ => new([Reading()]), value => copied = value, false);
            form.Show();
            form.UpdateDetection(Running(GameId.Cs2));
            Find<ComboBox>(form, "Target").SelectedIndex = index;
            Assert.Equal(display, Find<TextBox>(form, "Result").Text);
            Find<Button>(form, "Copy").PerformClick();
            Assert.Equal(clipboard, copied);
            Assert.Equal("CS2", Find<Label>(form, "SourceGame").Text);
            using var details = form.CreateDetailsDialog();
            Assert.Contains(ConversionTarget.All[index].Name, Find<TextBox>(details, "Detected values").Text);
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
