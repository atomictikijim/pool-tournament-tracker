# Notes

Running log of issues discovered during development and the fixes used.
Newest entries at the top.

## 2026-07-08 — A merged `ResourceDictionary` file can't `StaticResource` a key its parent declares

**Issue:** Adding a rating column to `Themes/BracketTemplates.xaml`'s `PlayerLineTemplate` needed
to hide it with `Visibility="{Binding HasRating, Converter={StaticResource BoolToVisibilityConverter}}"`.
`BoolToVisibilityConverter` is declared directly in `App.xaml`'s own `<Application.Resources>`,
which merges in `BracketTemplates.xaml` via `<ResourceDictionary Source="..."/>`. That merge only
goes one direction: the parent (`App.xaml`) can see everything in the child dictionary it merges
in, but the child dictionary's own XAML has no visibility into keys declared by whichever parent
happens to merge it in - `StaticResource` lookup for content authored inside `BracketTemplates.xaml`
only searches that file's own resources (and anything *it* merges in), so the key doesn't resolve.

**Fix:** Declare a second `<BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter" />`
directly inside `BracketTemplates.xaml` itself. Duplicate keys across separate dictionaries don't
conflict - each dictionary resolves its own `StaticResource`s from its own scope first, so this
file's `DataTemplate`s find the local copy while the rest of the app keeps using App.xaml's.
General lesson: any shared/reusable `ResourceDictionary` file that references a converter/brush
also used app-wide should declare that resource locally (or merge it in explicitly), never assume
a `StaticResource` will find something declared only by whatever parent dictionary happens to
include the file.

## 2026-07-08 — TabItem: `ContentTemplate` Setter silently replaces the tab PAGE content, not the header text

