using SensSync.Core;

namespace SensSync.Desktop;

internal static class GameDetectionPresentation
{
    internal static string Name(GameId game) => game switch
    {
        GameId.Cs2 => "CS2",
        GameId.Valorant => "Valorant",
        GameId.MarvelRivals => "Marvel Rivals",
        GameId.Battlefield6 => "Battlefield 6",
        _ => throw new ArgumentOutOfRangeException(nameof(game))
    };

    internal static string Integration(GameId game, bool cs2Readable) => game switch
    {
        GameId.Cs2 => cs2Readable ? "saved settings readable" : "saved settings not loaded",
        GameId.Valorant => "saved-settings reader available",
        GameId.MarvelRivals => "saved-settings reader available",
        GameId.Battlefield6 => "experimental saved-settings reader available",
        _ => throw new ArgumentOutOfRangeException(nameof(game))
    };

    internal static string Details(GameDetectionState state, bool cs2Readable) =>
        (state.DetectionAvailable ? "Process recognition available." : "Process recognition incomplete or unavailable.") + "\r\n" +
        (state.RunningGames.Any() ? string.Join("\r\n", state.RunningGames.Select(game => $"{Name(game)} running — {Integration(game, cs2Readable)}.")) : "No recognized game observed.") +
        (state.LastRelevantGame is { } last ? $"\r\nLast focused recognized game: {Name(last)} (history, not proof it is still running)." : "") +
        "\r\nThe last focused running game selects the source. Values come from saved settings, not protected game memory.";
}
