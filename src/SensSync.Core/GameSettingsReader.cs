using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace SensSync.Core;

public sealed record SourceReading(GameId Game, string Label, double Sensitivity, double DegreesPerCount, string Path, string Sha256, string Notes);
public sealed record SourceDiscovery(IReadOnlyList<SourceReading> Readings, string? Error = null);

/// <summary>Only reads bounded settings files. Never reads game memory or writes settings.</summary>
public static class GameSettingsReader
{
    public static IReadOnlyList<SourceReading> Read(GameId game, string path, string label)
    {
        byte[] bytes = Cs2Configuration.ReadBoundedBytes(path);
        string text = Cs2Configuration.Decode(bytes);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        SourceReading Make(double sensitivity, double rate, string suffix = "", string notes = "") =>
            new(game, label + suffix, sensitivity, rate, path, hash, notes);
        switch (game)
        {
            case GameId.Cs2:
                var cs = Cs2Configuration.Parse(text);
                return [Make(cs.Sensitivity, cs.Sensitivity * cs.Yaw, notes: $"Saved yaw: {cs.Yaw:G}. Startup or console overrides may differ.")];
            case GameId.Valorant:
                double val = ReadSetting(text, "EAresFloatSettingName::MouseSensitivity", '=', "Settings");
                LinearSensitivityModel.RequirePositive(val, "Saved sensitivity");
                return [Make(val, val * LinearSensitivityModel.ValorantYawCandidate)];
            case GameId.Battlefield6:
                double config = ReadSetting(text, "GstInput.MouseSensitivity", ' ');
                double menu = config / 0.00075;
                if (!double.IsFinite(menu) || menu < 0 || menu > 100) throw new FormatException("BF6 sensitivity is outside its menu range.");
                return [Make(menu, menu * ConversionTarget.Battlefield6.Slope + ConversionTarget.Battlefield6.Offset,
                    notes: "Experimental BF6 saved-file/menu mapping and turn-rate model. Validate in game.")];
            case GameId.MarvelRivals:
                using (var root = JsonDocument.Parse(text))
                using (var controls = DecodeJson(Required(Required(root.RootElement, "RemoteUserSetting"), "UserControl")))
                using (var global = DecodeJson(Required(controls.RootElement, "0")))
                {
                    double common = Required(global.RootElement, "MouseHorizontalSensitivity").GetDouble();
                    LinearSensitivityModel.RequirePositive(common, "Saved horizontal sensitivity");
                    var readings = new List<SourceReading> { Make(common, common * 0.0175, " · all heroes") };
                    foreach (var hero in controls.RootElement.EnumerateObject().Where(p => p.Name != "0"))
                    {
                        using var profile = DecodeJson(hero.Value);
                        if (profile.RootElement.TryGetProperty("MouseHorizontalSensitivity", out _))
                        {
                            double value = Required(profile.RootElement, "MouseHorizontalSensitivity").GetDouble();
                            LinearSensitivityModel.RequirePositive(value, "Saved hero sensitivity");
                            if (value != common) readings.Add(Make(value, value * 0.0175, $" · hero {hero.Name}", "Hero-specific sensitivity. Select the hero profile you use."));
                        }
                    }
                    return readings;
                }
            default: throw new ArgumentOutOfRangeException(nameof(game));
        }
    }

    private static JsonDocument DecodeJson(JsonElement value) => JsonDocument.Parse(value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
    private static JsonElement Required(JsonElement element, string key)
    {
        var matches = element.EnumerateObject().Where(p => p.Name == key).ToArray();
        if (matches.Length != 1) throw new FormatException($"Missing or duplicate setting: {key}.");
        return matches[0].Value;
    }

    private static double ReadSetting(string text, string key, char separator, string? section = null)
    {
        string currentSection = "";
        var values = new List<double>();
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']')) { currentSection = trimmed[1..^1]; continue; }
            if (section is not null && currentSection != section) continue;
            int split = trimmed.IndexOf(separator);
            if (split < 0 || trimmed[..split].Trim() != key) continue;
            if (!double.TryParse(trimmed[(split + 1)..].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                throw new FormatException("Saved sensitivity is not a finite number.");
            values.Add(value);
        }
        if (values.Count != 1) throw new FormatException("Saved sensitivity is missing or duplicated.");
        return values[0];
    }
}