**Issue:** Restyling `TabItem` in `Themes/Generic.xaml` to look like a button needed the header's
plain-string `Content` to get a real local `Foreground` (same trap as Button's `ContentTemplate` -
see the 2026-07-06 entry below and NOTES.md's precedence traps generally). The header is presented
inside the `TabItem`'s `ControlTemplate` via `<ContentPresenter ContentSource="Header" .../>`. Per
WPF's `ContentSource` convention, that specific `ContentPresenter` reads `HeaderTemplate` (and
`HeaderTemplateSelector`/`HeaderStringFormat`), NOT `ContentTemplate` - `ContentTemplate` is a
completely different property, still governing the TabItem's own `Content` (the tab *page*).
Setting a `Style` `Setter Property="ContentTemplate"` on `TabItem` therefore did nothing to the
header, and instead got picked up by `TabControl`'s own template (`<ContentPresenter
ContentSource="SelectedContent" />`), which auto-binds `ContentTemplate` to the *selected* TabItem's
`ContentTemplate`. The result: every tab page's real content (a `ScrollViewer` wrapping the actual
UI) got replaced by the new `DataTemplate`'s `TextBlock Text="{Binding}"` - which rendered as the
literal `ToString()` of whatever object `{Binding}` resolved to, e.g. "System.Windows.Controls.
ScrollViewer" - visible on every tab, not just the one being restyled, since the Style applies to
all `TabItem`s.

**Fix:** Use `Setter Property="HeaderTemplate"` instead of `ContentTemplate` on the `TabItem`
`Style`. General lesson: whenever a `ControlTemplate` presents a sub-part via
`ContentSource="X"`, any Style Setter meant to affect that sub-part must target `XTemplate`/
`XTemplateSelector`/`XStringFormat`, not the plain `Content`/`ContentTemplate` properties - those
remain wired to the control's own (different) `Content` property and can silently leak into
whatever else reads it (here, the parent `TabControl`'s `SelectedContentTemplate`).

## 2026-07-07 — Elimination-bracket prize payouts beyond 1st/2nd are a deliberate approximation, not exact placement

**Issue:** Building `PrizePayoutService` for the new entry-fee/prize-payout feature needs a
"finishing place" for every entrant. Round Robin and Chip Tournament already compute one exactly
(a full strict order / an elimination-sequence number - see `RoundRobinStandingsService`/
`ChipGameService.ComputeStandings`). Elimination brackets (Single/Double/Modified) have **no such
concept anywhere in the codebase** beyond the champion (the final's winner) - grepping for
"RunnerUp"/"Placement" turns up nothing. A bracket only ever *decides* a strict order for 1st and
2nd; every earlier round eliminates multiple entrants with no inherent ranking between them (e.g.
both semifinal losers are simply "eliminated in the semifinals"). Building exact bracket-depth
placement would require bespoke graph traversal per `BracketKind` - and, for Modified Single
Elimination specifically, per-pod traversal too, since pod-eliminated entrants never enter the
shared Final-side bracket at all - with no existing precedent to build on.

**Fix (a scope decision, not a bug fix):** 1st/2nd are computed exactly (find the deciding match
per `BracketKind` - the top Winners/Final-side node, or the Grand Final preferring its reset match
if one was played). Everyone else is ranked by match win/loss record (wins desc, losses asc, name),
**excluding bye matches** from the count so a round-1 bye doesn't inflate one semifinalist's win
count relative to another who played every round. Entrants with an identical record tie and split
the combined payout for the place range they occupy evenly. This is a defensible, well-tested
approximation (see `PrizePayoutServiceTests` - it produces exact results for every case that has
one, e.g. Double Elimination's N=4 bracket has zero ties since each round eliminates exactly one
person), but it is *not* the same thing as true bracket-depth seeding placement. Anyone tempted to
"fix" a payout split that looks slightly off for a large/irregular bracket should know this is
working as designed, not a latent bug - see PROGRESS.md's Next Steps for the same call-out.

## 2026-07-07 — Modified Single Elimination: two bugs, one from a test, one only visible in the UI

**Issue 1:** `BracketGenerationService.GenerateModifiedSingleElimination` feeds every pod's 2
"Final Four" winners into a shared cross-pod single-elimination stage via the existing
`BuildWinnersRounds2AndUp` helper. That helper's target-slot inference falls back to the
*completed* node's own `PositionInRound % 2` when no explicit `FeedsIntoWinnerSlot` is set - a
convention that's only unambiguous when the round being fed from was built with clean, freshly
assigned 0-based positions (true for every other caller of this helper). The interleaved
cross-pod list instead carries each node's *pod-relative* `PositionInRound` (e.g. two different
pods' "lane 0" rep can both be even), so two reps from different pods could both resolve to slot
1, and slot 2 of the target semifinal node never got filled - it silently never materialized. A
16-entrant playthrough test (`PlayThrough16Entrants_...`) caught this immediately as a match-count
assertion off by exactly 2 (the missing semifinal matches); an 8-entrant (single-pod) test could
never have caught it, since a single pod's 2 reps happen to fall on parity-compatible positions by
coincidence.

**Fix:** After wiring the cross-pod rounds, explicitly set
`interleaved[i].FeedsIntoWinnerSlot = i % 2 == 0 ? 1 : 2` from the interleaved list's own index,
overriding the ambiguous parity fallback. General lesson: `BracketNode`'s own doc comment already
warns that the slot-parity fallback requires both inputs to arrive via "the same path" with
naturally alternating positions - any time a round is built by merging nodes from otherwise
independent numbering schemes (not just double elimination's winner/loser merge), assume the
fallback is wrong and set the slot explicitly.

**Issue 2:** After the generator was fully correct (and covered by 82 passing Core tests), the
bracket still didn't render at all when creating a Modified Single Elimination tournament in the
real app - `TournamentViewModel.RebuildBracket()` and `DisplayWindowViewModel`'s equivalent both
compute `IsEliminationBracket` from a hardcoded `format is TournamentFormat.SingleElimination or
TournamentFormat.DoubleElimination` check that predates this format and was never extended, so the
new format's `Bracket` was always built as empty. No Core test could have caught this - the bug is
entirely in App-project format-eligibility lists that mirror each other without a shared source of
truth. Fix: added `or TournamentFormat.ModifiedSingleElimination` to both. General lesson: grep for
every `TournamentFormat.SingleElimination or TournamentFormat.DoubleElimination`-shaped check
across the App project (not just Core) whenever a bracket-based format is added - Core's tests
cover generation/wiring correctness but nothing exercises the UI's own parallel "which formats
show a bracket" lists.

## 2026-07-07 — Singleton `TournamentStateService` + Scoped `ITournamentRepository` silently split the DbContext in two

**Issue:** Finishing a match showed "Result recorded" - score frozen, winner bolded, bracket
advanced, duration displayed - but a full app restart reverted the match to unplayed. The DB was
never actually written to for any *mutation* of an existing entity (score/status/winner on a
`Match`, `Slot2EntrantId`/`MatchId` on a `BracketNode`); brand-new inserts (`TrackNew`) DID persist,
which is what made the bug so confusing - the visible "half-success" (new bracket-advancement Match
row present, but disconnected from its BracketNode, and the original match still `Scheduled`) came
from two different `PoolTournamentDbContext` instances being in play. `TournamentStateService` is
`AddSingleton`; `ITournamentRepository`/`PoolTournamentDbContext` were `AddScoped`. A Singleton that
depends on a Scoped service is a classic DI "captive dependency": .NET's container resolves the
Singleton's own dependencies through the *root* container cache (not the ambient scope), so
`TournamentStateService`'s `ITournamentRepository` ends up as a materially different instance than
the one resolved directly for `TournamentViewModel`'s own constructor parameter - even though both
are (in this app) ultimately requested from the same single `_appScope`. `GetByIdAsync` (via State)
loaded and tracked the Tournament graph in context A; `SaveChangesAsync()` (via the ViewModel's own
field) ran against context B, which had never seen that graph, so mutating an already-tracked
`Match`/`BracketNode` there was a silent no-op - confirmed by dumping
`_dbContext.ChangeTracker.Entries().Count()` at both call sites (33 vs. 8) and the constructor's
`Environment.StackTrace` (one resolution path went through `VisitScopeCache`, the other through
`VisitRootCache`). This bug predates this session's changes - it would have silently dropped every
`ReportResultAsync` call past the tournament's own creation-time save, for the entire life of the
app; it just took a session that actually restarted the app and diffed before/after to notice, since
the in-memory ViewModel state looks identical either way.

**Fix:** Registered `PoolTournamentDbContext`, `ITournamentRepository`, and `IPlayerRepository` as
`Singleton` instead of `Scoped` in `App.xaml.cs` (`AddDbContext(..., contextLifetime:
ServiceLifetime.Singleton, optionsLifetime: ServiceLifetime.Singleton)`). This app only ever creates
one `IServiceScope` (`_appScope`, for its whole process lifetime), so Scoped was never buying
anything - Singleton just makes the existing one-context-for-the-app-lifetime assumption explicit
and removes the captive-dependency split. General lesson: a Singleton that takes a Scoped
constructor parameter is a bug waiting to happen even when `ServiceProvider.CreateScope()` is only
called once - verify by comparing `dbContext.GetHashCode()`/`ChangeTracker.Entries().Count()` at
the read site vs. the write site if a save silently doesn't stick. A full app **restart** (not just
navigating away and back in the running app - that reuses the same tracked, still-correct-looking
in-memory objects) is the only way this class of bug reliably surfaces.

## 2026-07-07 — `DbContext.Remove()`'s navigation-graph cascade threw a duplicate-tracked-entity error

**Issue:** Regenerating a bracket after adding a player (`TournamentViewModel.RegenerateBracket`)
needs to discard the old `Match`/`BracketNode`/`BracketDetail` rows before creating new ones. Doing
that via `_dbContext.Remove(entity)` (mirroring `TrackNew`'s `_dbContext.Add(entity)`) threw "The
instance of entity type 'Match'/'Player'/'BracketNode' cannot be tracked because another instance
with the same key value ... is already being tracked" on the second or later `Remove()` call in the
same batch. Unlike `Add()` (which only needs to succeed once per entity), `Remove()` walks the
*entire* reachable navigation graph from the given entity (e.g. `Match.Player1Entrant.Player`) to
cascade the delete - and `TournamentRepository.GetByIdAsync`'s Include chain reaches the same
Match/TournamentEntrant/Player rows via more than one path (`Matches` directly, and via
`Bracket.Nodes.Match`), so that graph walk re-attaches an already-tracked entity through a second
path and trips the identity-map conflict.

**Fix:** Use `_dbContext.Entry(entity).State = EntityState.Deleted` instead of `Remove()` in
`TrackRemoved` - it marks only the given entity as Deleted without touching its navigation graph.
General lesson: `Add()`/`Remove()` are graph-walking convenience APIs; anything that repeatedly
mutates an already-fully-loaded aggregate (not a fresh, just-queried root) should prefer the
single-entity `Entry(...).State = ...` form to avoid this whole class of "already tracked via a
different path" error.

## 2026-07-06 — Verified a WPF window's look by rendering it to PNG from a tiny harness, not by driving the live app

**Issue:** The v0.8 bracket-tree Display redesign is a purely visual feature - the only way to
know it's right is to *see* it. Driving the real app to get there (create tournament, add
players, generate bracket, select it, open the Display window) means many coordinate clicks
through the exact surface this app is hostile to (UI Automation can't see tab-page content;
synthetic "Report Result" clicks no-op/race - both documented below), and the app also starts
with an empty DB and no auto-selected tournament.

**Fix:** Two throwaway console projects in the scratchpad (not added to the solution):
(1) a **seeder** that `ProjectReference`s Core+Data and builds a real 8-player single- and
double-elimination tournament through the actual services (`SeedingService`,
`BracketGenerationService`, `TournamentRepository`) into a scratch copy of the app's SQLite DB,
playing out all but the final matches; (2) a **render harness** (`WinExe`, `UseWPF`,
`ProjectReference`s the App) that creates its own `Application`, merges the App assembly's theme
dictionaries via `pack://application:,,,/PoolTournamentManager.App;component/Themes/...`, defines
the same converter resource keys App.xaml does, loads the seeded tournament through a real
`TournamentStateService`, constructs the **real** `DisplayWindow` + `DisplayWindowViewModel`
(+ a `new ThemeService()`), and on `ContentRendered` captures the window with
`RenderTargetBitmap` to a PNG, then `Shutdown()`s. This renders the true XAML + palette with zero
clicks and is deterministic. General lesson for this codebase: to verify how a WPF window *looks*,
render it to a bitmap from a harness that reuses the real window/VM/themes - it sidesteps every
UI-automation and synthetic-click trap and is repeatable. (Back up the dev DB's `.db`+`-wal`+`-shm`
before seeding and restore after, so the user's environment is left as it was.)

## 2026-07-06 — The .NET SDK vanished (only the runtime remained), blocking every build

**Issue:** At the start of the v0.7 work, `dotnet build`/`test`/`ef`/`run` all failed with "No
.NET SDKs were found" even though prior sessions built fine. `C:\Program Files\dotnet` still had
`dotnet.exe`, `host`, and `shared` (the runtimes: NETCore.App and WindowsDesktop.App 8.0.28 and
10.0.9) but no `sdk` folder at all - so only the runtime host was present, not the SDK. Something
(a Windows/VS update, most likely) had removed the SDK.

**Fix:** Reinstalled the SDK the project targets with `winget install --id Microsoft.DotNet.SDK.8
-e` (landed 8.0.422), then `dotnet tool install --global dotnet-ef`. Builds/tests/migrations
worked immediately after. Note for a future session: if `dotnet` is on PATH but reports no SDKs,
check for the `sdk` subfolder before assuming a PATH problem - the runtime and SDK are installed
(and can be removed) independently. Also, on this machine the `dotnet` on the Git Bash PATH
doesn't resolve; run .NET commands from PowerShell (or via the full path
`C:\Program Files\dotnet\dotnet.exe`).

