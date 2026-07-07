# Progress

Tracks what's changed and what's next during development. Newest entries at
the top of each section.

## Current status

v0.10 complete: removed the in-app color schemes (Green/Red/Blue/Grey) - the
app now has a single neutral palette and simply inherits the Windows light/dark
mode, tracked live. The Settings tab's color swatches are gone, replaced by a
short note that appearance follows Windows.

v0.9.1 complete: every main tab now scrolls, so nothing is unreachable at
smaller window sizes - the Players editor column, the Tournament create-form
panel, and the Settings pane scroll vertically; the players grid and the
tables row scroll horizontally.

v0.9: the chip tournament - the last unimplemented format. Every
player buys in (funding a pot) and starts with a set number of chips; the
operator logs ad-hoc "winner beat loser" games and the loser drops a chip
(lives rule - the winner is unchanged). A player at 0 chips is out; the last
player holding a chip wins. Live standings show chips, finishing places (as
players are knocked out), and the configured 1st/2nd/3rd payouts. The
read-only Display window mirrors the standings board.

v0.8.1: seed numbers now show on every bracket box, and the
operator's Tournament tab uses the same live bracket tree as the Display
window (with editable boxes - inline score inputs + table picker + Report
on the live match). Shared bracket templates now live in
Themes/BracketTemplates.xaml so both windows render from one source.

v0.8: the read-only Display window renders a proper live bracket tree
(styled after digitalpool.com) instead of flat round columns - round
columns left-to-right, each match centred between its two feeder matches,
elbow connectors, winner rows highlighted, and winners/losers/grand-final
sections for double elimination.

## Next steps

- [x] Scrollbars on every main tab (done in v0.9.1).
- [ ] All five formats are now implemented (single/double elim, round robin,
  ring game, chip tournament). No unimplemented format remains.
- [ ] Chip-tournament follow-ups: an undo/correction control for a mis-recorded
  game (the ledger already supports it - only the UI is missing); optionally
  enforce that payouts don't exceed the pot; a game-history/log view.
- [ ] Ring game follow-ups: rebuys / adding a waiting player into a vacated
  spot mid-session (deferred from 0.7; rotation is fixed for now), and
  optional per-rack pot-distribution rules (pay-the-breaker, etc.).
- [ ] Bracket-display follow-ups: connectors converging into the grand final
  render as long verticals from the LB final (works, but could be prettier);
  irregular losers-bracket fan-in rounds fall back to even spacing with no
  connectors; consider seed numbers / match numbers on each box.

## Change log

## v0.10 — 2026-07-07

- Removed all in-app color schemes. Previously the Settings tab let the user
  pick one of four color schemes (Green/Red/Blue/Grey), each with its own
  light/dark palette, persisted to settings.json. Now the app has a single
  neutral palette (Windows 11-style greys + the standard system accent blue)
  and only inherits the Windows "choose your color mode" light/dark setting,
  tracked live. Deleted the 8 `Palette.{Scheme}.{Light,Dark}.xaml` files, the
  `AppColorScheme` enum, and the `AppSettingsStore` (nothing to persist now);
  added `Palette.Light.xaml` / `Palette.Dark.xaml`. `ThemeService` no longer
  has a color-scheme axis - it just swaps the light/dark palette on the Windows
  setting. The dark-mode native title bar now uses the neutral chrome color
  instead of a scheme accent. Settings tab's swatch buttons replaced by an
  "Appearance" note. Tests: unchanged (77 pass). Verified end-to-end: launched
  the app in dark mode (neutral graphite + blue accent, dark title bar), toggled
  Windows to light mode live and confirmed the app repainted instantly to the
  neutral light palette (white surfaces, light title bar), and confirmed the
  Settings tab shows the new note with no swatches.

## v0.9.1 — 2026-07-07

- Scrollbars on every main tab so content is never clipped off-screen at
  smaller window sizes (previously the create-tournament form's lower fields
  and Create button, the player editor's lower fields, and off-screen table
  buttons could all be unreachable on a short/narrow window). Wrapped the
  Players editor column and the Tournament left create-form panel in vertical
  ScrollViewers, the tables row in a horizontal ScrollViewer, and the Settings
  pane in a ScrollViewer; the players grid, standings/ledger grids, and the
  bracket tree already scrolled on their own. XAML-only, no behavior change.
- Verified by rendering all three tabs (Players, Tournament, Settings) at a
  cramped 900x520 window and confirming the scrollbars appear and every
  control (Create Tournament button, Save button, off-screen table buttons)
  is reachable. Tests unchanged at 77.

## v0.9 — 2026-07-07

- Chip tournament, the last unimplemented format. Confirmed the ruleset with
  the user: a "lives" chip game (loser drops a chip, winner unchanged; out at
  0; last player holding a chip wins), games logged ad-hoc between any two
  active players (no fixed rounds/pairings), with a dollar buy-in funding a
  pot and configurable 1st/2nd/3rd payouts.
