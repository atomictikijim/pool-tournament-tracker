# Progress

Tracks what's changed and what's next during development. Newest entries at
the top of each section.

## Current status

v0.6.2 complete: removed the Active/Deactivate flag and workflow from
Players - every player is now always an eligible tournament entrant.

## Next steps

- [ ] 0.7 — Ring game: seat/queue/ledger state machine, buy-in/out UI,
  live seat/queue view (first non-bracket format).

## Change log

## v0.6.2 — 2026-07-06 (UI)

- Removed `Player.IsActive` and the whole Deactivate workflow (the
  "Active" roster grid column, the "Active" checkbox and "Deactivate"
  button on the detail panel, and the `DeactivatePlayerCommand`) - not
  needed, by explicit request. Entrant candidates for a new tournament
  are no longer filtered by this flag; every player is eligible.
  `RemovePlayerIsActive` migration drops the column - verified against
  the real dev database, including a player that had actually been
  deactivated in an earlier session, which now loads and appears as a
  normal, selectable entrant candidate again.
- `Table.IsActive` (a different entity, an unrelated feature) was left
  untouched - the migration only targets the `Players` table.
- Verified manually: ran the app against the existing dev database,
  confirmed the roster grid and detail panel no longer show any
  Active/Deactivate UI, and confirmed a previously-deactivated player now
  shows up normally in both the roster and the Tournament tab's entrant
  checklist.

## v0.6.1 — 2026-07-06 (UI)

- Removed `Player.UsaplRating` (and `RatingSystem.Usapl`) - flagged as a
  duplicate of Fargo Rate, so kept only one. Removed the "USAPL" roster
  grid column and "USAPL Rating" detail-panel field; "Seed by rating" no
  longer offers USAPL as a choice. `RemoveUsaplRating` migration drops the
  column - verified against the real dev database (8 existing players
  loaded cleanly with no errors after the migration ran).
