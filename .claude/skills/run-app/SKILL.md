---
name: run-app
description: Build, launch, and drive the PoolTournamentManager WPF desktop app to verify a change works end-to-end. Use whenever asked to run/start/screenshot the app, or to confirm a change works in the real UI (not just tests). Encodes this app's build commands, the LOCALAPPDATA database location, and the UI-automation traps documented in NOTES.md.
---

# Running PoolTournamentManager

WPF desktop app, `net8.0-windows`, WinExe. MVVM (CommunityToolkit.Mvvm), DI wired in
[App.xaml.cs](src/PoolTournamentManager.App/App.xaml.cs), SQLite via EF Core.

## Build and launch

From the solution root:

```powershell
dotnet build PoolTournamentManager.sln          # always build first; never run a broken build
dotnet run --project src/PoolTournamentManager.App
```

The exe also lands at `src/PoolTournamentManager.App/bin/Debug/net8.0-windows/PoolTournamentManager.App.exe`
if you need to launch it directly (e.g. background it and then drive it).

On startup `App.xaml.cs` calls `dbContext.Database.Migrate()`, so the live DB is
migrated to head automatically — you do NOT need to apply migrations by hand before running.

## Where the data lives

`%LOCALAPPDATA%\PoolTournamentManager\tournaments.db` (see
[PoolTournamentDbContextFactory.cs](src/PoolTournamentManager.Data/Persistence/PoolTournamentDbContextFactory.cs)).

- To inspect or copy the DB, you MUST copy the `-wal` and `-shm` sidecar files alongside
  `tournaments.db` — a plain copy of just the `.db` looks stale (recent committed writes
  live in the WAL). This exact mistake wasted time in a prior session (see NOTES.md).
- To test first-run behavior, move/rename the whole `PoolTournamentManager` folder aside;
  the app recreates it. Restore it afterward to keep the user's real data.
- Never delete the user's real dev DB to "reset" — copy it aside first.

## Driving the UI to verify (READ THIS before automating clicks)

Prior sessions hit real, time-wasting traps here — all documented in NOTES.md:

1. **TabControl content is invisible to UI Automation.** After the custom `TabControl`
   template, `AutomationElement.FindAll` returns ZERO elements for anything inside the
   selected tab's page — buttons, text, all of it — even though it renders and is
   mouse/keyboard-interactable. The content is real (confirm via screenshot). Fall back to
   coordinate-based mouse clicks, not automation-element lookups, for anything on a tab page.

2. **"Report Result" first click after typing scores often no-ops.** A synthetic click
   immediately after typing both scores routinely does nothing on the FIRST click; a second
   click always works. Prefer a genuine slower pattern (pause between typing and clicking).
   If you see a no-op, retry the click once before concluding anything is broken. Do NOT
   spam-click: `[RelayCommand]`s here don't guard against concurrent execution, and a
   too-fast retry can race a second `SaveChangesAsync` on the non-thread-safe `DbContext`
   (this produced a scary-but-spurious FOREIGN KEY crash once — it was not a real defect).

3. **Verify behavior, not just appearance.** For every version bump the change log records
   an end-to-end manual verification. Match that bar: actually create/play a tournament,
   report results, and confirm the observed state change — screenshots plus a described
   click sequence.

## Tests

```powershell
dotnet test PoolTournamentManager.sln
```

Core logic (bracket generation, seeding, round-robin scheduling/standings, validation) is
unit-tested in `tests/PoolTournamentManager.Core.Tests` with no UI. Add/extend those tests
for any algorithm change — they are the fast feedback loop; UI automation is the slow,
trap-laden one.