- `ChipGameService` (pure/testable): `StartChipTournament` (gives everyone the
  same starting chips, records buy-in/payout settings, marks InProgress),
  `RecordGame` (validates both players are active and distinct, drops the
  loser a chip, eliminates at 0, completes at one player left),
  `ComputeStandings` (replays the game log to chip counts + finishing places -
  first out finishes last, champion is 1st - orders active-by-chips then
  eliminated-by-reverse-elimination, maps places to payouts), and `Pot`
  (buy-in times entrant count). Nothing about chips/places is stored - always
  recomputed from the log.
- New entities `ChipGameDetail` (1:1 with Tournament, like BracketDetail /
  RingGameDetail - starting chips, buy-in, 1st/2nd/3rd payouts) and
  `ChipGameEntry` (a winner/loser/sequence game-log row). Reused
  `TournamentEntrant.IsEliminated` as "out of chips". EF migration
  `AddChipTournament` (two new tables, no changes to existing ones).
- Tournament creation now offers Chip Tournament (starting-chips, buy-in, and
  1st/2nd/3rd payout fields, shown only for that format). The admin Tournament
  tab gains a chip panel - a one-line "N of M left · C chips each · Pot $X"
  status, a Winner/Loser picker + "Record Game" over the still-active players,
  and a live standings grid (place, player, chips, payout). The read-only
  Display window mirrors the standings as a card board (leader highlighted,
  eliminated players dimmed with their finishing place). The
  "chip tournaments aren't supported yet" guard is gone.
- 7 new `Core.Tests` (71 total): start validation, loser-drops-a-chip /
  winner-unchanged, elimination at 0 and completion with champion first,
  reject same-player / eliminated-player games, first-out-finishes-last with
  payouts following place, and eliminated places locking while others play.
- 1 new `Data.Tests` integration test against real SQLite (2 total): the full
  create -> reload -> record-game (persisted via `TrackNew`) -> reload ->
  eliminate -> complete flow, asserting chip counts, eliminations, places, and
  the game log all survive each reload.
- Verified end-to-end by seeding a real 6-player chip tournament through the
  actual services and rendering the real `MainWindow` (operator panel) and
  `DisplayWindow` (standings board) to PNG - the status line, record-game
  pickers, standings grid, locked eliminated places, and dimmed-card display
  all render correctly. Persistence (the risky already-tracked-aggregate
  `TrackNew` insert) is covered by the integration test above.

## v0.8.1 — 2026-07-06

- Seed numbers now render on every match box (both windows): a small muted
  seed before each player name, matching digitalpool's seeded-bracket look.
  Added `Player1Seed`/`Player2Seed` to `MatchRowViewModel` (from the
  entrant's `SeedNumber`; null for BYE/TBD) and threaded seed through
  `PlayerLineViewModel`.
- Carried the bracket tree to the operator's Tournament tab. It previously
  showed the editable bracket as flat round columns; it now uses the same
  tree layout as the Display window, with editable boxes - each box keeps
  the inline score TextBoxes, and the live (reportable) match shows a footer
  with the table picker + Report button (hidden on finished/pending boxes,
  which centre their two lines). Round robin keeps the simple round-column
  list, now using the same editable card. Reporting wiring
  (ReportResultCommand, score/table bindings) is unchanged from the
  previously-verified flat card - only its position moved.
- Refactored the read-only bracket templates (player line, match card, box,
  connector, header, section label, canvas placement) into a shared
  `Themes/BracketTemplates.xaml` merged in App.xaml, so both windows render
  from one source; the operator tab adds only its editable card/box on top.
  `BracketLayoutBuilder.Build` now takes box width/height/row-gap so the
  compact display and the taller editable operator boxes share one
  algorithm.
- No test count change (68 total: 64 Core + 4 App); the builder's tests
  still pass with the parameterised signature. Verified by rendering the
  real `DisplayWindow` and the real `MainWindow` (Tournament tab) to PNG for
  seeded single- and double-elimination brackets - seeds, winner highlights,
  the editable Report footer on the live match, and the winners/losers
  banding all render correctly. Dev DB restored to empty afterward.

## v0.8 — 2026-07-06

- Redesigned the read-only Display window's bracket into a real bracket tree,
  using digitalpool.com's bracket as the visual blueprint. Previously both
  windows showed each round as a flat vertical stack of cards in a column,
  with no tree structure. The Display window now lays matches out as a
  classic elimination bracket: round columns marching left-to-right, each
  match positioned at the vertical midpoint of the two feeder matches that
  flow into it, three-segment elbow connectors joining them, and the winner's
  line in each finished match highlighted (accent fill, dark bold text, its
  score cell knocked out to let the accent show).
- Double elimination stacks a "Winners Bracket" band above a "Losers Bracket"
  band (each its own left-to-right tree with a section label and per-column
  round headers), with grand-final match(es) in a trailing column aligned to
  the winners final and both finals feeding in. Round robin keeps the simple
  round-column list (now using the same match-card look); ring game is
  unchanged.
- New `BracketLayoutBuilder` (pure, in App) computes the pixel layout - box
  positions, connector segments, headers, section labels, total extent -
  purely from each round's (Side, RoundNumber, ordered matches), using the
  standard pairing rules (a round with twice the next round's matches feeds
  pairwise; an equal-count losers "receiving" round feeds straight across) so
  no bracket-node graph is needed. Rendered on a Canvas via four overlaid
  layers (connectors, section labels, headers, boxes). `RoundGroupViewModel`
  now carries the `BracketSide`; `MatchRowViewModel` exposes `Player1Line`/
  `Player2Line` projections so the winner highlight is a DataTrigger on named
  template parts (avoids the local-value-beats-trigger trap in NOTES.md).
- 4 new `App.Tests` (the first tests in that project): empty input, single-
  elimination column/box counts and even column spacing, each match centred
  between its feeders with the right connector count, and double-elimination
  winners-above-losers banding with section labels. Core.Tests unchanged at
  64; total 68.
- Verified end-to-end by seeding a real 8-player single- and double-
  elimination tournament through the actual services into a scratch copy of
  the app's database, then rendering the real `DisplayWindow` (real XAML +
  Green/Dark palette) to PNG via a small harness - confirmed the tree shape,
  winner highlights, pending-match boxes, connectors, and the winners/losers
  banding all render correctly. (Went the render-to-PNG route rather than
  driving the live UI because UI Automation can't see this app's tab content
  and synthetic clicks are unreliable here - see NOTES.md. The user's dev DB
  was empty before and was restored to empty afterward.)