- Relabeled "Fargo Rate" to "Fargo Rating" everywhere it's user-facing
  (detail panel and the validator's negative-rating error message); the
  underlying `Player.FargoRate` property/column name is unchanged.
- Verified manually: ran the app against the existing dev database,
  confirmed the roster grid and detail panel no longer show USAPL, the
  Fargo field now reads "Fargo Rating", and all 8 pre-existing players
  still loaded correctly.

## v0.6 — 2026-07-06

- `RoundRobinSchedulingService.GenerateSchedule`: circle-method scheduler -
  fixes one entrant, rotates the rest each round, padding with a ghost bye
  slot for odd entrant counts (no Match is created for whoever draws the
  bye that round). Produces N rounds for odd N and N-1 for even N, with
  every entrant playing every other entrant exactly once. `Match` gained a
  `RoundNumber` column (`AddRoundRobinRoundNumber` migration) since round
  robin has no bracket/`BracketNode` to hang a round number off of.
- `RoundRobinStandingsService.ComputeStandings`: ranks entrants by wins
  descending; entrants still tied on wins are ordered by head-to-head
  record within just that tied group, then (still tied) by point
  differential, then (still tied) by games-won % - each rule only breaks
  ties the previous rule couldn't. Computed on demand from completed
  `Match` rows every time, never persisted separately.
- Tournament creation now supports Round Robin as a format choice (no
  entrant-count restriction, unlike double elimination's power-of-2
  requirement). Reporting a result now also completes the tournament and
  announces a champion (the #1 standings row) once every round-robin match
  is done, alongside the existing bracket-final-match completion path.
- New "Standings" panel next to the bracket/round view, visible only for
  round-robin tournaments (rank/wins/losses/diff/games % columns), live-
  updating on every reported result.
- 16 new `Core.Tests` (53 total): schedule round-count and every-pair-once
  checks for both odd and even entrant counts, no-repeat-within-a-round
  check, and standings tests for the wins-ranking, incomplete-match
  exclusion, head-to-head-before-point-differential, and point-
  differential-fallback-on-a-head-to-head-cycle cases.
- Verified manually end-to-end: created a 4-entrant round robin, confirmed
  the 3-round schedule (each player appearing exactly once per round),
  reported all 6 results through the UI, watched the Standings panel
  update live after each one (including a real point-differential-over-
  games-% tiebreak resolving live between two 2-0 entrants), and confirmed
  the tournament completed with the correct champion announced.
- Bug caught during that verification (see NOTES.md): the in-app "Report
  Result" button's *first* click after typing scores routinely did nothing
  (no error, no state change) - a second click always then worked. Root
  cause not identified; documented as a UI quirk to watch for, not
  fixed, since retrying resolves it and no data corruption was observed.
  A separate false alarm during the same session (an apparent FOREIGN KEY
  crash) turned out to be caused by clicking that same button twice in
  quick succession while automating the UI, not a real defect - confirmed
  by replaying the exact same sequence twice against copies of the real
  database with no failure.

## v0.5 — 2026-07-06

- Three new color schemes (Red, Blue, Grey) alongside the existing Green,
  each with its own light+dark palette pair following the same
  structure/shading as Green's (deep near-black background, a vivid
  mid-bright accent, muted borders, pale text) with the hue rotated -
  `Themes/Palette.{Scheme}.{Light,Dark}.xaml`, 6 new files.
- New "Settings" tab: four color swatches: click one to switch schemes
  live, no restart needed. The active scheme gets a highlighted border.
- `ThemeService.ColorScheme` (new, an `AppColorScheme` enum) is now a
  second axis alongside the existing Windows light/dark tracking - the
  active palette dictionary is `Themes/Palette.{ColorScheme}.{Light,Dark}`,
  computed from both. Persisted to
  `%LOCALAPPDATA%\PoolTournamentManager\settings.json` (new
  `AppSettingsStore`) and reloaded on startup.
- Verified manually: all 4 schemes render correctly in both light and dark
  mode (including the title bar), switching one live repaints every open
  window instantly, and the choice survives an app restart.
- Bug caught during that verification (see NOTES.md): `System.Text.Json`
  serializes enums as bare integers by default, silently failing (and
  falling back to Green) against a hand-written string value in the
  settings file - fixed by adding `JsonStringEnumConverter` so the
  persisted file is actually human-readable/editable as intended.

## v0.4.2 — 2026-07-06 (UI)

- Fixed button text silently rendering in `TextPrimaryBrush` instead of the
  intended `AccentPrimaryTextBrush` (white in light mode) - see NOTES.md.
  Buttons now get an explicit `ContentTemplate` so their text color is a
  real local value instead of losing to the global implicit `TextBlock`
  style.
- Dark mode's window chrome - the native title bar (via new
  `TitleBarColorizer`, DWM `DWMWA_CAPTION_COLOR`/`DWMWA_TEXT_COLOR`), the
  header banner behind the logo, and the tab strip - now matches the button
  green (`WindowChromeBrush`, a new palette key), while `AppBackgroundBrush`
  still governs the actual content area (grids, cards, bracket view) with
  the original deep-green palette. Went through a couple of overshoots
  first (matching the *entire* app background to the button green, which
  made buttons disappear into it) before landing here by explicit
  iteration with the user.
- The selected tab now reads as "cut into" the content area below it
  (its background switches to `AppBackgroundBrush`) rather than using a
  colored underline, since the underline's obvious color choice
  (`AccentPrimaryBrush`) is now the same as the strip it'd sit on.
- `ThemeService` now also re-colors every open window's title bar on every
  theme change (live, no restart), and exposes `ApplyTitleBar` for a
  newly-opened window (e.g. the Display window) to call once on
  `SourceInitialized`.

- Fixed button text silently rendering in `TextPrimaryBrush` instead of the
  intended `AccentPrimaryTextBrush` (white in light mode) - see NOTES.md.
  Buttons now get an explicit `ContentTemplate` so their text color is a
  real local value instead of losing to the global implicit `TextBlock`
  style.
