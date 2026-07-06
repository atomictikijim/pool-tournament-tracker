---
name: add-migration
description: Add or manage an EF Core (SQLite) database migration for PoolTournamentManager. Use whenever a Core entity, enum, or DbContext mapping changes shape (new/removed/renamed column, table, key, relationship) — the schema change is not real until a migration exists. Encodes the exact dotnet ef command for this repo's project layout and the change-tracker gotcha that makes mid-tournament inserts fail.
---

# Adding an EF Core migration

Persistence is EF Core + SQLite. The `DbContext` is
[PoolTournamentDbContext.cs](src/PoolTournamentManager.Data/Persistence/PoolTournamentDbContext.cs),
and an `IDesignTimeDbContextFactory` (`PoolTournamentDbContextDesignTimeFactory`) lives in the
same **Data** project, so EF design-time tooling targets that project for both `--project` and
`--startup-project`.

## When you need a migration

Any change to an entity in `src/PoolTournamentManager.Core/Entities`, an enum stored on one, or
a mapping in the `DbContext` that alters the SCHEMA: new/removed/renamed property→column, new
table, changed key/relationship, nullability, etc. Behavior-only changes need none. Recent
examples: `AddRoundRobinRoundNumber`, `RemoveUsaplRating`, `RemovePlayerIsActive`.

## The command

Run from the solution root (install the tool once with `dotnet tool install --global dotnet-ef`
if `dotnet ef` isn't found):

```powershell
dotnet ef migrations add <PascalCaseName> `
  --project src/PoolTournamentManager.Data `
  --startup-project src/PoolTournamentManager.Data
```

Name it after the change (`AddX`, `RemoveX`, `RenameXToY`) to match the existing history in
`src/PoolTournamentManager.Data/Migrations/`. This generates the migration `.cs`, its
`.Designer.cs`, and updates `PoolTournamentDbContextModelSnapshot.cs` — commit all three.

## After generating

1. **Read the generated `Up`/`Down`.** SQLite can't drop/alter columns natively, so EF rebuilds
   the table (create-temp → copy → drop → rename). Confirm no data you care about is silently
   dropped, and that `Down` is a real inverse.
2. **Do NOT apply it by hand.** [App.xaml.cs](src/PoolTournamentManager.App/App.xaml.cs) calls
   `dbContext.Database.Migrate()` on startup, so just running the app applies pending migrations
   to the live DB at `%LOCALAPPDATA%\PoolTournamentManager\tournaments.db`.
3. **Verify against the real dev DB.** Every schema change in the change log was verified by
   running the app against the existing DB (which already holds real players/tournaments) and
   confirming existing rows still load. Copy the DB aside first (with its `-wal`/`-shm` sidecars
   — see the run-app skill) if the change is destructive.
4. Rebuild: `dotnet build PoolTournamentManager.sln`.

## Runtime gotcha this schema layer imposes (from NOTES.md)

Entities use client-generated GUID keys (`Guid.NewGuid()` initializers). When you add a NEW
entity to a collection navigation of an **already-tracked** aggregate (e.g. the long-lived
`Tournament` graph) and then `SaveChangesAsync`, EF marks it `Modified`/`Unchanged` (not `Added`)
and emits a no-op UPDATE instead of an INSERT — which then trips `FOREIGN KEY constraint failed`
when a sibling FK points at the phantom row, or fails silently when there's no FK.

Rule: any code that attaches a new entity to an already-tracked aggregate must explicitly call
`ITournamentRepository.TrackNew(entity)` before saving. This does NOT bite at
tournament-creation time (that path calls `.Add()` on a still-untracked root, so EF walks the
whole graph correctly) — only when mutating an aggregate loaded earlier and kept tracked.
