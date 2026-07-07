---
name: release
description: Ship a version bump for PoolTournamentManager — pick the correct 0.<major>.<ui> number per the CLAUDE.md policy, update PROGRESS.md/NOTES.md, build/test green, then commit and tag. Use when a feature or UI change is complete and ready to record, or when asked to "bump the version", "commit this version", "cut a release", or "tag it".
---

# Cutting a version for PoolTournamentManager

Follows the versioning & commit policy in CLAUDE.md. Pre-1.0 format is **`0.<major>.<ui>`**.

## 1. Pick the number

- **Major functionality** (new feature/workflow, significant logic change) → bump the MIDDLE
  number, reset the UI counter: latest is `0.6.2`, so the next feature is `0.7`.
- **UI-only** (layout, styling, cosmetic, no behavior change) → bump the THIRD number under the
  current major: after `0.7` the first UI tweak is `0.7.1`.
- **Trivial** (typo, comment, formatting) → NO standalone bump; fold into the next real commit.

Check `git log --oneline -1` and the top of PROGRESS.md to confirm the current number before choosing.

## 2. Confirm green

```powershell
dotnet build PoolTournamentManager.sln
dotnet test PoolTournamentManager.sln
```

Never commit a change that doesn't build. For anything with runtime surface, also verify
end-to-end in the real app (see the run-app skill) — every change-log entry to date records a
manual end-to-end verification, and the commit message should be able to honestly claim the same.

## 3. Update the docs (part of the same commit)

- **PROGRESS.md** — update "Current status", tick/append "Next steps", and add a change-log entry
  at the TOP of the log in the existing style: what changed, why if non-obvious, test count delta,
  and the manual verification performed.
- **NOTES.md** — if any real bug or non-obvious gotcha was found/fixed, add an entry at the top
  (`## YYYY-MM-DD — title`, then `**Issue:**` / `**Fix:**`). Use today's date from context.

## 4. Commit, tag, and push

Version-bump commits (routine feature or UI-only) are pre-authorized in CLAUDE.md — commit, tag,
AND push automatically, no need to ask. This authorization covers the routine commit + tag +
`git push`/`git push --tags` below; it does NOT extend to force-push or history rewrites.

```powershell
git add -A
git commit   # message form:  v0.X[.Y]: <short description>
git tag v0.X[.Y]
git push
git push --tags
```

End the commit message body with the required trailer:

```
Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
```

Pushing is now part of the routine: after every successful version bump, push the commit and the
tag as the final step — don't leave verified work unpushed.
