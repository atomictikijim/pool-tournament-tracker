# Notes

Running log of issues discovered during development and the fixes used.
Newest entries at the top.

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
