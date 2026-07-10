# PoolTournamentManager

A Windows desktop application for running pool tournaments at a bar, club, or league
night — from building a player/team roster through running the event and displaying
live results on a second screen.

## What it does

- **Player and Team rosters.** Keep a persistent roster of players (name, contact
  info, Fargo/TAP/APA 8-ball/APA 9-ball ratings) or teams (name, division, and the
  pool hall they play out of), reused across tournaments. Add and edit records in a
  focused pop-up editor, and delete one or many at a time (with a confirmation
  prompt; anyone still entered in a tournament is protected from deletion).
- **Five tournament formats**, selectable per event:
  - **Single Elimination** — standard seeded knockout bracket, with byes for
    non-power-of-2 entrant counts.
  - **Double Elimination** — winners/losers brackets with a grand final and
    bracket-reset rematch if the losers'-bracket champion wins it; also supports
    non-power-of-2 entrant counts via first-round byes.
  - **Modified Single Elimination** — APA's format: every entrant is guaranteed at
    least two matches before elimination is possible, run in pods of up to 8 (entrants
    split as evenly as possible, byes fill short pods) that feed a single-elimination
    semifinal/final stage. Faster than double elimination; any field of 8 or more.
  - **Round Robin** — every entrant plays every other entrant once; standings ranked
    by wins, then head-to-head, then point differential, then games-won %.
  - **Ring Game** — a rotation-order money game (9-ball rules): players buy in, shoot
    in a fixed order, and cash out individually as the session runs.
  - **Chip Tournament** — a buy-in "lives" tournament: every player starts with the
    same chip count, loses a chip on a loss, and is eliminated at zero; last player
    standing wins, with configurable 1st/2nd/3rd payouts. Players are shuffled and
    seated at tables in rotation (winner stays, next player up takes the loser's
    seat), with a live table board, Next Up queue, and per-player win-rate tracking.
- **Teams as entrants.** Single, Double, and Modified Single Elimination can be run
  with Teams instead of individual Players.
- **Entry fees and prize payouts.** Every format except Ring Game can charge a
  per-entrant entry fee, take a percentage host cut, and split the remaining prize
  pool across any number of finishing places by percentage - shown live on the
  Tournament tab and Display window as entrants are decided.
- **Live match operation.** Assign tables, start/finish matches with a live elapsed
  timer, and report scores — the bracket, standings, or ledger update immediately.
- **Not Started tournaments can be reshuffled or edited.** A bracket/Round Robin
  tournament sits at Not Started until its first match starts - until then, its
  bracket can be reshuffled (a fresh 100% random draw) or its settings edited and
  saved in place, both from the Tournament tab. Once a match starts, both lock in.
- **Delete a tournament** you no longer need from the Tournament tab (with a
  confirmation prompt); its bracket/matches/tables are removed while the players and
  teams stay on their rosters. The tournament list can also be filtered by status
  (All / Not Started / In Progress / Completed).
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

## Building an installer

To hand the app to someone else (or install it on another Windows computer) as a
proper `Setup.exe` — Start Menu shortcut, uninstaller, no .NET runtime required on
the target machine — run:

```powershell
./build-installer.ps1
```

This publishes a self-contained `win-x64` build and compiles it into
`installer/output/PoolTournamentManager-Setup-v<version>.exe` via [Inno
Setup](https://jrsoftware.org/isinfo.php) (`installer/PoolTournamentManager.iss`).
Install Inno Setup once first if it isn't already on this machine:

```powershell
winget install --id JRSoftware.InnoSetup -e
```

Uninstalling the app removes the installed program files only — it never touches
`%LOCALAPPDATA%\PoolTournamentManager`, so a reinstall picks up existing tournament
data untouched.

## Tests

```powershell
dotnet test PoolTournamentManager.sln
```

Core tournament logic (bracket generation for all three elimination formats,
seeding, round-robin scheduling/standings, ring game, chip tournament) is covered by
fast, UI-free unit tests in `tests/PoolTournamentManager.Core.Tests`.

## License

Copyright © 2026 James Milne.

PoolTournamentManager is free software: you can redistribute it and/or modify it
under the terms of the **GNU General Public License** as published by the Free
Software Foundation, either **version 3** of the License, or (at your option) any
later version. It is distributed in the hope that it will be useful, but WITHOUT ANY
WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A
PARTICULAR PURPOSE. See the [LICENSE](LICENSE) file for the full text, or
<https://www.gnu.org/licenses/>.

In short: you may use, modify, and sell it, provided you keep the original copyright
and attribution notices intact and release any modified/derivative versions under the
same GPL-3.0 (or later) terms.

### Commercial licensing

The GPL requires that anything built on this project also be shared under the GPL. If
you want to incorporate PoolTournamentManager into a proprietary/closed-source product,
or otherwise use it on terms that are not compatible with the GPL, a separate
**commercial license is available** — contact James Milne (<james.milne@prolocity.com>)
to arrange one.

## More documentation

- [FUNCTIONS.md](FUNCTIONS.md) — end-user instruction manual for every screen and
  feature.
- [PROGRESS.md](PROGRESS.md) — development change log, version by version.
- [NOTES.md](NOTES.md) — technical gotchas and lessons learned during development.
- [CLAUDE.md](CLAUDE.md) — project conventions and versioning/commit policy.
