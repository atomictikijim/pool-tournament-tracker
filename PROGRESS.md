# Progress

Tracks what's changed and what's next during development. Newest entries at
the top of each section.

## Current status

v0.4 complete: double-elimination tournaments (losers bracket + Grand Final +
single bracket-reset rematch) are fully playable end-to-end, reusing 0.2/0.3's
UI, live-sync, and Display window with no separate code paths for rendering.

## Next steps

- [ ] 0.5 — Round robin: circle-method scheduler, standings + tiebreaks.

## Change log

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
