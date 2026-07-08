# Progress

Tracks what's changed and what's next during development. Newest entries at
the top of each section.

## Current status

v0.16 complete: Teams now have two new optional fields, **Division** (a short
number/alphanumeric code) and **Location** (the pool hall the team plays out
of), editable on the Teams tab and shown as columns in the Teams grid. New EF
Core migration (`AddTeamDivisionAndLocation`); the 44 teams already in the dev
test data were backfilled with plausible values.

v0.15.1 complete: on the Tournament Settings tab, the Entrants picker (Players or Teams,
whichever applies) now sits in its own column to the right of the create-tournament form
instead of stacked underneath it, and the "Appearance" blurb about following the Windows
light/dark theme has been removed from the tab entirely (still true, just no longer worth a
paragraph on a settings screen with nothing to configure).

v0.15 complete: the whole bracket tree (every round, every format) now renders
from the moment a tournament is created, with "TBD" placeholder boxes that
fill in with real names as entrants advance, instead of a round only
appearing once every match in it is playable. Double Elimination's Grand
Final and Modified Single Elimination's cross-pod Final stage now render as
trailing columns inside the "Winners Bracket" section instead of their own
separately-labeled band.

v0.14.1 complete: the main tab selectors (Players/Teams/Tournament/Tournament
Settings) now look like a row of separate buttons instead of flat text in a
strip.

v0.14 complete: entry fees, a host cut, and place-based prize payouts.
Single/Double/Modified Elimination, Round Robin, and Chip Tournament all get
a per-entrant entry fee, a host fee percentage, and any number of payout
places with percentages of the prize pool (must sum to 100%). Chip
Tournament's old fixed-dollar buy-in/1st/2nd/3rd payouts are gone, replaced
by this generic system. Ring Game is unchanged (its own buy-in/per-ball-payout
model, no discrete finishing order).

v0.13 complete: a new Modified Single Elimination format (APA's format:
entrants are split into 8-entrant pods, each running a shortened
double-elimination-style ladder down to 2 "reps," which then feed a plain
single-elimination stage with no further consolation chances). Available to
both Players and Teams, like Single/Double Elimination.

v0.12 complete: a new Teams tab (name-only roster) and a per-tournament "Use
Teams" toggle so Single Elimination and Double Elimination can be run with
Team entrants instead of individual Players. Round Robin, Ring Game, and
Chip Tournament remain individual-players-only.

v0.11 complete: players can be added to a tournament after creation (while
still pre-play), every non-Ring-Game format now requires a table count at
creation, and matches have separate Start/Finish steps with a live per-match
timer instead of a single Report button.

v0.10.1 complete: reorganized the tournament tabs. The "Settings" tab is
renamed "Tournament Settings" and now hosts the whole create-tournament form
(name, game, format, seed-by-rating, ring/chip options, entrant selection, and
Create) that used to sit in the Tournament tab's left column. The Tournament
tab's left column is now just the tournament picker (with a hint pointing to
Tournament Settings for creation); its right side still operates the selected
tournament (tables, bracket, standings, ring/chip controls). App Appearance
note stays at the bottom of Tournament Settings.

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

- [ ] **Manual UI testing of v0.14** (entry fees/host cut/prize payouts) - no
  UI-automation tool was available when it shipped, so the create-tournament
  flow, live total/percentage hints, and the Prize Payouts panel were never
  click-tested end-to-end. Create a tournament with an entry fee and a few
  payout places, confirm the live hints behave, and check the Prize Payouts
  panel on a completed bracket with a tied place.
- [x] Entry fee, host cut, and place-based prize payouts (done in v0.14).
- [ ] Elimination-bracket payouts beyond 1st/2nd (champion/runner-up, always
  exact) use a win/loss-record heuristic to group tied places (e.g. two
  semifinal losers tied for 3rd-4th) rather than exact bracket-depth
  traversal - documented simplification, see NOTES.md.
