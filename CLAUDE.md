# PoolTournamentManager

A C# Windows desktop executable application for managing pool tournaments.

## Project Layout

- `PoolTournamentManager.sln` — solution file
- `src/` — application source (currently empty scaffolding)
- `tests/` — test project(s) (currently empty scaffolding)

This project is not yet a git repository. Once it is initialized, the versioning
and commit policy below takes effect.

## C# / Windows Executable Best Practices

- Target an explicit TFM in every `.csproj` (e.g. `net8.0-windows`) — avoid floating/implicit TFMs.
- Separate UI from logic: keep WinForms/WPF code-behind thin; put business logic in
  services/view-models that can be unit tested without spinning up a UI.
- Enable nullable reference types (`<Nullable>enable</Nullable>`) on new projects.
- Install a global exception handler (`Application.ThreadException`,
  `AppDomain.CurrentDomain.UnhandledException`) so the app fails with a logged error
  dialog instead of a silent crash or raw stack trace.
- Store tournament/user data under `%APPDATA%` or `%LOCALAPPDATA%` (or a local
  SQLite/LiteDB file there) — never write app data next to the executable, since
  `Program Files` installs are typically read-only for standard users.
- Build and run `dotnet build` (and `dotnet test` once tests exist) from the solution
  root before every commit — never commit a change that doesn't build.
- Prefer `dotnet publish -p:PublishSingleFile=true -p:SelfContained=true` for
  distributable builds so end users don't need to separately install a matching
  .NET runtime.
- Keep secrets/connection strings out of source; use user secrets or a local
  untracked config file for anything environment-specific.

## Versioning & Commit Policy

Version format while pre-1.0: **`0.<major>.<ui>`**

- **Major functionality updates** (new features, new workflows, significant logic
  changes) bump the middle number: `0.1` → `0.2` → `0.3` ... The first functional
  version is `0.1`.
- **UI-only updates** (layout, styling, cosmetic/no-behavior-change tweaks) bump the
  third number under the current major, starting at `0.1.1`: `0.1.1` → `0.1.2` ...
  When a new major functionality version ships, the UI counter resets (e.g. `0.2`,
  then its first UI tweak is `0.2.1`).
- Trivial fixes (typos, comments, formatting) that are neither new functionality nor
  a UI change do not get their own version bump — fold them into the next
  functionality or UI commit.

**Auto-commit-and-push authorization:** completing and verifying a major
functionality change or a UI-only change is pre-authorized to be committed,
tagged, AND pushed automatically — do not stop to ask for confirmation on these
version-bump commits specifically. After every successful update, commit and
push (including tags) as the final step; do not leave verified work unpushed.
This authorization covers the routine `git push` / `git push --tags` of
version-bump commits to the normal upstream branch only; it does NOT cover
force-pushes, history rewrites, or any other git operation.

Workflow for every version bump:

1. Build the solution (and run tests, once they exist) and confirm it's green.
2. Commit with a message in the form `v0.X[.Y]: <short description>`.
3. Tag the commit with the same version string (e.g. `git tag v0.2`).
4. Push the commit and the tag (`git push && git push --tags`).
