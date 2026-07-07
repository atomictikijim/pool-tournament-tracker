# PoolTournamentManager

A Windows desktop application for running pool tournaments at a bar, club, or league
night — from building a player/team roster through running the event and displaying
live results on a second screen.

## What it does

- **Player and Team rosters.** Keep a persistent roster of players (name, contact
  info, Fargo/TAP/APA 8-ball/APA 9-ball ratings) or name-only teams, reused across
  tournaments.
- **Five tournament formats**, selectable per event:
  - **Single Elimination** — standard seeded knockout bracket, with byes for
    non-power-of-2 entrant counts.
  - **Double Elimination** — winners/losers brackets with a grand final and
    bracket-reset rematch if the losers'-bracket champion wins it.
  - **Modified Single Elimination** — APA's format: every entrant is guaranteed at
    least two matches before elimination is possible, run in 8-entrant pods that
    feed a single-elimination semifinal/final stage. Faster than double elimination.
  - **Round Robin** — every entrant plays every other entrant once; standings ranked
    by wins, then head-to-head, then point differential, then games-won %.
  - **Ring Game** — a rotation-order money game (9-ball rules): players buy in, shoot
    in a fixed order, and cash out individually as the session runs.
  - **Chip Tournament** — a buy-in "lives" tournament: every player starts with the
    same chip count, loses a chip on a loss, and is eliminated at zero; last player
    standing wins, with configurable 1st/2nd/3rd payouts.
- **Teams as entrants.** Single, Double, and Modified Single Elimination can be run
  with Teams instead of individual Players.
- **Entry fees and prize payouts.** Every format except Ring Game can charge a
  per-entrant entry fee, take a percentage host cut, and split the remaining prize
  pool across any number of finishing places by percentage - shown live on the
  Tournament tab and Display window as entrants are decided.
- **Live match operation.** Assign tables, start/finish matches with a live elapsed
  timer, and report scores — the bracket, standings, or ledger update immediately.
- **Second-screen Display window.** A read-only, projector-friendly window mirroring
  the live bracket/standings/rotation board, for the room to watch.
- **Follows Windows light/dark mode** automatically, live, with no in-app settings.

See [FUNCTIONS.md](FUNCTIONS.md) for a full walkthrough of every screen and feature.

## Project layout

- `PoolTournamentManager.sln` — solution file
- `src/PoolTournamentManager.Core` — domain entities, enums, and pure business logic
  (bracket generation, seeding, round-robin scheduling/standings, ring game, chip
  tournament) — no UI or database dependencies, fully unit-testable
- `src/PoolTournamentManager.Data` — EF Core + SQLite persistence and migrations
- `src/PoolTournamentManager.App` — the WPF desktop application (MVVM via
  CommunityToolkit.Mvvm)
- `tests/` — one test project per `src/` project

## Running it

Requires the .NET 8 SDK.

```powershell
dotnet build PoolTournamentManager.sln
dotnet run --project src/PoolTournamentManager.App
```

Tournament data is stored in a local SQLite database under
`%LOCALAPPDATA%\PoolTournamentManager\tournaments.db`, created and migrated
automatically on first launch — no setup required.

## Tests

```powershell
dotnet test PoolTournamentManager.sln
```

Core tournament logic (bracket generation for all three elimination formats,
seeding, round-robin scheduling/standings, ring game, chip tournament) is covered by
fast, UI-free unit tests in `tests/PoolTournamentManager.Core.Tests`.

## More documentation

- [FUNCTIONS.md](FUNCTIONS.md) — end-user instruction manual for every screen and
  feature.
- [PROGRESS.md](PROGRESS.md) — development change log, version by version.
- [NOTES.md](NOTES.md) — technical gotchas and lessons learned during development.
- [CLAUDE.md](CLAUDE.md) — project conventions and versioning/commit policy.