- [ ] Ring Game intentionally has no entry-fee/prize-payout UI - it keeps its
  own buy-in + 5-ball/9-ball payout fields, since it has no discrete
  finishing order to pay places against.
- [ ] No way to edit a tournament's entry fee/host fee/prize places after
  creation, same as Ring/Chip Tournament's existing buy-in fields - it's a
  create-time-only configuration.
- [x] Modified Single Elimination format (done in v0.13).
- [ ] Modified Single Elimination currently requires an entrant count that's a
  multiple of 8 and a power of 2 (8, 16, 32, 64...) - partial pods and byes
  within a pod are not supported yet, same kind of scope-cut Double
  Elimination shipped with.
- [x] Teams tab + Team entrants for Single/Double Elimination (done in v0.12).
- [ ] Teams are name-only by design (no player roster/membership) - there is
  no way to see which players make up a team, and no per-player stats roll
  up through team results.
- [x] Add-players-after-creation, required table counts, match Start/Finish +
  timer (done in v0.11).
- [ ] Ring Game / Chip Tournament formats don't use Match/Table at all, so
  Start/Finish/timer only applies to Single/Double Elimination and Round
  Robin; Chip Tournament still requires a table count at creation (per "every
  format except Ring Game") but nothing currently consumes it.
- [ ] Add-player-after-creation is pre-play-only for every format (locked once
  any match/game has been played) - no support yet for adding players
  mid-tournament even where a format could technically tolerate it (e.g. Ring
  Game's rotation, round robin's schedule).
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

## v0.16 — 2026-07-08

- Added `Division` and `Location` (nullable strings) to the `Team` entity, editable from the
  Teams tab's detail panel and shown as new columns ("Division", "Location") in the Teams grid.
  Division is meant as a short code (e.g. "1" or "A"); Location is the name of the pool hall the
  team plays out of. Neither field participates in seeding/scheduling - informational only.
- New migration `AddTeamDivisionAndLocation` adds the two nullable `TEXT` columns to `Teams`; no
  data loss, existing rows just get `NULL` until edited.
- Manually verified in the running app: both new fields show in the grid and round-trip correctly
  through the detail panel's Save/select flow.
- Backfilled the 44 teams already in the dev test data with a division (cycling "1"-"4"/"A"-"C")
  and a location (cycling through a dozen pool-hall names) via a one-off script against the
  repositories - not part of the app itself, just dev-environment test data.

## v0.15.1 — 2026-07-08 (UI)

- Tournament Settings tab: the Entrants list (Players or Teams, whichever the tournament format/
  toggle calls for) now lays out in its own column to the right of the create-tournament form via
  a two-column `Grid`, instead of stacked below the form fields inside the same narrow column.
- Removed the "Appearance" heading and its explanatory paragraph about following the Windows
  light/dark theme - correct but not worth space on a settings screen with nothing to configure.
- Manually verified in the running app: Players entrants render at right for the default
  (non-team) form, and toggling "Use Teams" swaps in the Teams entrants list in the same spot.

## v0.15 — 2026-07-08

- **Whole bracket tree visible from tournament creation.** Previously a round only appeared once
  every match in it had "materialized" (both slots known), so a fresh 8-player bracket showed
  only Round 1 - the Quarterfinal/Semifinal/Final columns popped into existence one at a time as
  the tournament progressed. `BracketNode` already exists for every round of the entire tree
  upfront (created by `BracketGenerationService` at tournament creation) - only `Match` rows are
  lazily materialized once both slots resolve. The gap was purely in the App layer, which threw
  away any node whose `Match` hadn't materialized yet before it reached the view models. No
  Core/database change was needed.
- `MatchRowViewModel.Match` is now nullable; a new constructor represents a bracket slot with no
  materialized match yet, showing whichever entrant(s) have already arrived via a prior round's
  result and "TBD" for the rest (never "BYE" - a placeholder is never a legitimate bye, since
  Round 1's byes are always resolved immediately and every later-round/Grand-Final node always
  needs two real winners to arrive). `TournamentStateService.RebuildRounds` no longer filters out
  nodes without a `Match`, resolving placeholder names against `tournament.Entrants` (always
  fully loaded, unlike `Match.Player1Entrant`/`Player2Entrant` navigation). Every existing
  read-only/editable card template needed **zero XAML changes** - `IsStartable`/`IsInProgress`/
  `IsComplete` all naturally evaluate false for a placeholder, so it renders as a plain "TBD vs
  TBD" box with no Start/Finish controls, exactly matching the existing pending-match look.
- **Grand Final and Modified Single Elimination's cross-pod Final stage now render inside the
  Winners Bracket section** rather than their own band. Double Elimination's Grand Final already
  had no separate section label (just needed to keep working once its placeholder always exists
  from creation); Modified Single Elimination's cross-pod stage previously got its own "Final
  Rounds" band stacked below Losers Bracket - `BracketLayoutBuilder` now lays it out as trailing
  columns in the same band as Winners, right after the last Winners column.
- Tests: 104 pass (91 Core + 2 Data + 11 App, up from 98 total) - new
  `BracketFullVisibilityTests` (real-SQLite, same pattern as `TournamentEntrantAdditionTests`)
  proves the Final round shows "TBD vs TBD" before Round 1 is played and fills in real winner
  names after; new `MatchRowViewModelTests` cover the placeholder constructor (TBD not BYE for
  unresolved slots, one-slot-resolved, all match-state properties false); new
  `BracketLayoutBuilderTests` case proves a `BracketSide.Final` round list lays out inside the
  Winners band with no separate "Final Rounds" section label.
- Verified end-to-end in the running app: created a fresh 8-entrant Single Elimination
  tournament and confirmed Round 1/Semifinals/Final all render immediately (the bye winner
  already showing in the Semifinal slot), played a Round 1 match and watched its Semifinal slot
  fill in with the real winner's name while Final stayed "TBD vs TBD"; opened an existing Double
  Elimination tournament and confirmed the Grand Final box (TBD vs TBD, connected to both the WB
  Final and LB Final) sits beside the Winners band with no separate section label; opened a
  completed Modified Single Elimination tournament and confirmed its Semifinals/Final columns
  render under the "Winners Bracket" label rather than a separate "Final Rounds" band.

## v0.14.1 — 2026-07-08 (UI)

- The main tab selectors (Players/Teams/Tournament/Tournament Settings) now
  look like a row of separate buttons - rounded, bordered chips with spacing
  between them - instead of flat text sitting directly on the window,
  matching the app's existing Button look. The selected tab fills with
  `AccentPrimaryBrush` like a pressed/primary button; unselected tabs use the
  neutral `ControlBackgroundBrush` control surface. `Themes/Generic.xaml`'s
  `TabItem` style gained a full `ControlTemplate` (replacing the previous
  flat-strip template) plus a `HeaderTemplate` so the header text keeps its
  own real `Foreground` local value rather than losing to the implicit
  `TextBlock` style, same pattern already used for `Button`.
- Real bug caught and fixed during that work (see NOTES.md): the header text
  fix was first written as a `ContentTemplate` Setter, which - because the
  template's `ContentPresenter` uses `ContentSource="Header"` - actually
  overrides each tab's *page* content instead (via `TabControl`'s
  `ContentSource="SelectedContent"` auto-binding to the selected TabItem's
  `ContentTemplate`), so every tab page silently rendered as the literal
  `ToString()` of its content object (e.g. "System.Windows.Controls.
  ScrollViewer") instead of the real UI. Fixed by using `HeaderTemplate`
  instead, which is what `ContentSource="Header"` actually looks up.
- No test count change (98 pass); XAML-only, no view-model/logic changes.
  Verified end-to-end by screenshotting the running app: all four tabs
  render as button-styled chips in both the unselected and selected states,
  and every tab's page content (Players grid + detail panel, Tournament
  Settings' create-tournament form, etc.) renders correctly - confirming the
  ContentTemplate/HeaderTemplate regression above was fully fixed, not just
  the header appearance.

## v0.14 — 2026-07-07

- **Entry fees, host cut, and place-based prize payouts.** New generic
  `Tournament.EntryFee`/`HostFeePercentage`/`PrizePlaces` (a new
  `TournamentPrizePlace` entity: `Place` + `Percentage`, one row per paid
  place) apply to Single/Double/Modified Elimination, Round Robin, and Chip
  Tournament. `PrizePool = (EntryFee * entrant count) * (1 - HostFeePercentage)`,
  split across the configured places by percentage (must sum to 100%). Ring
  Game is deliberately excluded - it's a continuous cash game with no discrete
  finishing order, so it keeps its own buy-in/5-ball/9-ball payout fields
  untouched.
- **Chip Tournament's old fixed-dollar buy-in/1st/2nd/3rd payout fields are
  gone**, replaced by the generic system above - its place data (`ChipGameService
  .ComputeStandings`'s `Place`) already mapped cleanly onto it.
  `ChipGameService.StartChipTournament` dropped its `buyIn`/`firstPayout`/
  `secondPayout`/`thirdPayout` params (down to just `startingChips` -
  `tournament.EntryFee` etc. are set by the caller beforehand, same pattern as
  `SeedingRatingSystem`). `ChipGameService.Pot`/`ChipStandingRow.Payout` are
  gone too - callers use the new `PrizePayoutService` instead.
- **New `PrizePayoutService` (Core)** computes placements + payouts for every
  covered format. Round Robin and Chip Tournament already have an exact, never-
  tied placement, reused as-is. Elimination brackets have no placement concept
  beyond the champion/runner-up (the deciding match, found per `BracketKind`:
  the top Winners/Final-side node, or the Grand Final preferring its reset
  match) - 1st/2nd are always exact. 3rd place and below are a **deliberate
  simplification**: remaining entrants are ranked by match win/loss record
  (excluding byes), and entrants with identical records tie, splitting the
  combined payout for the place range they occupy evenly. This is not exact
  bracket-depth traversal (which would need bespoke per-`BracketKind`, per-pod
  graph walking with no existing precedent) - see NOTES.md.
- Migration `AddEntryFeeAndPrizePayouts`: adds `Tournaments.EntryFee`/
  `HostFeePercentage`, new `TournamentPrizePlaces` table, drops
  `ChipGameDetails.BuyInAmount`/`FirstPlacePayout`/`SecondPlacePayout`/
  `ThirdPlacePayout`. Carries over each existing chip tournament's
  `BuyInAmount` into the new `EntryFee` (a clean 1:1 copy) before dropping it;
  the old fixed-dollar payouts have no safe equivalent conversion into
  percentages (can't know if they summed to the full pot), so no
  `TournamentPrizePlaces` rows are synthesized for pre-existing chip
  tournaments - a one-time, low-risk loss since chip payouts shipped the same
  day as this change in the dev DB.
- Tournament Settings' create form: chip-tournament setup shrinks to just
  "Starting chips per player"; a new shared "Entry fee ($) / Total collected
  (live) / Host fee (%) / Number of payout places / Place N: __%" section
  appears for every format except Ring Game, with a live "Total: XX%" hint
  and validation (`CreateTournamentAsync` rejects a non-100% sum, a negative
  fee, or an out-of-range host percentage). The Tournament tab and Display
  window both gained a shared "Prize Payouts" panel (entry fees / host cut /
  prize pool summary + a per-place payout list), populated live for Round
  Robin/Chip Tournament and only once completed for elimination brackets.
- Tests: 98 pass (91 Core + 2 Data + 5 App, up from 89 total) - new
  `PrizePayoutServiceTests` covers the money math, empty-payout edge cases
  (no places configured, Ring Game, an incomplete bracket), exact Round
  Robin/Chip Tournament payouts, exact Single Elimination 1st/2nd with tied
  3rd-4th, exact Double Elimination with no ties at all (every place is
  unambiguous for N=4), and a Modified Single Elimination 8-entrant pod
  producing three separate tied-pair tiers (3rd-4th/5th-6th/7th-8th).
  `ChipGameServiceTests`/`ChipGamePersistenceTests` updated for the trimmed
  `StartChipTournament` signature.
- Verification note: no UI-automation tool was available this session, so the
  create-tournament flow, live total/percentage hints, and the new Prize
  Payouts panel were not click-tested end-to-end. Verified instead: a clean
  build, all 98 tests green, the migration applying successfully against the
  real dev database, and the built exe launching and rendering both windows
  without error. The user opted to ship on this basis and spot-check manually
  afterward rather than block the release on it.

## v0.13 — 2026-07-07

- **Modified Single Elimination**, a new `TournamentFormat` modeled on APA's
  official format (confirmed against APA's own reference diagram and directly
  with the user): entrants are randomly drawn (not rating-seeded) into pods of
  8. Each pod runs Round 1 -> Losers Round 1 (a loss here eliminates outright)
  -> Winners Round 2 -> Losers Round 2 ("receiving": pairs a Losers-Round-1
  survivor with a Winners-Round-2 loser) -> a Final Four, whose 2 winners
  become that pod's "reps." Every pod's reps then feed one ordinary
  single-elimination bracket (semifinal/final for 2 pods, quarterfinal onward
  for 4+) with no further consolation chances - "once it enters the
  semifinal/final it's single elimination." Available to both Players and
  Teams, like Single/Double Elimination; requires an entrant count that's a
  multiple of 8 and a power of 2 (8, 16, 32, 64...) for now.
- `BracketGenerationService.GenerateModifiedSingleElimination` reuses the
  double-elimination "receiving round" helper as-is for each pod's Losers
  Round 2, and reuses the existing round-building recursion (parameterized
  with a new `BracketSide.Final`) for the cross-pod single-elimination stage -
  `RecordMatchResult` needed **zero changes**, since it already only drops a
  loser further when the completed node is on the Winners side with a wired
  `FeedsIntoLoserNodeId`, which is exactly how this format's Losers-Round-1/2
  nodes are (deliberately) left unwired.
- `BracketDetail.IsDoubleElimination` (bool) became a `BracketKind` enum
  (`SingleElimination`/`DoubleElimination`/`ModifiedSingleElimination`),
  explicitly numbered to match the old bool's stored ints - the migration was
  a plain column rename with no data conversion needed.
- Two real bugs found and fixed while building this (see NOTES.md): (1) the
  cross-pod round's winner-propagation silently used the completed node's own
  `PositionInRound` for slot inference, which is ambiguous once nodes come
  from different pods with independent numbering - a 16-entrant playthrough
  test caught the semifinal never materializing, fixed by setting
  `FeedsIntoWinnerSlot` explicitly from the interleaved list's own index. (2)
  `IsEliminationBracket` in both `TournamentViewModel` and
  `DisplayWindowViewModel` still only listed Single/Double Elimination, so the
  new format's bracket silently failed to render in the UI despite generating
  correctly - caught by the manual run-through, not by the (format-agnostic)
  Core tests.
- Tests: 89 pass (82 Core + 2 Data + 5 App, up from 81 total - added
  `ModifiedSingleEliminationBracketTests` covering the invalid-entrant-count
  rejection, the exact 13-match shape/sequence for a standalone 8-entrant pod,
  and a 16-entrant playthrough proving 2 pods each contribute exactly 2 reps
  into a real semifinal/final). Verified end-to-end in the running app:
  created an 8-player Modified Single Elimination tournament, confirmed "Use
  Teams" appears and "Seed by rating" is hidden, confirmed the draw order is
  genuinely random (not alphabetical/rating-based), played through Round 1 and
  watched Winners Round 2 and Losers Round 1 populate correctly with the right
  winners/losers.

## v0.12 — 2026-07-07

- **Teams tab.** New roster CRUD tab, mirroring the Players tab but simpler -
  a `Team` entity is just a name, no player membership tracked. Backed by
  `ITeamRepository`/`TeamRepository` (mirrors `IPlayerRepository`) and a
  `TeamEditorViewModel`/`Teams` collection on `MainWindowViewModel`.
- **Team entrants for Single/Double Elimination.** `TournamentEntrant.PlayerId`
  is now nullable and gained a nullable `TeamId`/`Team` pair, plus a
  `DisplayName` accessor (`Player?.FullName ?? Team?.Name ?? "TBD"`) so every
  display/status-message call site works for either kind of entrant.
  `Tournament.UsesTeams` (new bool) records the choice made at creation.
  `TournamentViewModel` gained a `UseTeams` toggle (only offered when
  `NewTournamentFormat` is Single/Double Elimination - `IsTeamEligibleFormat`)
  that swaps the entrant checklist between `EntrantCandidates` (Players) and
  a new `TeamCandidates`, and hides the meaningless "seed by rating" control
  for team tournaments. The post-creation "Add" picker on the Tournament tab
  does the same swap based on `ActiveTournament.UsesTeams`.
- The bracket engine itself (`BracketGenerationService`, `Match`,
  `BracketNode`) needed **zero changes** - it already worked purely off
  `TournamentEntrant.Id`/`SeedNumber`, never touching `.Player` directly.
  `SeedingService.GetRating` was refactored to take the whole
  `TournamentEntrant` (returns null for a Team entrant, same as an unrated
  Player) and its name tie-break now uses `DisplayName`, so a team tournament
  seeds gracefully by team name with no special-casing.
- Migration `AddTeams`: new `Teams` table, `TournamentEntrants.PlayerId` made
  nullable, new nullable `TournamentEntrants.TeamId` FK, new
  `Tournaments.UsesTeams` column (default false) - additive, no data loss.
- Tests: 81 pass (74 Core + 2 Data + 5 App, up from 79/2/... - added
  `AssignSeeds_TeamEntrantsHaveNoRatingAndSortByName` and
  `HasRating_IsFalseForTeamEntrants` to `SeedingServiceTests`). Verified
  end-to-end in the running app: added 4 teams on the new Teams tab, created a
  Single Elimination tournament with "Use Teams" checked (confirmed the
  Player checklist and "Seed by rating" hid, the Team checklist appeared),
  confirmed the resulting bracket showed the seeded team names (not "TBD"),
  confirmed the Tournament tab's post-creation row showed an "Add Team:"
  picker for that tournament, and confirmed switching format to Round Robin
  hides the "Use Teams" checkbox entirely (auto-unchecking it).

## v0.11 — 2026-07-07

- **Add players after creation.** A new "Add Player" picker on the Tournament
  tab lets the operator add a roster player to the selected tournament as
  long as no match/game has actually been played yet (`CanAddEntrant` in
  `TournamentViewModel`, checked per format: no match started for
  Single/Double Elimination/Round Robin, only buy-ins recorded for Ring Game,
  no games logged for Chip Tournament). For Single/Double Elimination and
  Round Robin, adding a player discards and regenerates the whole
  bracket/schedule from scratch (safe pre-play, since nothing has a result
  yet); double elimination still requires the new total to be a power of 2.
  Regeneration needed a new `ITournamentRepository.TrackRemoved` (mirrors
  `TrackNew`) to discard the old `BracketDetail`/`BracketNode`/`Match` rows.
- **Required table count.** Tournament Settings has a new "Number of tables"
  field, required and validated for every format except Ring Game; creating
  the tournament bulk-creates that many `Table` rows. Existing tournaments
  with zero tables (created before this version) simply can't start a match
  until a table is added via the existing "Add Table" button.
- **Match Start/Finish replaces Report.** `MatchStatus` gained an `InProgress`
  value (explicit enum values so existing persisted `Completed` rows don't
  shift). A match now goes `Scheduled` -[Start]-> `InProgress` -[Finish]->
  `Completed`. Starting requires a table to be assigned and blocks if that
  table already has another in-progress match on it. Finishing is what now
  finalizes the score/winner and advances the bracket (`RecordMatchResult`
  rejects finishing a match that was never started).
- **Per-match timer.** `Match.StartedAtUtc`/`FinishedAtUtc` (new nullable
  columns) drive a live "mm:ss" elapsed display while a match is in progress
  (ticked once a second by a `DispatcherTimer` in `TournamentStateService`)
  and a frozen "Finished in ..." readout once complete.
- **Two real bugs found and fixed while building this** (see NOTES.md for
  detail): (1) `TournamentRepository.TrackRemoved` used `DbContext.Remove()`,
  which cascades through the whole reachable navigation graph and threw a
  duplicate-tracked-entity exception given `GetByIdAsync`'s multiple Include
  paths onto the same Match/Player rows - fixed by using
  `Entry(entity).State = EntityState.Deleted` instead, which only marks the
  one entity. (2) `PoolTournamentDbContext`/`ITournamentRepository`/
  `IPlayerRepository` were registered `Scoped`, but the singleton
  `TournamentStateService` and transient `TournamentViewModel` each captured
  a *different* Scoped instance (a DI "captive dependency"), so any match
  mutation made through `State.ActiveTournament` silently failed to persist
  once saved through the ViewModel's own repository - fixed by registering
  all three as `Singleton`, matching how the app already behaves (one
  `_appScope` for its entire lifetime).
- Migration: `AddMatchTimingAndInProgressStatus` (adds `Matches.StartedAtUtc`,
  `Matches.FinishedAtUtc`; additive, no data loss).
- Tests: 79 pass (72 Core + 2 Data + 5 App, up from 77/2/4 - added
  `RecordMatchResult_ThrowsIfMatchNotStarted` and
  `TournamentEntrantAdditionTests.AddEntrant_ToSingleElimination_...`, the
  latter written against the real `TournamentViewModel`/`TournamentStateService`
  wired together, which is what actually caught the DI bug above). Verified
  end-to-end in the running app: created a Single Elimination tournament with
  a table count, confirmed the field hides for Ring Game; added a 5th player
  pre-play and watched the bracket regenerate with correct names/seeds;
  confirmed Start is blocked with no table and blocked on a table already in
  use; watched the live timer tick during an in-progress match; finished
  matches and confirmed the score, winner, bracket advancement, and "Finished
  in ..." duration all survive a full app restart (this is what surfaced and
  proved the fix for the DI bug - the original code showed the same result on
  screen either way, only a real restart revealed nothing had actually saved).

## v0.10.1 — 2026-07-07

- Reorganized the tournament UI. Renamed the "Settings" tab to "Tournament
  Settings" and moved the entire create-tournament form there - the game type
  (8/9/10-ball) picker, the format (single/double-elim, round robin, ring,
  chip) picker, seed-by-rating, the format-specific ring/chip setup fields, the
  entrant checklist + Refresh, and the Create button. The Tournament tab's left
  column now holds only the tournament picker list (fills the column) plus a
  hint that creation lives on Tournament Settings; the right side is unchanged
  (tables, bracket tree, standings, ring/chip operator controls). The moved
  form binds to the same shared TournamentViewModel (its tab content sets
  DataContext="{Binding Tournament}"), so a tournament created on Tournament
  Settings shows up in the Tournament tab's picker. The app Appearance note now
  sits below the create form on Tournament Settings. XAML-only relocation - no
  view-model or logic changes; the create command and all bindings are the
  byte-identical, already-verified ones from before the move. Tests unchanged
  (77 pass). Verified end-to-end in the running app: the Tournament Settings
  tab renders the full form with Game/Format/Seed combos populated from the VM
  enums and the entrant list loaded (proving the relocated DataContext resolves
  every binding), entrant checkboxes toggle, and the Tournament tab shows the
  picker (listing existing tournaments) with the hint and the tables/operator
  area.

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
