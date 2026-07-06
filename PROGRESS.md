# Progress

Tracks what's changed and what's next during development. Newest entries at
the top of each section.

## Current status

v0.2 complete: single-elimination tournaments run end-to-end (seed by rating,
generate bracket with correct bye handling, enter scores, auto-advance
winners, assign tables) against a local SQLite database, verified by
actually running the app and playing a 6-entrant bracket to a champion.

## Next steps

- [ ] 0.3 — Display window + live sync: `ITournamentStateService`/DI
      composition for a read-only second window (table assignments + live
      bracket/scores), reused by every later format milestone.

## Change log

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
