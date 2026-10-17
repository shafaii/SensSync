using System.Security.Cryptography;
using System.Text.Json;
using SensSync.Core;

namespace SensSync.Tests;

public sealed class GameSettingsReaderTests
{
    private static string Rivals(double sensitivity, double? hero = null)
    {
        var controls = new Dictionary<string, string> { ["0"] = JsonSerializer.Serialize(new { MouseHorizontalSensitivity = sensitivity }) };
        if (hero is not null) controls["1035"] = JsonSerializer.Serialize(new { MouseHorizontalSensitivity = hero.Value });
        return JsonSerializer.Serialize(new { RemoteUserSetting = new { UserControl = JsonSerializer.Serialize(controls) } });
    }

    [Fact]
    public void RivalsReadsNestedJsonAndKeepsHeroOverridesDistinctWithoutWriting()
    {
        using var files = new TestFiles();
        string path = files.Write("settings.json", Rivals(0.75, 1.2));
        byte[] before = File.ReadAllBytes(path);
        var values = GameSettingsReader.Read(GameId.MarvelRivals, path, "synthetic");
        Assert.Equal(2, values.Count);
        Assert.Equal(0.75, values[0].Sensitivity);
        Assert.Equal(0.013125, values[0].DegreesPerCount, 10);
        Assert.Equal(1.2, values[1].Sensitivity);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(before)), values[0].Sha256);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void ValorantUsesOnlyExactHipfireKeyInSettingsSection()
    {
        using var files = new TestFiles();
        string path = files.Write("riot.ini", "[Other]\nEAresFloatSettingName::MouseSensitivity=9\n[Settings]\nEAresFloatSettingName::MouseSensitivityZoomed=3\nEAresFloatSettingName::MouseSensitivity=0.2");
        Assert.Equal(0.2, Assert.Single(GameSettingsReader.Read(GameId.Valorant, path, "test")).Sensitivity);
    }

    [Theory]
    [InlineData(0.006, 8)]
    [InlineData(0, 0)]
    [InlineData(0.075, 100)]
    public void BattlefieldConvertsConfigValueToMenuSetting(double config, double menu)
    {
        using var files = new TestFiles();
        string path = files.Write("profile", "GstInput.MouseSensitivity " + config.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var read = Assert.Single(GameSettingsReader.Read(GameId.Battlefield6, path, "test"));
        Assert.Equal(menu, read.Sensitivity, 10);
        Assert.True(read.DegreesPerCount > 0);
    }

    [Theory]
    [InlineData("[Settings]\nEAresFloatSettingName::MouseSensitivityZoomed=1")]
    [InlineData("[Settings]\nEAresFloatSettingName::MouseSensitivity=NaN")]
    [InlineData("[Settings]\nEAresFloatSettingName::MouseSensitivity=1\nEAresFloatSettingName::MouseSensitivity=2")]
    public void MissingInvalidOrDuplicateValorantSettingsFail(string text)
    {
        using var files = new TestFiles();
        string path = files.Write("riot.ini", text);
        Assert.Throws<FormatException>(() => GameSettingsReader.Read(GameId.Valorant, path, "test"));
    }

    [Fact]
    public void MissingRivalsGlobalSensitivityDoesNotInventDefault()
    {
        using var files = new TestFiles();
        string path = files.Write("settings.json", "{\"RemoteUserSetting\":{\"UserControl\":\"{}\"}}");
        Assert.Throws<FormatException>(() => GameSettingsReader.Read(GameId.MarvelRivals, path, "test"));
    }
}
