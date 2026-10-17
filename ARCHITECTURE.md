# Architecture

## Current M1 modules

- **SensSync.Core:** `LinearSensitivityModel` (pure math), bounded Valve text `KeyValues` reader, `Cs2Configuration` (read-only snapshot/hash), `SteamDiscovery` (known paths), `GameDetectionTracker` (pure state transitions).
- **SensSync.Desktop:** WinForms UI, Steam registry discovery, normal process-name/foreground detection with an out-of-context event hook and two-second timer. Owns clipboard access. No write adapter, persistence, or network component.
- **SensSync.Tests:** xUnit numeric/parser/discovery/detection tests and isolated STA window behavior tests. All file fixtures are synthetic; temp directories are uniquely owned by each test.

Data flow: discover → explicitly resolve ambiguity → read snapshot → enter DPI → calculate → copy. Manual source modes bypass file discovery. Choosing a physical distance keeps that target constant when DPI changes; saved/manual CS2 modes describe the sensitivity at the currently entered DPI.

## M1 verification boundary (2026-10-14)

All 74 automated tests pass, including isolated STA window tests. The published self-contained app was inspected through Windows Computer Use: selected real CS2 settings, DPI entry, conversion, Details, clipboard round trip, and saved/manual layouts work at the current desktop scale. Saved yaw is shown in Details; manual yaw is editable in a collapsible advanced row. The selected real file's SHA-256 equals the baseline from before implementation. There is no application file-write API: the game reader opens files with `FileAccess.Read`; the only byte writes in Core are to an in-memory buffer.

Detection is process-basename based, not executable authentication. State transitions are unit-tested, and the no-supported-game-running state was observed; actual CS2/Valorant foreground transitions are not yet exercised. PerMonitorV2 is configured, but cross-monitor/100–200% scaling, high contrast and screen-reader acceptance remain unverified. The self-contained runtime is packaged and the executable launched on the development machine; a clean machine without .NET has not been tested. Static source inspection finds no networking component; no packet capture was performed.

## Units and precision

Yaw is degrees per count at sensitivity 1. DPI is counts per inch; cm/360 is horizontal physical travel for a full turn.

`cm = 914.4 / dpi / sensitivity / yaw`

`sensitivity = 914.4 / dpi / cm / yaw`

CS2 default yaw 0.022 is a manual-input convenience only. Saved-file mode requires its actual `m_yaw`. Valorant candidate yaw is 0.07, source-reviewed but not qualified in actual-game tests yet. Internal values are doubles. Copy rounds to at most nine fractional places; those digits do not claim measurement accuracy. Non-finite, non-positive, or numerically unrepresentable results fail without clamping. Config numbers use invariant culture; UI accepts current-culture or invariant decimals without thousands separators.

## File boundary

VCFG must contain one `config` root and a `convars` object with exactly one sensitivity and yaw key. Read UTF-8 (with/without BOM) or BOM-marked UTF-16. Reject UTF-32 and invalid encoding. Bound input to 2 MiB, nesting to 32, and tokens to 100,000. Unrelated entries are not interpreted as sensitivity. Reading returns a SHA-256 of the original bytes and a timestamp, never a live-game guarantee.

Steam discovery reads current-user and both machine registry views, then numeric userdata account folders. Library VDF and app 730 manifests locate potential autoexec files. Existence of autoexec is a warning; launch options and arbitrary exec chains are not resolved in M1. No complete user configs or personal identifiers belong in committed fixtures.

## Future invariants (not implemented)

Every game write must belong to a transaction service with stopped-game checks, immutable backup, exact preview, precondition hash, staged replacement, post-read verification and rollback. Backup failure must stop the write. File-write verification, persistence and in-game confirmation remain separate states. Detection cannot atomically prevent an independent concurrent launch; user accepted guarded explicit writes in the future. These invariants do not authorize adding writes during M1.

## Research provenance

Reviewed source revisions: Autosens `620b682e9d045a5a741a91103d9f47d170e82d38`; Source2All `de092c68649233fc7389bc42c075f86022dc197b`; SensitivityMatcher `e77d601e81afa16aefe7f150346ef489525207f0`; SmartSens `5c6fe299eee86bde3b9d965b8598d8e4cda2a678`; Ludusavi `70f4abb31497fa5ed9b0d24098200f3662d6fda9`.

Source2All has no license file; Autosens/SmartSens declare MIT without complete license files in inspected trees; SensitivityMatcher is GPL-3.0; Ludusavi is MIT. SensSync is independently implemented and includes no copied competitor source. See the approved conversation plan for the full audit; do not redo research without a concrete question.

## Themes and recognition

The main window owns a session-only light/dark toggle; UiTheme applies a palette recursively and Details receives the selected mode. Theme changes do not recreate controls or recalculate inputs. System high contrast takes precedence. Normal DWM title-bar appearance is requested when supported; native borders and scrollbars may retain OS styling.

Recognition includes the observed Marvel-Win64-Shipping.exe process without adding a sensitivity adapter. Running-process state, focus history, and integration readiness remain distinct. CS2 saved-file readiness is based on a successful snapshot read; Valorant and Marvel Rivals are conversion targets only.

## Selectable output targets

ConversionTarget supplies output-only models for Valorant, default-yaw CS2, Marvel Rivals and experimental BF6. Existing source parsing and cm/360 calculation are unchanged. BF6 uses an affine rate model and enforces the 0–100 menu range. Target changes invalidate copy feedback and recompute display/clipboard/details without changing source, DPI or detected-game state. See CONVERSION_MODELS.md for numeric provenance and live-game validation limits.
