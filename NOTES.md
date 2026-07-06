# Notes

Running log of issues discovered during development and the fixes used.
Newest entries at the top.

## 2026-07-06 — Double elimination: a losers-bracket "receiving" round mixes two different propagation paths into one node

**Issue:** `PropagateWinner`'s existing slot-assignment rule (`PositionInRound % 2 == 0` → slot 1,
else slot 2) only works when a target node's two inputs arrive via the *same* path (two winners-
bracket siblings, or two losers-bracket siblings) — position parity is exactly what disambiguates
those two siblings. A losers-bracket "receiving" round breaks that assumption: one input is the
survivor advancing from the previous LB round (via `FeedsIntoWinnerNodeId`) and the other is a
freshly-dropped winners-bracket loser (via `FeedsIntoLoserNodeId`) - two *different* paths landing
on the same node, where each source's own `PositionInRound` is unrelated to the other's and can't
tell them apart (both are often position 0, colliding on the same computed slot).

**Fix:** Added explicit `FeedsIntoWinnerSlot`/`FeedsIntoLoserSlot` (nullable int) fields to
`BracketNode`, set unambiguously wherever double-elimination wiring is built (survivor always
slot 1 in a receiving round, dropped loser always slot 2; parity still used for genuine sibling
pairs like round-to-round winners-bracket advancement and losers-bracket consolidation rounds).
`PropagateWinner`/`PropagateLoser` prefer the explicit slot and fall back to the old parity rule
when it's null, so single-elimination's existing wiring (which never sets these fields) is
unaffected.

## 2026-07-06 — Double-elimination bracket display needed grouping by (Side, RoundNumber), not RoundNumber alone

**Issue:** `TournamentStateService.RebuildRounds` grouped bracket nodes by `RoundNumber` alone to
build the UI's left-to-right round columns. That's fine for single elimination (one continuous
round sequence) but wrong for double elimination, where the winners bracket, losers bracket, and
Grand Final all reuse overlapping round numbers (e.g. WB round 2 and LB round 2 both exist) - a
plain `RoundNumber` grouping would have merged unrelated matches from different bracket sides into
the same visual column.

**Fix:** Group by the tuple `(Side, RoundNumber)`, ordered by an explicit side rank (Winners,
then Losers, then GrandFinal) before round number, and title each group with a side prefix
("WB Round 1", "LB Final", "Grand Final", "Bracket Reset") whenever `BracketDetail.IsDoubleElimination`
is true. Single-elimination brackets keep their original unprefixed titles ("Round 1", "Semifinals",
"Final") since `IsDoubleElimination` is false for them.

## 2026-07-06 — DisplayWindow: local property values silently defeated Style triggers

**Issue:** The read-only display window's winner-highlight (gold + bold) and "table open"
fallback text never showed up, even though the triggers looked correct. Root cause: WPF
dependency-property precedence - a value set directly on an element (`Foreground="White"`, or
`<TextBlock.Text><MultiBinding .../></TextBlock.Text>`) is a **local value**, and local values
always win over anything coming from a `Style`, including a `Style.Trigger`'s `Setter` for that
same property. Setting `Foreground="White"` as a local attribute meant the trigger's
`Foreground="Gold"` setter could never take effect no matter what condition it checked.

**Fix:** For the winner highlight, moved the default `Foreground="White"` into the `Style`
itself as a base `Setter` (not a local attribute), so the `DataTrigger`'s override can actually
compete with it. For the "Now Playing" fallback text, abandoned the trigger approach entirely
and instead computed the display string (`"Open"` vs `"{p1} vs {p2}"`) as a plain C# property
on `TableAssignmentRow` - simpler and avoids the precedence trap altogether. Lesson for this
codebase: prefer computing conditional display strings in the ViewModel over `DataTrigger`
`Setter`s targeting a property that's also set as a local value anywhere on the same element.

**Also:** the trigger was originally keyed off `IsComplete` (match finished) rather than which
player actually won, which would have bolded Player1's line even when Player2 won. Added
`IsPlayer1Winner`/`IsPlayer2Winner` to `MatchRowViewModel` so each line's trigger checks the
right thing.

## 2026-07-06 — New Match entities created mid-tournament failed with "FOREIGN KEY constraint failed"