## 2026-07-06 — Verified ring game via a real-SQLite integration test instead of UI automation

**Issue:** v0.7 (ring game) needed end-to-end verification, but this app's UI is hostile to
automation: `AutomationElement` can't see tab-page content at all, and synthetic clicks are
unreliable (both documented below). Driving a brand-new multi-step money flow that way would have
been slow and low-confidence.

**Fix:** Verified the risky App->Data seam with a headless integration test in
`PoolTournamentManager.Data.Tests` that drives the exact patterns the ViewModel uses against a
real SQLite file - `AddAsync` on an untracked root at creation, then `TrackNew` for each
mid-aggregate insert (money-ball payout, cash-out marker), reloading through the eager-loading
`GetByIdAsync` after every step so nothing passes on change-tracker memory alone. The app was
still launched once to confirm it starts and renders (migration applies, XAML parses, DI
resolves). General lesson: for this codebase, an integration test through the repository is a
better verification of a persistence change than fighting the UI-automation limitations - and it
stays as a regression guard. `Core` logic stays in `Core.Tests`; the persistence round-trip goes
in `Data.Tests`.

## 2026-07-06 — "Report Result" button's first click after typing scores does nothing; a second click always works

**Issue:** Driving the app via synthetic mouse clicks (SetCursorPos + mouse_event) to verify round
robin end-to-end, clicking "Report Result" immediately after typing both scores routinely produced
no effect at all - no status message change, no standings update, button stayed enabled - on the
*first* click. A second click on the exact same spot, with no other change, then worked every
single time. Not fully root-caused (candidates: the TextBox's `PropertyChanged`-triggered binding
update racing the click's mouse-down, or the button needing a prior `MouseEnter` to arm its
click handling that a synthetic click skips) - flagged here rather than fixed, since it never
reproduced with a genuine slower human click pattern (deliberate pauses between typing and
clicking) and this may be purely an artifact of synthetic-input timing, not a real defect.

