# Architecture

- **SensSync.Core:** bounded read-only settings parsers, conversion models, exact process-name recognition, and pure focus/running-state tracking.
- **SensSync.Desktop:** Windows process monitoring, discovery in known settings locations, automatic source selection, DPI presets, target selection, clipboard, and session-only themes.
- **SensSync.Tests:** synthetic fixtures, isolated temporary files, mathematical checks and STA UI tests.

## Flow

Detect running game → select last focused running source → read saved sensitivity → choose target → calculate → copy. DPI defaults to 1600. The same selected DPI is used for source and target. Physical math is internal; there is no distance mode or manual source sensitivity control.

Source changes immediately clear the previous reading/result. The foreground history survives focus changes to non-game windows, but a stopped game is not treated as still running. Multiple games without a known source require a choice. Saved profiles with differing sensitivity/turn rate require an explicit account or hero choice. Equivalent readings from multiple profiles can be used without guessing an account identity.

The existing two-second monitor triggers bounded file refreshes, throttled to at most once per two seconds apart from explicit Refresh/source changes. Unchanged readings preserve copy feedback. Settings values are file snapshots, not live-memory observations.

## Readers

- CS2: Steam userdata VCFG; existing bounded Valve parser and actual saved m_yaw.
- Valorant: each account's Windows/RiotUserSettings.ini, exact MouseSensitivity key in the Settings section. Zoom sensitivity is excluded.
- Marvel Rivals: per-account MarvelUserSetting.json in Marvel/Saved/Saved/Config. Decode RemoteUserSetting.UserControl and its nested global/hero JSON strings. Read MouseHorizontalSensitivity. Differing hero overrides require selection.
- BF6: Documents/Battlefield 6/settings/PROFSAVE_profile or its steam subfolder. Convert GstInput.MouseSensitivity to the menu scale using the observed/documented 0.00075 factor. Recognition uses bf6.exe, confirmed in the local installation. Turn-rate conversion remains experimental.

All files use FileAccess.Read with sharing, a 2 MiB size bound, and strict decoding. Readers reject required missing, duplicate, non-finite and invalid values. No real account identifiers or complete user settings are committed. No write adapters or memory readers exist.

## Conversion and themes

See CONVERSION_MODELS.md for formulas, provenance and qualification limits. Four decimal places are normally shown, full calculation precision is in Details, and Copy rounds to at most nine fractional digits. Impossible BF6 menu values fail instead of being clamped.

UiTheme applies palettes without recreating inputs. Details receives the active theme; system high contrast takes precedence. Some Windows-native chrome remains OS styled.