## v0.7 — 2026-07-06

- Ring game, the first non-bracket format and the first to track money.
  Clarified with the user that this is a rotation-order ring game (9-ball):
  N players pay an entry fee and shoot in a fixed drawn order for the
  session; pocketing the 5 pays out and play continues, pocketing the 9
  pays out and ends the rack (the break rotates to the next player).
- `RingGameService` (pure/testable): `StartRingGame` (charges buy-ins,
  draws rotation into `TournamentEntrant.SeedNumber`, seats the opening
  breaker), `RecordMoneyBall` (5 vs 9 semantics), `AdvanceShooter`
  (miss/turn passes, skipping cashed-out players and wrapping), `CashOut`
  (marks `IsEliminated`, stamps realized net, completes the tournament at
  one player left), and `ComputeStandings`/`PotRemaining`. Money is
  conserved: every player's net sums to the negative of the pot still on
  the table. Nothing about net/pot is persisted - always recomputed from
  the ledger.
- New entities `RingGameDetail` (1:1 with Tournament, like BracketDetail -
  buy-in, 5/9 payouts, current rack, current shooter) and `RingLedgerEntry`
  (BuyIn/MoneyBall/CashOut rows). Reused `TournamentEntrant.SeedNumber` as
  the drawn rotation position and `IsEliminated` as "cashed out" to keep
  the entrant schema unchanged. EF migration `AddRingGame` (two new tables,
  no changes to existing ones).
- Tournament creation now offers Ring Game (buy-in and 5/9 payout fields,
  shown only for that format; no entrant-count restriction). Chip
  tournament remains the only unsupported format. The admin Tournament tab
  gains a live ring panel - rotation cards with the current shooter
  highlighted and cashed-out players dimmed, "Made the 5 / Made the 9 /
  Miss" and per-player "Cash Out" controls, a one-line "Rack N · Pot $X ·
  Up: [player]" status, and a money ledger grid. The read-only Display
  window mirrors the rotation + money board off the same shared state.
- 11 new `Core.Tests` (64 total): buy-in/rotation/first-shooter setup,
  <2-player rejection, 5-ball (pay + keep table) vs 9-ball (pay + rack
  advance + break rotation), shooter advancement with wrap, rotation
  skipping a cashed-out player, cash-out-while-your-turn handoff, realized
  net on cash-out, completion at one player left, rejecting a cashed-out
  shooter, and net-ordering + money-conservation of standings.
- 1 new `Data.Tests` integration test against real SQLite: the full
  create -> reload -> made-the-9 (persisted via `TrackNew`) -> reload ->
  cash-out -> reload flow, asserting rotation, rack, shooter, winnings,
  net, pot, and elimination all survive each reload.
- Verified: the built exe launches cleanly against a real database (the
  `AddRingGame` migration applies on startup, MainWindow XAML incl. the new
  ring panel parses, DI resolves, no error log). The ring-game logic and
  its full persistence round-trip were verified via the integration test
  above rather than synthetic UI clicks, since UI Automation can't see this
  app's tab-page content and synthetic clicks are unreliable here (both
  documented in NOTES.md); the app was confirmed to start and render.
- Environment note (see NOTES.md): the .NET SDK had gone missing on this
  machine (only the runtime remained), blocking all builds; reinstalled the
  .NET 8 SDK via winget to proceed.

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
