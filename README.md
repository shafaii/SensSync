# SensSync

A compact, read-only Windows desktop utility for carrying horizontal hipfire sensitivity from CS2 to Valorant.

## Features

- Read saved CS2 sensitivity and yaw, or enter CS2 values manually.
- Convert a physical cm/360 target using your mouse DPI.
- Clear result display, full precision in Details, and a Copy sensitivity button.
- In-app **Dark mode** toggle. The theme applies immediately to the main window and Details and resets to light when the app closes. Windows high contrast takes precedence; some native window chrome follows the OS theme.
- Recognize CS2, Valorant and Marvel Rivals independently of conversion support. Recognition persists when focus moves away from a running game.

**Marvel Rivals recognition does not provide sensitivity integration.** Valorant is a conversion target; its settings are not read. CS2 settings are saved-file snapshots, not live in-game readings.

## Use

Choose a source and enter your actual mouse DPI. Select your CS2 account if prompted, or choose an existing VCFG file. For manual CS2 input, expand **Advanced: custom yaw** only when needed. Copy the calculated value and enter it in Valorant yourself.

The main result normally shows four decimal places; Copy retains up to nine decimal places. Details contains full calculation precision, saved-file provenance, diagnostics and limitations.

No game files are modified. There is no auto-apply, protected-memory access, injection, tray, telemetry, network client, persistent profile or DPI manipulation. Equivalent cm/360 does not guarantee identical FOV, scoped aim, vertical response or perceived aiming. Valorant yaw 0.07 remains a candidate pending physical in-game validation.

## Build and test

Requires Windows and the .NET 10 SDK (see `global.json`).

```powershell
dotnet restore SensSync.slnx
dotnet build SensSync.slnx -c Release --no-restore
dotnet test SensSync.slnx -c Release --no-build
dotnet run --project src/SensSync.Desktop
```

To produce a self-contained Windows x64 folder and ZIP:

```powershell
./scripts/publish.ps1
```

The ZIP includes the .NET runtime. Extract the whole archive and run `SensSync.exe`; do not move the executable away from its accompanying files.

## Verification and limitations

Release build and **63 tests** passed locally on Windows. Tests use synthetic configurations and cover conversion, parsing, discovery, process recognition, foreground transitions, layout, theme switching and clipboard precision. Light/dark main and Details windows were rendered for visual inspection.

Marvel Rivals was recognized on the development machine using its observed `Marvel-Win64-Shipping.exe` process and matching window title. The launcher alone is not considered a running game. Detection is based on exact process names, not executable authentication. Other storefront executable names are not qualified.

Manual game-to-SensSync Alt+Tab trials, actual multi-monitor DPI changes, screen readers, high contrast and clean-machine packaging acceptance remain outstanding. Programmatic layout checks are not a substitute for real display-scaling tests.

See [ARCHITECTURE.md](ARCHITECTURE.md) for implementation boundaries and precision details.
