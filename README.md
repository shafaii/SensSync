# SensSync

A simple, read-only Windows sensitivity converter.

1. Open your source game. SensSync detects it and reads its saved sensitivity.
2. Choose your mouse DPI from the dropdown (default **1600**).
3. Choose the target game, copy the result, and enter it in that game.

The main screen contains no manual sensitivity entry, yaw controls or physical-distance fields. A dark-mode toggle is available. Theme selection lasts for the session.

## Supported games

CS2, Valorant, Marvel Rivals and Battlefield 6 have saved-settings readers and conversion targets. **BF6 remains experimental.** Recognition follows the last focused running game and survives returning to SensSync. If multiple games are running without a known foreground source, select one. If saved accounts or hero profiles have different sensitivity values, select the relevant profile; the app never silently guesses which account or hero is active. Identical profiles require no selection.

Settings refresh while the game is running. These are **saved settings**, not protected-memory readings: changes that have not been saved by the game can differ. Missing, malformed or ambiguous settings do not produce a copyable result. Details contains file provenance and diagnostics.

No game files are modified. There is no auto-apply, memory inspection, injection, telemetry or network client. Conversion matches horizontal hipfire turn rate; scoped aim and perceived aiming can differ. See [model assumptions and sources](CONVERSION_MODELS.md).

## Build and run

Requires Windows and the .NET 10 SDK (see `global.json`).

```powershell
dotnet restore SensSync.slnx
dotnet build SensSync.slnx -c Release --no-restore
dotnet test SensSync.slnx -c Release --no-build
dotnet run --project src/SensSync.Desktop
```

To build a self-contained Windows x64 folder and ZIP, run `./scripts/publish.ps1`. Extract the whole ZIP and launch `SensSync.exe`.

## Verification

Release build and **86 tests** passed locally on Windows. Tests cover bounded parsers, duplicate/missing values, account and hero ambiguity, conversion, recognition, foreground changes, DPI presets, theme switching, clipboard precision and layout. Existing core parser and math tests remain; UI tests reflect the simplified workflow.

Production readers were exercised against local CS2, Valorant, Rivals and BF6 saved files. File hashes remained unchanged. Light/dark renders were inspected. Current-game unsaved-setting changes, other storefront layouts, actual multi-monitor DPI transitions, screen readers and high contrast require further acceptance testing. BF6 model accuracy still needs physical in-game validation.
