using SensSync.Core;

namespace SensSync.Tests;

public sealed class DiscoveryAndDetectionTests
{
    [Fact]
    public void MultipleAccountsRequireSelectionAndExistingSelectionIsRetained()
    {
        using var files = new TestFiles();
        files.Write("userdata/100/730/local/cfg/cs2_user_convars_0_slot0.vcfg", TestFiles.Standard);
        string second = files.Write("userdata/200/730/local/cfg/cs2_user_convars_0_slot0.vcfg", TestFiles.Standard);
        files.Write("userdata/not-an-account/730/local/cfg/cs2_user_convars_0_slot0.vcfg", TestFiles.Standard);
        var result = SteamDiscovery.Discover([files.Root, files.Root]);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Null(SteamDiscovery.ChooseUnambiguous(result.Candidates));
        Assert.Equal(second, SteamDiscovery.ChooseUnambiguous(result.Candidates, second)!.Path);
        Assert.Null(SteamDiscovery.ChooseUnambiguous(result.Candidates, "removed.vcfg"));
    }

    [Fact]
    public void EmptyAndSingleAccountScenarios()
    {
        using var files = new TestFiles();
        Assert.Empty(SteamDiscovery.Discover([files.Root]).Candidates);
        string path = files.Write("userdata/100/730/local/cfg/cs2_user_convars_0_slot0.vcfg", TestFiles.Standard);
        var candidates = SteamDiscovery.Discover([files.Root]).Candidates;
        Assert.Equal(path, SteamDiscovery.ChooseUnambiguous(candidates)!.Path);
    }

    [Fact]
    public void FindsInstallationInSecondarySteamLibrary()
    {
        using var files = new TestFiles();
        string library = Path.Combine(files.Root, "secondary");
        string escaped = library.Replace("\\", "\\\\");
        files.Write("steamapps/libraryfolders.vdf", $"\"libraryfolders\" {{ \"1\" {{ \"path\" \"{escaped}\" }} }}");
        files.Write("secondary/steamapps/appmanifest_730.acf", "\"AppState\" { \"appid\" \"730\" \"installdir\" \"Counter-Strike Global Offensive\" }");
        Assert.Equal(Path.Combine(library, "steamapps", "common", "Counter-Strike Global Offensive"), Assert.Single(SteamDiscovery.FindCs2Installations(files.Root)));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..")]
    [InlineData("C:\\outside")]
    public void DoesNotFollowInstallDirectoryTraversal(string directory)
    {
        using var files = new TestFiles();
        files.Write("steamapps/appmanifest_730.acf", $"\"AppState\" {{ \"installdir\" \"{directory.Replace("\\", "\\\\")}\" }}");
        Assert.Empty(SteamDiscovery.FindCs2Installations(files.Root));
    }

    [Fact]
    public void OwnWindowFocusDoesNotForgetTheLastRelevantGame()
    {
        var tracker = new GameDetectionTracker();
        ObservedProcess[] running = [new(10, "CS2.exe"), new(20, "SensSync.exe"), new(30, "VALORANT-Win64-Shipping.exe")];
        Assert.Equal(GameId.Cs2, tracker.Observe(running, 10).LastRelevantGame);
        var state = tracker.Observe(running, 20);
        Assert.Equal(GameId.Cs2, state.LastRelevantGame);
        Assert.True(state.Cs2Running);
        Assert.Equal(GameId.Valorant, tracker.Observe(running, 30).LastRelevantGame);
        state = tracker.Observe([], 20);
        Assert.Equal(GameId.Valorant, state.LastRelevantGame);
        Assert.False(state.Cs2Running);
        Assert.False(state.ValorantRunning);
    }

    [Fact]
    public void DoesNotRecognizeLaunchersOrSubstringMatches()
    {
        Assert.Null(GameDetectionTracker.Recognize("cs2-helper.exe"));
        Assert.Null(GameDetectionTracker.Recognize("VALORANT.exe"));
        Assert.Null(GameDetectionTracker.Recognize("RiotClientServices.exe"));
        Assert.False(new GameDetectionTracker().Observe([], 0, false).DetectionAvailable);
    }
    [Theory]
    [InlineData("cs2.exe", GameId.Cs2)]
    [InlineData("VALORANT-Win64-Shipping.exe", GameId.Valorant)]
    [InlineData("Marvel-Win64-Shipping.exe", GameId.MarvelRivals)]
    [InlineData("MARVEL-WIN64-SHIPPING.EXE", GameId.MarvelRivals)]
    public void RecognizesExactGameExecutablesAndRetainsAcrossForegroundTransitions(string executable, GameId expected)
    {
        var tracker = new GameDetectionTracker();
        ObservedProcess[] running = [new(10, executable), new(20, "SensSync.exe"), new(30, "explorer.exe")];
        // Starting SensSync after a game is already running must work before the game gains focus.
        Assert.Contains(expected, tracker.Observe(running, 20).RunningGames);
        Assert.Equal(expected, tracker.Observe(running, 10).LastRelevantGame);
        foreach (int foreground in new[] { 20, 30, 0, 999 })
        {
            var state = tracker.Observe(running, foreground);
            Assert.Equal(expected, state.LastRelevantGame);
            Assert.Contains(expected, state.RunningGames);
        }
        var exited = tracker.Observe([], 20);
        Assert.Empty(exited.RunningGames);
        Assert.Equal(expected, exited.LastRelevantGame);
    }

    [Theory]
    [InlineData("Marvel.exe")]
    [InlineData("Marvel-Win64-Shipping-helper.exe")]
    [InlineData("MarvelRivals.exe")]
    [InlineData("CrashReportClient.exe")]
    public void DoesNotConfuseLaunchersAndHelpersWithMarvelRivals(string executable)
        => Assert.Null(GameDetectionTracker.Recognize(executable));

    [Fact]
    public void TracksAllThreeGamesIndependentlyAndPreservesPositivePartialObservations()
    {
        var tracker = new GameDetectionTracker();
        ObservedProcess[] running = [new(1, "cs2.exe"), new(2, "VALORANT-Win64-Shipping.exe"), new(3, "Marvel-Win64-Shipping.exe")];
        foreach (int foreground in new[] { 1, 2, 3 })
        {
            var state = tracker.Observe(running, foreground);
            Assert.Equal(3, state.RunningGames.Count());
            Assert.Equal(GameDetectionTracker.Recognize(running[foreground - 1].ExecutableName), state.LastRelevantGame);
        }
        var partial = tracker.Observe([running[2]], 0, false);
        Assert.False(partial.DetectionAvailable);
        Assert.True(partial.MarvelRivalsRunning);
        Assert.Equal(GameId.MarvelRivals, partial.LastRelevantGame);
        Assert.All(GameDetectionTracker.ExecutableNames, executable => Assert.NotNull(GameDetectionTracker.Recognize(executable)));
    }

}