- Dark theme's `AppBackgroundBrush` changed to the same green as
  `AccentPrimaryBrush` (#8FCB3E), by explicit request - confirmed with the
  user that this means buttons/borders blend into the background, visible
  mainly via their white text.

## v0.4.1 — 2026-07-06 (UI)

- Color scheme derived from `images/SwampThingFill_square_11720.png` (deep
  swamp greens, chartreuse highlights) and `images/You Chalkin To Me.png`
  (the gator's teal polo shirt) - sampled to exact hex values rather than
  eyeballed. Two palettes (`Themes/Palette.Light.xaml` /
  `Palette.Dark.xaml`), same brush keys in both, each themed independently
  for contrast (e.g. the chartreuse accent becomes a deeper forest green on
  a light background).
- `ThemeService`: reads the Windows "choose your color mode" setting
  (`HKCU...Themes\Personalize\AppsUseLightTheme`) at startup and live-tracks
  it via `SystemEvents.UserPreferenceChanged`, swapping the active palette
  dictionary with no app restart needed - verified by flipping the Windows
  setting while the app was running and watching it repaint immediately.
- `Themes/Generic.xaml`: shared themed styles for every control type used
  by the app. Button, TabItem, and ComboBox needed full `ControlTemplate`
  overrides, not just Setters - their default chrome ignores `Background`
  entirely (see NOTES.md).
- App icon (`Assets/AppIcon.ico`, multi-resolution, transparent background)
  and header logo (`Assets/Logo.png`, transparency-keyed) generated from
  "You Chalkin To Me.png" - shows in the taskbar, title bar, and both
  windows' headers.
- Verified manually: light and dark rendering of both windows, a live
  Windows-setting toggle in each direction, taskbar/title-bar icon
  rendering, and that the re-templated ComboBox still opens and selects
  correctly (not just looks right).

## v0.4 — 2026-07-06

- `BracketGenerationService.GenerateDoubleElimination`: builds a losers
  bracket alongside the winners bracket, wiring each winners-bracket round's
  losers into the correct losers-bracket round (a "receiving" round once
  counts already match, preceded by a pure "consolidation" round whenever
  they don't) and a Grand Final between both brackets' champions, with a
  single bracket-reset rematch if the losers-bracket champion wins it.
  Requires an exact power-of-2 entrant count for now (2/4/8/16/32...) -
  seeding byes through both brackets at once is a known gap, called out in
  the UI's validation message rather than silently mishandled.
- `RecordMatchResult` now returns every newly-materialized match a single
  reported result can produce (up to two: the winner's advance and the
  loser's drop into the losers bracket), not just one.
- `BracketNode` gained explicit `FeedsIntoWinnerSlot`/`FeedsIntoLoserSlot`
  fields (see NOTES.md) so losers-bracket "receiving" rounds - which mix a
  losers-bracket survivor with a freshly-dropped winners-bracket loser on the
  same node - can't collide on which slot each side lands in.
  EF migration `AddDoubleEliminationSlots`.
- Tournament creation now supports Double Elimination as a format choice,
  validating the power-of-2 entrant count with a clear message when it isn't.
- Bracket display (both the admin Tournament tab and the read-only Display
  window) groups rounds by bracket side, not just round number, and labels
  them "WB Round N"/"WB Final", "LB Round N"/"LB Final", "Grand Final", and
  "Bracket Reset" - single-elimination brackets keep their original
  unprefixed titles.
- 8 new `Core.Tests` (37 total): non-power-of-2 rejection, node-shape checks,
  a full 4-entrant playthrough with no reset, a full 4-entrant playthrough
  that forces a bracket reset, and a full 8-entrant playthrough that
  exercises the losers-bracket consolidation rounds.
- Verified manually end-to-end: created a 4-entrant double-elimination
  tournament, played it so the losers-bracket champion beat the winners-
  bracket champion in the Grand Final (forcing a reset), played the reset,
  and confirmed the champion and "Completed" status in both windows.

## v0.3 — 2026-07-06

- `TournamentStateService` (DI singleton): the single shared source of truth
  for the currently-open tournament, its bracket rounds, and its tables -
  injected into both the admin window's `TournamentViewModel` and the new
  `DisplayWindowViewModel`, so both windows are bound to the exact same
  objects with no messaging/polling needed to stay in sync.
- `DisplayWindow`: new read-only second window (dark/projector-style theme)
  showing the tournament name/status, a live "Now Playing" table board, and
  the full bracket with completed matches' winners highlighted in gold.
  Structurally read-only - its ViewModel exposes no `ICommand` mutators, only
  projections over the shared state.
- "Open Display Window" button on the admin window's Tournament tab, opens/
  activates a single display window instance via DI (`IServiceProvider`).
- Verified manually end-to-end: created a tournament, opened the display
  window, assigned a table and reported the final score entirely from the
  admin window, and confirmed the display window reflected each change
  immediately (status, "Now Playing" table board, gold winner highlight)
  without any restart or manual refresh.
- Two real bugs found and fixed during that verification (see NOTES.md):
  the winner-highlight and "table open" fallback text never appeared because
  local property values on the same XAML elements were silently overriding
  the Style triggers meant to change them.

## v0.2 — 2026-07-06

- `Tournament`, `TournamentEntrant`, `Table`, `Match`, `BracketDetail`,
  `BracketNode` entities + `GameType`/`TournamentFormat`/`TournamentStatus`/
  `RatingSystem`/`BracketSide`/`MatchStatus` enums.
- `SeedingService`: orders entrants by a chosen rating system (Fargo/USAPL/
  TAP/APA 8-ball/APA 9-ball), missing ratings sort last.
- `BracketGenerationService`: standard recursive seed-pairing single-elim
  generation (byes to top seeds for non-power-of-2 entrant counts) and
  score-entry-driven advancement, materializing each round's matches only
  once both slots are known. 29 passing `Core.Tests`, including bye counts
  for N = 3, 5, 6, 7, 11 and a full 6-entrant bracket played to completion.
- EF Core migration `AddTournamentBracket`; `ITournamentRepository` with a
  `GetByIdAsync` that eager-loads the whole tournament/bracket/match graph.
- WPF "Tournament" tab: create tournament + pick entrants from active
  players, live bracket view grouped by round, inline score entry, table
  add/assign, champion banner on completion.
- Verified manually end-to-end via UI Automation: seeded and played a real
  6-entrant bracket (byes to top 2 seeds, correct 4v5/3v6 first-round
  pairing, correct semifinal/final pairings) to a declared champion, and a
  2-entrant tournament to confirm the summary list's status updates live.
- Two real bugs found and fixed during that verification (see NOTES.md):
  round-2+ slots being mis-treated as byes, and new entities attached to an
  already-tracked aggregate failing to persist (`FOREIGN KEY constraint
  failed` for the FK case, silent no-op for the non-FK case).

## v0.1 — 2026-07-06

- Solution scaffolded into `Core` (domain/algorithms, no WPF/DB deps),
  `Data` (EF Core + SQLite persistence), `App` (WPF, MVVM via
  CommunityToolkit.Mvvm), and matching `tests/` projects.
- `Player` entity with contact info + Fargo/USAPL/TAP/APA (8-ball & 9-ball)
  rating fields; `PlayerValidator` enforces required name fields and APA
  skill range 1-9; 12 passing unit tests in `Core.Tests`.
- SQLite persistence via EF Core, database created at
  `%LOCALAPPDATA%\PoolTournamentManager\tournaments.db` on first run,
  `InitialCreate` migration in place.
- WPF admin window: roster grid + detail panel supporting add/edit/save/
  deactivate, wired through DI (`App.xaml.cs`) with a global exception
  handler per CLAUDE.md's best practices.
- Verified manually by running the built exe and driving it via UI
  Automation: added players, confirmed persistence survives a full app
  restart, deactivated a player and confirmed the UI reflects it live.

<!--
Entry format:

## v0.X[.Y] — YYYY-MM-DD

- What changed.
- Why (if not obvious).
-->