**Related false alarm:** the same double-click habit (retrying a click immediately after one that
*looked* like a no-op) once produced a real-looking `SQLite Error 19: FOREIGN KEY constraint
failed` crash on `SaveChangesAsync` during a round-robin "Report Result" call. Replaying the exact
same tournament-creation-then-report sequence twice more - once against a fresh empty database,
once against a copy of the actual crashed session's database (including its `-wal`/`-shm`
sidecar files, which SQLite needs alongside the main `.db` file to see the most recent committed
writes - a plain copy of just the `.db` file will look stale) - never reproduced the crash. Most
likely explanation: the first "no-op" click had actually already started its async save, and the
too-fast retry raced a second `SaveChangesAsync` on the same non-thread-safe `DbContext` instance.
`[RelayCommand]`-generated commands in this codebase don't guard against concurrent execution
anywhere (not just this one) - worth a real fix (e.g. disabling the button while its command runs)
if this ever surfaces from a genuine double-click, but not chased further here since a single
click always works correctly and no data corruption occurred.

## 2026-07-06 — System.Text.Json silently dropped a hand-written enum string, falling back to the default

**Issue:** Manually wrote `{"ColorScheme":"Red"}` into `settings.json` to test the load path before
trusting UI automation to click the right button (UI automation was proving flaky in this
environment - see below). The app loaded and showed Green (the fallback default) instead of Red,
with no visible error. Root cause: `System.Text.Json` serializes enums as their underlying
**integer** by default; deserializing a **string** ("Red") into that same enum property throws
`JsonException`, which `AppSettingsStore.LoadColorScheme`'s catch-all correctly swallowed (by
design - a corrupt settings file should never block startup) but which also masked this bug
completely, since the fallback (Green) looks identical to a legitimate first-run default.

