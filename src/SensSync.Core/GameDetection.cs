namespace SensSync.Core;

public enum GameId { Cs2, Valorant, MarvelRivals }
public sealed record ObservedProcess(int Id, string ExecutableName);
public sealed record GameDetectionState(GameId? LastRelevantGame, bool Cs2Running, bool ValorantRunning, bool DetectionAvailable, bool MarvelRivalsRunning = false)
{
    public IEnumerable<GameId> RunningGames
    {
        get
        {
            if (Cs2Running) yield return GameId.Cs2;
            if (ValorantRunning) yield return GameId.Valorant;
            if (MarvelRivalsRunning) yield return GameId.MarvelRivals;
        }
    }
}

/// <summary>Pure state logic: moving focus to SensSync or another app does not forget the last game.</summary>
public sealed class GameDetectionTracker
{
    public GameId? LastRelevantGame { get; private set; }

    // Exact game executables only; launcher names are deliberately excluded.
    public static IReadOnlyList<string> ExecutableNames { get; } = Array.AsReadOnly(new[]
        { "cs2.exe", "VALORANT-Win64-Shipping.exe", "Marvel-Win64-Shipping.exe" });

    public static GameId? Recognize(string executableName) => executableName.ToLowerInvariant() switch
    {
        "cs2.exe" => GameId.Cs2,
        "valorant-win64-shipping.exe" => GameId.Valorant,
        "marvel-win64-shipping.exe" => GameId.MarvelRivals,
        _ => null
    };

    public GameDetectionState Observe(IReadOnlyList<ObservedProcess> processes, int foregroundPid, bool available = true)
    {
        var foreground = processes.FirstOrDefault(p => p.Id == foregroundPid);
        var foregroundGame = foreground is null ? null : Recognize(foreground.ExecutableName);
        if (foregroundGame is not null) LastRelevantGame = foregroundGame;
        return new(LastRelevantGame,
            processes.Any(p => Recognize(p.ExecutableName) == GameId.Cs2),
            processes.Any(p => Recognize(p.ExecutableName) == GameId.Valorant), available,
            processes.Any(p => Recognize(p.ExecutableName) == GameId.MarvelRivals));
    }
}
