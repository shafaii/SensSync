using SensSync.Core;

namespace SensSync.Desktop;

internal static class GameDetectionPresentation
{
    internal static string Name(GameId game) => game switch
    {
        GameId.Cs2 => "CS2",
        GameId.Valorant => "Valorant",
        GameId.MarvelRivals => "Marvel Rivals",
        _ => throw new ArgumentOutOfRangeException(nameof(game))
    };

    internal static string Integration(GameId game, bool cs2Readable) => game switch
    {
        GameId.Cs2 => cs2Readable ? "saved settings readable" : "saved settings not loaded",
        GameId.Valorant => "conversion target only; settings reading unsupported",
        GameId.MarvelRivals => "sensitivity integration unsupported",
        _ => throw new ArgumentOutOfRangeException(nameof(game))
    };

    internal static string Summary(GameDetectionState state, bool cs2Readable)
    {
        var running = state.RunningGames.ToArray();
        if (running.Length == 0)
            return state.DetectionAvailable ? "No game detected" : "Game detection unavailable";
        var relevant = state.LastRelevantGame is { } last && running.Contains(last) ? last : running[^1];
        string names = string.Join(", ", running.Select(Name));
        string integration = relevant == GameId.Valorant ? "conversion target only" : Integration(relevant, cs2Readable);
        return $"{names} running\n{(running.Length > 1 ? Name(relevant) + ": " : "")}{integration}" +
            (state.DetectionAvailable ? "" : " · detection incomplete");
    }

    internal static string Details(GameDetectionState state, bool cs2Readable) =>
        (state.DetectionAvailable ? "Process recognition available." : "Process recognition incomplete or unavailable.") + "\r\n" +
        (state.RunningGames.Any() ? string.Join("\r\n", state.RunningGames.Select(game => $"{Name(game)} running — {Integration(game, cs2Readable)}.")) : "No recognized game observed.") +
        (state.LastRelevantGame is { } last ? $"\r\nLast focused recognized game: {Name(last)} (history, not proof it is still running)." : "") +
        "\r\nRecognition does not select a conversion source. Readable settings are saved CS2 values, not a live in-game reading.";
}