**Issue:** Reporting a real (non-bye) match's score threw `DbUpdateException` / SQLite error 19
whenever it caused a new next-round `Match` to materialize (e.g. when a bye-advanced player's
semifinal opponent is finally decided). Root cause: EF Core's change tracker cannot tell a
brand-new entity from a pre-existing one purely by reachability when it's attached to an
*already-tracked* aggregate (our long-lived `Tournament` graph, loaded once and kept tracked for
the app's lifetime) - because our entities use client-generated GUID keys (`Guid.NewGuid()` in
the property initializer), a "non-default key + discovered only via navigation fixup, not via an
explicit `Add()`" entity gets marked `Modified`/`Unchanged` instead of `Added`. EF then emits a
no-op UPDATE (0 rows affected) instead of an INSERT, so when a sibling entity's FK (here,
`BracketNode.MatchId`) points at that phantom row, the FK constraint fails. This did NOT show up
at tournament-creation time because that path calls `_dbContext.Tournaments.Add(tournament)` on
a still-untracked root, which makes EF walk the whole graph and correctly mark everything Added
regardless of key values - the bug only bites when mutating an already-tracked, already-loaded
aggregate later.

**Fix:** `BracketGenerationService.RecordMatchResult` now returns the newly-materialized `Match`
(if any) instead of just mutating in place. Added `ITournamentRepository.TrackNew(object)`
(`_dbContext.Add(entity)`) so the Data-layer explicitly marks it `Added` before
`SaveChangesAsync()`. Same fix applied to `TournamentViewModel.AddTableAsync`, which had the
identical bug (silently never persisting new tables - no FK to trip over, so it failed quietly
instead of throwing). General rule for this codebase: any time code adds a new entity to a
collection navigation of an *already-tracked* entity (rather than to a fresh, unattached
aggregate later passed to `Add()`), it must be explicitly tracked via `TrackNew` before saving.

## 2026-07-06 — Bracket advancement treated a still-pending round-2+ slot as a bye

**Issue:** `BracketGenerationService`'s single "materialize match" helper treated *any* node
with an empty second slot as a permanent bye and auto-completed it. That's only true for
round-1 nodes (where an empty slot means the opponent seed doesn't exist). For round-2+ nodes,
an empty second slot just means the other semifinal hasn't been played yet - but the shared
helper auto-completed those too, cascading extra "byes" and inflating the bye count (caught by
`Core.Tests`: e.g. a 6-entrant bracket produced 4 byes instead of the expected 2).

**Fix:** Split the logic into `MaterializeRound1Match` (always creates the match immediately -
round 1 has complete information upfront) and `TryMaterializeAdvancedMatch` (only creates a
`Scheduled` match once *both* slots are actually filled; never auto-completes as a bye).

## 2026-07-06 — Nullable int TextBox bindings showed a red validation border when empty

**Issue:** `TextBox.Text` bound directly to a nullable `int` property (Fargo
Rate, APA 8-Ball/9-Ball skill) showed a red validation error border whenever
the field was empty, even though nothing was actually wrong — WPF's default
binding conversion fails to treat an empty string as `null` for `Nullable<T>`
targets.

**Fix:** Added `TargetNullValue=''` to those bindings in `MainWindow.xaml` so
an empty box round-trips to `null` instead of a failed conversion.

## 2026-07-06 — Editor panel didn't refresh immediately after Deactivate

**Issue:** Clicking "Deactivate" correctly persisted `IsActive = false` to
the database and disabled the button (`CanDeactivatePlayer` re-evaluated
correctly), but the "Active" checkbox in the editor panel kept showing
checked until the player was re-selected or the app was restarted —
`DeactivatePlayerAsync` mutated the entity and reloaded the grid, but never
re-pointed `SelectedPlayer` at the reloaded instance, so `Editor.LoadFrom`
never re-ran.

**Fix:** Mirrored `SavePlayerAsync`'s pattern in `DeactivatePlayerAsync`:
after `LoadPlayersAsync()`, re-select the same player by `Id` from the
freshly loaded collection, which re-triggers `OnSelectedPlayerChanged` and
syncs the editor.

<!--
Entry format:

## YYYY-MM-DD — Short title of the issue

**Issue:** What went wrong / what was discovered.

**Fix:** What was changed to resolve it.
-->