**Fix:** Added `JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }`, applied
to both `Serialize` and `Deserialize`, so the persisted file actually stores (and accepts) the
enum's name. General lesson for this codebase: a silent fallback that exists to protect against a
corrupt file also hides genuine serialization mismatches - when a persisted setting doesn't seem
to be loading, check the fallback path is even being hit before assuming the bug is elsewhere (a
one-line temporary `throw` instead of `catch` would have surfaced this immediately).

## 2026-07-06 — UI Automation couldn't see a TabControl's selected page content at all

**Issue:** After retemplating `TabControl` (see the entry below from the previous session) to
separate the tab strip's background from the content area's, `AutomationElement.FindAll` for
*anything* inside the selected tab's page - buttons, text, all of it - returned zero results,
even though the content rendered correctly on screen and was interactable by mouse/keyboard. Not
fully root-caused given the time available; suspect the custom template's plain
`ContentPresenter ContentSource="SelectedContent"` inside a `Border` doesn't get hooked into
`TabControlAutomationPeer`'s expectations the same way the default template's does.

**Fix:** Not fixed - noted here so a future automated-UI-testing attempt against this app knows to
expect it, and knows the content is real (confirmed via screenshots) even when automation can't
see it. Verification for this session fell back to mouse clicks (once a coordinate-calculation
mistake was found and corrected) and direct manipulation of the persisted settings file instead.

