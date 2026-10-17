using System.Text.Json;
using SensSync.Core;

namespace SensSync.Desktop;

internal static class GameSettingsLocations
{
    public static SourceDiscovery Read(GameId game)
    {
        var readings = new List<SourceReading>();
        var errors = new List<string>();
        try
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            IEnumerable<(string Path, string Label)> files = game switch
            {
                GameId.Cs2 => Cs2Files(),
                GameId.MarvelRivals => AccountFiles(Path.Combine(local, "Marvel", "Saved", "Saved", "Config"), "MarvelUserSetting.json"),
                GameId.Valorant => AccountFiles(Path.Combine(local, "VALORANT", "Saved", "Config"), Path.Combine("Windows", "RiotUserSettings.ini")),
                GameId.Battlefield6 => new[] { Path.Combine(documents, "Battlefield 6", "settings", "PROFSAVE_profile"), Path.Combine(documents, "Battlefield 6", "settings", "steam", "PROFSAVE_profile") }
                    .Where(File.Exists).Select(p => (p, "BF6 " + new DirectoryInfo(Path.GetDirectoryName(p)!).Name)),
                _ => []
            };
            foreach (var file in files)
            {
                try { readings.AddRange(GameSettingsReader.Read(game, file.Path, file.Label)); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or ArgumentException or JsonException or InvalidOperationException)
                { errors.Add("A saved profile could not be read."); }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        { errors.Add("Settings discovery was unavailable."); }
        // Do not silently use a different account when one of the possible accounts is unreadable.
        if (errors.Count > 0) return new([], "Could not safely read all saved profiles. See Details.");
        return new(readings, readings.Count == 0 ? "No readable saved sensitivity found. Save your settings in game, then refresh." : null);
    }

    private static IEnumerable<(string Path, string Label)> Cs2Files()
    {
        var found = SteamLocations.Discover();
        if (found.Warnings.Count > 0) throw new IOException("Steam settings discovery was incomplete.");
        return found.Candidates.Select(c => (c.Path, c.ToString()));
    }

    private static IEnumerable<(string Path, string Label)> AccountFiles(string root, string relativeFile)
    {
        if (!Directory.Exists(root)) yield break;
        foreach (string directory in Directory.EnumerateDirectories(root))
        {
            string file = Path.Combine(directory, relativeFile);
            if (File.Exists(file)) yield return (file, "Account " + Path.GetFileName(directory));
        }
    }
}
