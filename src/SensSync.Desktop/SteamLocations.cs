using Microsoft.Win32;
using SensSync.Core;

namespace SensSync.Desktop;

internal static class SteamLocations
{
    public static DiscoveryResult Discover()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        foreach (var (hive, view, value) in new[]
        {
            (RegistryHive.CurrentUser, RegistryView.Default, "SteamPath"),
            (RegistryHive.LocalMachine, RegistryView.Registry32, "InstallPath"),
            (RegistryHive.LocalMachine, RegistryView.Registry64, "InstallPath")
        })
        {
            try
            {
                using var registry = RegistryKey.OpenBaseKey(hive, view);
                using var key = registry.OpenSubKey(@"Software\Valve\Steam", false);
                if (key?.GetValue(value) is string path && Path.IsPathFullyQualified(path)) roots.Add(path);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            { warnings.Add("A Steam registry location could not be read. You can choose a configuration manually."); }
        }
        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        var result = SteamDiscovery.Discover(roots);
        return result with { Warnings = result.Warnings.Concat(warnings).ToArray() };
    }

    public static string DescribeOverrides(Cs2ConfigCandidate candidate)
    {
        const string limitation = "Saved file values are not guaranteed live settings. Launch options, console commands, or other configs may override them.";
        if (candidate.SteamRoot is null) return limitation + " The Steam installation for this file is unknown.";
        try
        {
            var installs = SteamDiscovery.FindCs2Installations(candidate.SteamRoot);
            if (installs.Any(path => File.Exists(Path.Combine(path, "game", "csgo", "cfg", "autoexec.cfg"))))
                return "An autoexec.cfg exists and may override these saved settings. " + limitation;
            return limitation + (installs.Count == 0 ? " The CS2 installation could not be located." : " No autoexec.cfg was found in the located installation.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or ArgumentException)
        { return limitation + " Startup configuration discovery was incomplete."; }
    }
}