## 2026-07-06 — Window's own native title bar isn't reachable from WPF at all

**Issue:** Wanted the OS-drawn title bar (with the minimize/maximize/close buttons) to match the
app's accent color in dark mode. `Window.Background`, and every other WPF property, only paints
the *client area* - the title bar itself is drawn by DWM (the OS compositor), entirely outside
anything WPF's Style/resource system can reach.

**Fix:** Added `TitleBarColorizer`, a P/Invoke wrapper around `DwmSetWindowAttribute` with the
`DWMWA_CAPTION_COLOR`/`DWMWA_TEXT_COLOR` attributes (Windows 11 22000+ only - no-ops harmlessly on
older Windows). Needs a real HWND, so each window calls it from `SourceInitialized`, and
`ThemeService` re-applies it to every open window on a live theme change (mirroring how it already
re-applies the resource-dictionary palette swap).

## 2026-07-06 — Window.Background set via implicit Style silently didn't render; TabControl's own chrome ignored it too

**Issue:** Generic.xaml's implicit `Style TargetType="Window"` set `Background` to a themed brush,
but the header area (a transparent StackPanel sitting directly on the Window, above the
TabControl) rendered plain white regardless of theme - meanwhile `DisplayWindow.xaml`, which sets
`Window.Background` as a **local** attribute instead of relying on the implicit style, rendered
correctly. Separately, the `TabControl`'s own tab-strip area *also* rendered white despite a
`Background` `Setter` on it, for the by-now-familiar reason (see the Button/TabItem/ComboBox entry
below): its default template doesn't read `Background` at all for that chrome.

