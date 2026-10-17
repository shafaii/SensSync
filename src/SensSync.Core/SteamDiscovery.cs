namespace SensSync.Core;

public sealed record Cs2ConfigCandidate(string Path, string AccountId, string? SteamRoot)
{
    public string DisplayName => $"Account {AccountId} · {System.IO.Path.GetFileNameWithoutExtension(Path).Replace("cs2_user_convars_", "", StringComparison.Ordinal)}";
    public override string ToString() => DisplayName;
}
public sealed record DiscoveryResult(IReadOnlyList<Cs2ConfigCandidate> Candidates, IReadOnlyList<string> Warnings);

/// <summary>Only inspects known Steam/CS2 locations, never searches an entire disk.</summary>
public static class SteamDiscovery
{
    public static DiscoveryResult Discover(IEnumerable<string> steamRoots)
    {
        var candidates = new List<Cs2ConfigCandidate>();
        var warnings = new List<string>();
        foreach (string root in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                string users = Path.Combine(root, "userdata");
                if (!Directory.Exists(users)) continue;
                foreach (string account in Directory.EnumerateDirectories(users).Order(StringComparer.OrdinalIgnoreCase))
                {
                    if (!Path.GetFileName(account).All(char.IsAsciiDigit)) continue;
                    string configs = Path.Combine(account, "730", "local", "cfg");
                    if (!Directory.Exists(configs)) continue;
                    foreach (string path in Directory.EnumerateFiles(configs, "cs2_user_convars*_slot*.vcfg").Order(StringComparer.OrdinalIgnoreCase))
                        candidates.Add(new(Path.GetFullPath(path), Path.GetFileName(account), Path.GetFullPath(root)));
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            { warnings.Add($"Some Steam configurations could not be inspected: {e.Message}"); }
        }
        return new(candidates.DistinctBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToArray(), warnings);
    }

    public static Cs2ConfigCandidate? ChooseUnambiguous(IReadOnlyList<Cs2ConfigCandidate> candidates, string? selectedPath = null)
    {
        if (selectedPath is not null)
        {
            var selected = candidates.FirstOrDefault(c => c.Path.Equals(selectedPath, StringComparison.OrdinalIgnoreCase));
            if (selected is not null) return selected;
        }
        return candidates.Count == 1 ? candidates[0] : null;
    }

    public static IReadOnlyList<string> FindCs2Installations(string steamRoot)
    {
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamRoot };
        string folders = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(folders))
        {
            var entries = KeyValues.Parse(Cs2Configuration.Decode(Cs2Configuration.ReadBoundedBytes(folders)));
            var libraryNode = KeyValues.Single(entries, "libraryfolders")?.Children;
            if (libraryNode is not null)
                foreach (var entry in libraryNode)
                {
                    string? path = entry.Children is null ? entry.Value : KeyValues.Single(entry.Children, "path")?.Value;
                    if (path is not null && Path.IsPathFullyQualified(path)) libraries.Add(path);
                }
        }
        var installs = new List<string>();
        foreach (string library in libraries)
        {
            string manifest = Path.Combine(library, "steamapps", "appmanifest_730.acf");
            if (!File.Exists(manifest)) continue;
            var nodes = KeyValues.Parse(Cs2Configuration.Decode(Cs2Configuration.ReadBoundedBytes(manifest)));
            var state = KeyValues.Single(nodes, "AppState")?.Children;
            string? dir = state is null ? null : KeyValues.Single(state, "installdir")?.Value;
            if (dir is null || dir.IndexOfAny(['/', '\\', ':']) >= 0 || dir is "." or "..") continue;
            installs.Add(Path.Combine(library, "steamapps", "common", dir));
        }
        return installs;
    }
}
