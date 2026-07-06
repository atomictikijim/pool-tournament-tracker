# Progress

Tracks what's changed and what's next during development. Newest entries at
the top of each section.

## Current status

v0.1 complete: player roster CRUD is working end-to-end (add, edit, persist,
deactivate) against a local SQLite database, verified by running the app and
driving the UI directly, not just build/test.

## Next steps

- [ ] 0.2 — Single-elimination bracket + admin window: `Tournament`,
      `TournamentEntrant`, `Table`, `Match`, `BracketDetail`/`BracketNode`,
      rating-based seeding, single-elim generation, score entry, manual
      table assignment.

## Change log

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