**Fix:** Set `Background` as a **local** attribute directly on both `<Window>` elements (matching
what already worked for `DisplayWindow`) instead of depending on the implicit `Style`. Gave
`TabControl` a real `ControlTemplate` (a two-row `Grid`: a `Border` for the tab strip, a separate
`Border` for the selected page's content) instead of a `Setter`, so the strip and the content area
can each have their own explicit, reliable background. Broader lesson for this codebase: don't
trust an implicit `Style` `Setter` for `Background`/`Foreground` on *any* control until it's been
visually verified - for `Window` specifically, prefer a local attribute.

## 2026-07-06 — A `<StaticResource x:Key=".." ResourceKey=".."/>` alias element silently failed

**Issue:** Tried to make several palette keys (`SurfaceBackgroundBrush`, `CardBackgroundBrush`,
etc.) all resolve to the same color as `AccentPrimaryBrush` by aliasing them via
`<StaticResource x:Key="Foo" ResourceKey="AccentPrimaryBrush" />` inside the dark palette
dictionary. Every `DynamicResource` binding against those aliased keys came back empty (rendered
as if the background were unset/white) instead of the aliased color.

**Fix:** Gave up on the alias and just duplicated the literal `SolidColorBrush` + `Color` value
under each key. Not fully root-caused given the time available - noting it here so a future
attempt at DRY-ing up palette values via aliasing knows this specific pattern is suspect and
should be checked carefully rather than assumed to work.

## 2026-07-06 — Button text silently used the wrong theme brush (Style Setter beat inherited Foreground)

**Issue:** Button.Foreground was set to `AccentPrimaryTextBrush` (white in light mode) via the
implicit `Button` `Style`, but the rendered text stayed dark. Root cause: `Button.Content` is
always a plain string in this app, so with no `ContentTemplate`, WPF auto-wraps it in an anonymous
`TextBlock`. That generated `TextBlock` matches the app's own global implicit
`Style TargetType="TextBlock"` (which sets `Foreground` to `TextPrimaryBrush` for ordinary body
text) - and a `Style` `Setter` on an element always wins over a `Foreground` value the element
would otherwise have *inherited* from an ancestor (here, the Button), regardless of how that
ancestor's value was itself set. So the button's own `Foreground` never had a chance to apply to
its own text.

**Fix:** Gave the `Button` style an explicit `ContentTemplate` (`<TextBlock Text="{Binding}"
Foreground="{Binding Foreground, RelativeSource={RelativeSource AncestorType=Button}}" />`) so the
text's `Foreground` is a real local value (via binding) on that specific `TextBlock`, which does
outrank a `Style` `Setter`. General lesson for this codebase: *any* control whose default content
wrapping produces a `TextBlock` (Button, CheckBox, TabItem headers, etc.) is at risk of the same
issue if the app also has a global implicit `TextBlock` style - inheritance alone won't reliably
carry a color through.

## 2026-07-06 — Button/TabItem/ComboBox default chrome ignores the Background property

**Issue:** Setting `Background`/`Foreground`/`BorderBrush` via a `Style` `Setter` worked fine for
`TextBox`, `ListBox`, and `DataGrid`, but had no visible effect on `Button`, `TabItem`, or
`ComboBox` - they kept rendering with plain system-gray chrome regardless of what the Setters
said. Root cause: those three controls' default templates paint their main surface using
classic-theme chrome elements (button/tab/combo "chrome" borders) that read from system theme
colors, not from the control's own `Background` property. A `Setter` only ever changes the
*value* of a dependency property - it can't make a template read a property it was never bound to.

**Fix:** Gave all three a full `ControlTemplate` override in `Themes/Generic.xaml` that explicitly
binds `Border.Background`/`BorderBrush` to `{TemplateBinding Background}`/`BorderBrush`. Lesson
for this codebase: when a themed Setter has no visible effect on a standard WPF control, suspect
the default template is ignoring that property rather than a resource-resolution bug.

## 2026-07-06 — Retemplated ComboBox: nested ToggleButton read a property that was never actually set

**Issue:** After adding the ComboBox `ControlTemplate` above, it still rendered as a flat
system-gray box instead of the theme's control background - a different bug from the one above,
in the same area. The inner `ToggleButton`'s own nested template read
`{Binding Background, RelativeSource={RelativeSource TemplatedParent}}` (i.e. "whatever
`Background` is set to on the `ToggleButton` element"), but the outer `ComboBox` template's
`<ToggleButton>` element never actually had a `Background`/`BorderBrush`/`BorderThickness`
*attribute* set on it - so that property was reading its own unset default the whole time,
regardless of what the outer `ComboBox.Background` resolved to.

**Fix:** Explicitly set `Background="{TemplateBinding Background}"` (and `BorderBrush`/
`BorderThickness`) directly on the `<ToggleButton>` element in the *outer* template - only then
does the *inner* template's own `{TemplateBinding Background}` have something real to read.
General lesson: when nesting a `ControlTemplate` inside another control that itself lives inside
a template, `TemplateBinding`/`RelativeSource TemplatedParent` on the inner template refers to the
immediate control (here, the `ToggleButton`), not the outer templated control (the `ComboBox`) -
a value has to be explicitly relayed onto that immediate control first, one hop at a time.

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
