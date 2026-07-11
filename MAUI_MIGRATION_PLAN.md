# Maui Migration Plan for PoolTournamentManager

## Executive Summary

PoolTournamentManager is a mature Windows WPF tournament management app (~19k LOC of source, 4.3k LOC of tests) with robust business logic for bracket generation, scoring, and tournament formats. The migration to Maui will allow it to run on Windows desktop and Android tablets while preserving the existing Core/Data architecture and logic. The plan spans 8 major phases over an estimated 12-16 weeks, with careful separation between reusable business logic and platform-specific UI concerns.

---

## Current Architecture Analysis

### What Can Be Reused

- **PoolTournamentManager.Core** (100%) - All entities, enums, interfaces, and services (BracketGenerationService, ChipGameService, RingGameService, RoundRobinSchedulingService, SeedingService, PrizePayoutService, validators)
- **PoolTournamentManager.Data** (95%) - EF Core DbContext, repositories, migrations; only the database path resolution needs platform abstraction
- **Base MVVM patterns** - CommunityToolkit.Mvvm.ComponentModel/RelayCommand are cross-platform; most ViewModels can be adapted
- **Shared resource assets** - Logo, icons (convert to cross-platform formats)

### What Must Be Rewritten

- WPF-specific UI (XAML, windows, converters, dialogs) → Maui pages/controls (XAML-based, but Maui-specific)
- Theme service (WPF's registry/native title bar → Maui's app theme system)
- BracketLayoutBuilder (WPF rendering specifics → cross-platform layout logic + platform-specific rendering)
- DisplayWindow (WPF second-screen support → Maui screen/window management)
- Converters and value types specific to WPF binding semantics
- Platform-specific file/database access patterns
- DPI/scaling handling (WPF's physical-pixel model → Maui's logical/physical density)

### What Needs Significant Adaptation

- TournamentStateService - WPF-oriented ObservableObject usage; needs abstraction for two-way data binding pattern
- App.xaml.cs and DI setup - split into platform-specific startup code
- Error handling/logging (WPF MessageBox → Maui DisplayAlert; file logging paths differ)

---

## Phase 1: Foundation & Project Structure (2 weeks, ~80 effort points)

**Objective:** Create the new solution structure, establish shared code organization, migrate Core/Data as-is, and prove Maui can access the business logic.

### Steps

1. **Create new multi-target project structure:**
   - Rename current solution to `PoolTournamentManager.sln` (keep existing, archive as backup)
   - Create `.maui/` branch or subfolder for Maui exploration
   - Add two new project types to solution:
     - `PoolTournamentManager.Maui.Desktop` (net8.0-windows)
     - `PoolTournamentManager.Maui.Android` (net8.0-android34.0)
   - Keep existing Core/Data projects unchanged (core focus: no modifications to business logic)
   - Create `PoolTournamentManager.App.Abstraction` project (net8.0) for cross-platform service interfaces

2. **Extract platform abstractions into App.Abstraction:**
   - `IPlatformStorageService` - database path resolution, document access, file logging
   - `IThemeService` - theme provider (interface only; implementations per platform)
   - `INavigationService` - page/window navigation abstraction
   - `IDialogService` - alerts/dialogs abstraction (MessageBox equivalent)
   - `IClipboardService` - clipboard access
   - `IDisplayService` (optional) - multi-monitor/second-screen support on Windows

3. **Migrate Core and Data projects as subprojects:**
   - Copy Core/Data csproj files, update target frameworks to `net8.0` if needed (leave as cross-platform)
   - Verify no breaking changes; run unit tests on both to confirm
   - Add Core/Data as project references to abstraction layer

4. **Set up Maui desktop/Android entry points (bare minimum):**
   - Create `MauiProgram.cs` stub for Maui startup
   - Register Core services (repositories, bracket services) in DI container
   - Verify compilation and basic DI resolution works

5. **Database abstraction:**
   - Create `PlatformDatabasePathResolver` class in App.Abstraction
   - Windows impl: `%LOCALAPPDATA%\PoolTournamentManager\tournaments.db` (same as current)
   - Android impl: `context.GetExternalFilesDir(null)` or `context.FilesDir` + `tournaments.db`
   - Add conditional DI registration per platform

6. **Testing strategy foundation:**
   - Create `PoolTournamentManager.Maui.Tests` (unit tests for new abstraction layer)
   - Verify Core tests still pass
   - No UI tests yet (premature); focus on logic layer isolation

### Deliverables

- Maui.Desktop and Maui.Android projects compile and can instantiate Core services
- App.Abstraction interfaces defined and Core/Data accessible
- PlatformDatabasePathResolver working for both platforms
- All existing Core/Data tests green

### Effort & Duration

- **Effort:** 80 points
- **Duration:** 2 weeks
- **Risk:** Medium (first Maui scaffolding; DI wiring)

---

## Phase 2: Core MVVM Refactor & Abstraction (2 weeks, ~70 effort points)

**Objective:** Adapt TournamentStateService and key ViewModels to work cross-platform; decouple from WPF-specific bindings.

### Steps

1. **Create cross-platform base ViewModels:**
   - Define `ObservableObject` base or interface in App.Abstraction (wrap or extend CommunityToolkit.Mvvm.ComponentModel)
   - Ensure compatibility with Maui's binding semantics (very similar to WPF, but test edge cases)
   - Create `RelayCommand` equivalents if needed (likely not; Mvvm toolkit works on Maui)

2. **Refactor TournamentStateService:**
   - Split into two layers:
     - `TournamentStateCore` (net8.0) - holds Tournament, collections, computed properties; logic-only, no UI framework deps
     - `TournamentStateServiceWindows` (net8.0-windows) - WPF-specific ObservableObject wrapper (existing code, mostly as-is)
     - `TournamentStateServiceMaui` (new) - Maui-specific wrapper using Maui's ViewModel patterns
   - Move public interface to abstraction: `ITournamentStateService`
   - Platform-specific DI registration injects the right impl

3. **Adapt existing ViewModels:**
   - Copy MainWindowViewModel → `AdminViewModel` (platform-agnostic, logic-focused)
   - Copy DisplayWindowViewModel → `DisplayViewModel` (logic-focused, no UI rendering yet)
   - Copy editor ViewModels (PlayerEditorViewModel, TeamEditorViewModel) → cross-platform versions
   - Remove WPF-specific event handlers (e.g., `TournamentReady` event → use bindings or explicit callback)
   - Extract view-coordination logic to a separate `ViewCoordinator` or navigation service

4. **Create abstraction for dialog/editor workflows:**
   - `IPlayerEditorDialog` - open editor, return edited Player or null
   - `ITeamEditorDialog` - open editor, return edited Team or null
   - `IConfirmDialog` - yes/no confirmation before delete
   - Concrete impls will be platform-specific Maui Pages/PopUps

5. **Migrate converters to cross-platform value converters:**
   - `CountToVisibilityConverter` → `CountToVisibilityValueConverter` (Maui-compatible signature)
   - `EnumEqualsConverter` → similar cross-platform version
   - `NonEmptyToVisibilityConverter`, `NotNullToBoolConverter` → equivalent
   - Test in both platforms to ensure logic is identical

6. **Dependency injection consolidation:**
   - Define a `ServiceBootstrapper` in App.Abstraction that both platforms use
   - Register Core services, cross-platform ViewModels, abstraction impls
   - Platform-specific `MauiProgram.cs` calls `ServiceBootstrapper.Register()` + adds platform-specific services

7. **Test coverage:**
   - Verify all Core tests still pass
   - Add unit tests for refactored ViewModels (no UI frame needed; just instantiate and test properties)
   - Add tests for value converters

### Deliverables

- TournamentStateService abstracted; Core logic isolated
- Cross-platform ViewModels compile and work in both platforms
- Value converters migrated and tested
- App.Abstraction has clear, documented service contracts

### Effort & Duration

- **Effort:** 70 points
- **Duration:** 2 weeks
- **Risk:** Medium-High (refactoring risk; MVVM semantics must align)

---

## Phase 3: Maui UI Scaffold & Navigation (2 weeks, ~85 effort points)

**Objective:** Build the basic Maui page structure, implement navigation, and connect ViewModels to UI without complex rendering yet.

### Steps

1. **Create Maui page structure (desktop + Android responsive):**
   - `AppShell.xaml` - root shell with tabs (Players, Teams, Tournament, Settings) + menu
   - `PlayersPage.xaml/cs` - player list (CollectionView) + add/edit/delete buttons
   - `TeamsPage.xaml/cs` - team list similar pattern
   - `TournamentPage.xaml/cs` - main tournament UI (tabs for Bracket/Standings/Ring/Chip/FinalResults)
   - `TournamentSettingsPage.xaml/cs` - format/seeding selection
   - `DisplayPage.xaml/cs` - read-only display view (mirrors tournament state)
   - Editor modals: `PlayerEditorPage`, `TeamEditorPage` (Popup or Modal Shell route)
   - Help/About pages: `HelpPage`, `AboutPage`

2. **Implement cross-platform navigation:**
   - `NavigationService` implementation using Maui Shell or custom router
   - Centralized route definitions (avoid hard-coded route strings scattered in ViewModels)
   - Support deep linking (e.g., direct to tournament tab if needed)
   - Handle modal workflows (editor opens, returns result, closes)

3. **DataTemplate-based rendering (no bracket visualization yet):**
   - Player list: simple TextCell or DataTemplate showing Name, Rating
   - Team list: similar
   - Tournament matches: simple list showing Player1 vs Player2, Score, Table
   - Standings: list showing Place, Name, Wins, Points
   - Entrant list: simple list (no drag-drop yet)
   - Skip visual bracket rendering for now (that's Phase 4)

4. **Implement platform-specific dialogs:**
   - `ConfirmationDialog` - yes/no prompts (DeletePlayer, DeleteTeam, DeleteTournament)
   - `AlertDialog` - info dialogs (success messages, errors)
   - `InputDialog` (optional) - text entry for tournament name
   - Map abstraction interfaces to Maui implementations

5. **Tab/page coordination:**
   - Use Shell TabBar for main tabs
   - Implement `INavigation` for "switch to Tournament tab when tournament created" workflow
   - Desktop: consider Window management or single-window tabs
   - Android: use Shell navigation

6. **Basic styling (placeholder theme):**
   - Light and Dark mode support via `AppThemeBinding`
   - Consistent padding, fonts, colors (use a basic palette, not final design)
   - Use Maui's built-in resource dictionaries for brush definitions
   - Goal: functional, not beautiful yet

7. **Responsive layout scaffolding:**
   - Desktop: wider layouts, multi-column grids where suitable
   - Android: single-column, touch-friendly spacing
   - Use `OnPlatform` and adaptive triggers for divergence
   - Tablet-specific hints (medium ≥600dp width might use wider layouts)

### Deliverables

- All pages compile and navigate correctly
- ViewModels wired to pages via BindingContext
- Player/Team/Tournament CRUD workflows flow end-to-end (no visual bracket yet)
- Platform-specific dialogs working
- Light/Dark theme switching works

### Effort & Duration

- **Effort:** 85 points
- **Duration:** 2 weeks
- **Risk:** Medium (Maui Shell learning curve, async dialog coordination)

---

## Phase 4: Bracket Visualization & Complex Rendering (3 weeks, ~120 effort points)

**Objective:** Rebuild bracket rendering for cross-platform support; handle Desktop/Android differences in layout.

### Critical Decisions

**Bracket rendering strategy:**
- **Option A: Custom Canvas drawing (GraphicsView in Maui)** - full control, more complex, cross-platform
- **Option B: SVG rendering** - simpler, good cross-platform support, can be styled via CSS
- **Option C: HTML5 Canvas via WebView** - flexible, but adds web layer complexity
- **Recommendation:** Option A (Canvas) for now; SVG as fallback if performance issues arise

**Form factor differences:**
- **Desktop:** full visual bracket at normal zoom, "Fit to Window" mode, potential manual pan/zoom
- **Tablet:** auto-fit to screen, touch-friendly, simplified bracket if too many entrants
- Consider responsive breakpoints (small tablet <600dp, large tablet ≥600dp)

### Steps

1. **Rebuild BracketLayoutBuilder for cross-platform:**
   - Core logic (node positioning) stays the same; extract into `BracketLayout` class (net8.0)
   - Graphics rendering layer: create platform-specific implementers
   - `IBracketRenderer` interface: `DrawBracket(layout, canvas, theme)`
   - `GraphicsViewBracketRenderer` (Maui, uses GraphicsView): render boxes, connectors, text
   - Ensure identical layout math on both platforms

2. **Handle display scaling correctly:**
   - Desktop: respect Windows DPI, compute physical pixels
   - Android: use DisplayDensity to scale touch input/output
   - Store layout in logical units; scale at render time
   - Bracket tests verify node positions are identical before scaling

3. **Bracket zoom & pan (Desktop):**
   - Implement zoom via transform (scale) + pan
   - Fit-to-Window mode calculates zoom to fit bracket in visible area
   - Track mouse wheel for zoom, mouse drag for pan (or touch on Android)
   - Reset button to return to Fit-to-Window

4. **Match box interaction (both platforms):**
   - Tap/click a match box to open match detail editor
   - Show entrant names, current score, status
   - Allow score update if match not yet finished
   - Editor page (MatchEditorPage) similar to player/team editors
   - Close editor, update model, re-render bracket

5. **Standings and Ring/Chip rotation rendering:**
   - DataGrid-like component for standings (list of columns: Place, Name, Wins/Points/etc.)
   - Ring game: rotating list of players, current up player highlighted
   - Chip tournament: table board (who's at each table now), Next Up queue
   - These are less complex than bracket; build on DataTemplate/CollectionView patterns

6. **Second-screen / Display view:**
   - Maui doesn't have built-in second-monitor support like WPF; strategy depends on platform:
     - **Windows desktop:** Can use Win32 P/Invoke to detect secondary displays and open app window on that display (or render to texture and display via network)
     - **Android:** Not typically multi-display; could support wireless casting (Miracast) but complex; initial cut: skip or display via web dashboard
   - Start with single-screen mirroring (same display shows bracket and results)
   - Desktop second-screen as optional enhancement later

7. **Testing bracket rendering:**
   - Visual regression tests: render a known bracket, capture output, compare to baseline
   - Geometry tests: verify node positions match Core tests
   - Responsive tests: verify layout adapts to different screen sizes

### Deliverables

- Bracket rendering works on Desktop and Android
- Zoom/pan on Desktop, auto-fit on Android
- Match editing workflow complete
- Standings/Ring/Chip rotations display correctly
- No second-screen in MVP; noted for Phase 6 (nice-to-have)

### Effort & Duration

- **Effort:** 120 points
- **Duration:** 3 weeks
- **Risk:** High (graphics rendering, platform scaling subtleties, performance on complex brackets)

---

## Phase 5: Data Persistence & Migration (1.5 weeks, ~60 effort points)

**Objective:** Ensure EF Core SQLite works on both platforms; handle database initialization and migration.

### Steps

1. **Verify EF Core migrations work on Maui:**
   - Test migration application on Windows (existing flow + new abstraction layer)
   - Test migration application on Android (file path, permissions)
   - Confirm entities load/save correctly on both platforms

2. **Database initialization on first app launch:**
   - Detect if database exists; if not, run migrations
   - Handle migration errors gracefully (alert user, log, possibly auto-create)
   - Desktop: may require elevated permissions for Program Files installs (but Maui apps won't be installed there; use LocalAppData)
   - Android: use app-specific directory (no user elevation needed)

3. **Platform-specific file access:**
   - Implement `PlatformStorageService` for each platform
   - Windows: use Path.Combine with Environment.SpecialFolder.LocalApplicationData
   - Android: use context.GetExternalFilesDir() or FilesDir
   - Register in DI per platform

4. **Backup/export functionality (optional for MVP):**
   - Export database to user's Documents/Downloads for sharing
   - Import previously exported database
   - Defer to Phase 6 if time constrained

5. **Test data seeding (optional):**
   - Add helper to seed sample tournaments for testing
   - Useful for development; disable in Release builds

6. **Data validation & error handling:**
   - Ensure all validation from Core.Services carries through UI
   - Display validation errors to user via dialog/alert
   - Log errors to local file for debugging

### Deliverables

- Database initializes correctly on first app launch (both platforms)
- Existing tournaments load, new tournaments persist
- All data validations working end-to-end
- Error recovery graceful (no crashes, helpful messages)

### Effort & Duration

- **Effort:** 60 points
- **Duration:** 1.5 weeks
- **Risk:** Low (EF Core is mature; mostly integration)

---

## Phase 6: Theming, Styling & Polish (1.5 weeks, ~70 effort points)

**Objective:** Implement dark/light theme support, finalize visual design, match Windows/Android platform conventions.

### Steps

1. **Theme system implementation:**
   - Define cross-platform theme colors (similar to current WPF palette brushes)
   - Implement `IThemeService` for both platforms
   - Windows: detect OS dark/light mode via Win32 Registry (or Maui's AppTheme API)
   - Android: use System theme setting
   - Create `AppThemeBinding` or similar for dynamic theme switching

2. **Palette definition:**
   - Create `Themes/Light.xaml` and `Themes/Dark.xaml` resource dictionaries
   - Colors: background, surface, text (primary/secondary), accent, border, etc.
   - Map to Maui's color naming conventions (consistent with platform)

3. **Control styling:**
   - Default styles for Button, Entry, Label, CollectionView, DataGrid-like components
   - Ensure readability in both light and dark modes
   - Use consistent spacing, typography (font sizes, weights)
   - Accessibility: color contrast ratios, hit-target sizes (48dp recommended on Android)

4. **Platform-specific UX tweaks:**
   - Windows: keyboard shortcuts (Ctrl+N for New Tournament, Ctrl+S for Save, F11 for fullscreen Display)
   - Android: back button handling, haptic feedback on actions
   - Tablet: use landscape orientation, wider layouts
   - Phone: portrait only or adaptive rotation

5. **About box, Help, splash:**
   - Update About page with version, copyright, license link
   - Rebuild Help (embedded content similar to current app)
   - Optional splash screen on app startup

6. **Accessibility (WCAG 2.1 level A):**
   - Semantic labels for screen readers
   - Sufficient color contrast
   - Touch-friendly button sizes (48x48dp min)
   - Keyboard navigation support

### Deliverables

- Dark/Light theme working on both platforms
- All UI styled consistently
- Help and About content present
- App feels polished and platform-native

### Effort & Duration

- **Effort:** 70 points
- **Duration:** 1.5 weeks
- **Risk:** Medium (design choices, platform consistency)

---

## Phase 7: Testing & QA (1.5 weeks, ~80 effort points)

**Objective:** Comprehensive test coverage of new Maui-specific code; manual QA on both platforms.

### Steps

1. **Unit tests for new code:**
   - Test `PlatformStorageService` implementations
   - Test platform-specific dialogs
   - Test navigation service
   - Test value converters (Maui versions)
   - Target: >80% code coverage on App.Abstraction and platform implementations

2. **Integration tests:**
   - Create tournament end-to-end (Desktop & Android)
   - Add entrants, generate bracket, start match, finish tournament
   - Verify data persists across app restart
   - Test all tournament formats (Single Elim, Double Elim, Modified Single Elim, Round Robin, Ring, Chip)

3. **Bracket rendering tests:**
   - Verify bracket layout math on both platforms
   - Test zoom/pan on Desktop
   - Test bracket auto-fit on Android
   - Visual regression (if automated testing available)

4. **Performance testing:**
   - Measure app startup time (goal: <2s)
   - Measure bracket rendering for large entrant counts (32, 64, 128)
   - Measure UI responsiveness when adding/deleting entrants
   - Profile for memory leaks

5. **Platform-specific QA:**
   - **Windows:**
     - Multiple monitors (if feasible; note: second-screen deferred to future)
     - High DPI scenarios (verify scaling correct)
     - Dark/Light mode switching during app run
   - **Android:**
     - Multiple screen sizes (phone, 7" tablet, 10" tablet)
     - Device rotation (portrait ↔ landscape)
     - Low-memory scenarios (memory pressure)
     - Network off (offline support; should work locally)

6. **Backwards compatibility:**
   - Verify v0.35 WPF databases can be loaded by Maui apps
   - Test migration path: WPF → Maui (same database)

7. **Manual playthrough:**
   - Follow complete user workflows:
     - Create player roster, add teams
     - Run tournament from setup through results
     - Verify Display view mirrors main view
     - Verify all format-specific logic (ring rotations, chip eliminations, bracket propagation)

### Deliverables

- >80% test coverage on new code
- All integration tests green
- Manual QA sign-off on both platforms
- Performance benchmarks documented
- Known issues/deferred items listed in NOTES.md

### Effort & Duration

- **Effort:** 80 points
- **Duration:** 1.5 weeks
- **Risk:** Medium (depends on number of bugs found)

---

## Phase 8: Release Preparation & Documentation (1 week, ~50 effort points)

**Objective:** Finalize packaging, documentation, and prepare for public release.

### Steps

1. **Update version & release notes:**
   - Bump to v0.40 (major feature: Maui multi-platform support)
   - Create RELEASE_NOTES.md: "Now runs on Windows desktop and Android tablets"
   - List known differences from WPF version (e.g., second-screen deferred)

2. **Update README.md & FUNCTIONS.md:**
   - Note multi-platform support
   - Add Android installation instructions (APK or Play Store if distributed there)
   - Update screenshots (show both Windows and Android)
   - Clarify feature availability per platform

3. **Build & packaging:**
   - Desktop: Create installer via `dotnet publish` + Inno Setup (or MSIX via Visual Studio)
   - Android: Create signed APK or AAB (App Bundle) for distribution
   - Test installer workflows (fresh install, upgrade from WPF version)

4. **Platform-specific store prep:**
   - Windows: consider Microsoft Store submission (automated, but review needed)
   - Android: consider Google Play Store submission (requires developer account, play services setup)
   - MVP: distribute APK directly + GitHub releases for Windows build

5. **Git workflow & tagging:**
   - Create release branch (if not using main)
   - Tag commit as v0.40 (following existing policy)
   - Update CLAUDE.md with new conventions if any (e.g., platform suffixes for issues)
   - Merge to main, push tags

6. **Migration guide (internal docs):**
   - Document codebase changes for future developers
   - Outline how to add platform-specific code
   - Explain abstraction layers and why they exist

### Deliverables

- Windows installer builds and installs cleanly
- Android APK builds and installs on test device
- Documentation updated
- v0.40 tagged and released
- Public announcement ready

### Effort & Duration

- **Effort:** 50 points
- **Duration:** 1 week
- **Risk:** Low (mostly administrative)

---

## Architectural Decisions & Trade-Offs

| Decision | Rationale | Alternative | Outcome |
|----------|-----------|-------------|---------|
| **Bracket rendering: GraphicsView** | Full control, cross-platform, no web layer | SVG or WebView | Canvas provides fine-grained control; SVG could be added later if performance issues arise |
| **DI Container: MS Extensions** | Already in use; mature, Maui-compatible | Autofac, Ninject | Reduces external dependencies |
| **Second-screen (deferred)** | Android doesn't support multi-monitor natively; Windows support complex | Implement now | Defer to Phase 6+ as optional; initial release single-screen only |
| **Data storage: SQLite + EF Core** | Already proven; Android support built-in | Cloud-sync (Firebase), other ORMs | Keeps offline-first model; no server dependency |
| **MVVM: CommunityToolkit.Mvvm** | Already in use; supports Maui | Prism, ReactiveUI | Reduces rewrite scope; compatible patterns |
| **UI per-platform: Maui Shell** | Single codebase, platform-specific rendering | separate WPF + Android UIs | Maui Shell balances code reuse with platform conventions |
| **Responsive breakpoints** | Tablet/desktop/phone detection | Fixed layouts | Adapts to actual device capabilities |

---

## Risk Assessment & Mitigation

| Risk | Severity | Mitigation |
|------|----------|-----------|
| **Bracket rendering complexity** | High | Prototype GraphicsView rendering early (Phase 1 spike); unit tests with geometry validation; consider SVG fallback |
| **MVVM semantics mismatch** | Medium | Build sample page early in Phase 2; ensure converters work; test binding two-way updates |
| **Performance on large brackets** | Medium | Profile during Phase 4; consider virtualization or progressive rendering for 64+ entrants; cap display at 32 for first release if needed |
| **Android permission/file access** | Medium | Test early in Phase 1; use scoped storage APIs correctly; handle denials gracefully |
| **EF Core migrations** | Low | EF Core is production-ready; test existing migrations apply cleanly; create integration test |
| **Theme switching bugs** | Medium | Comprehensive light/dark testing during Phase 6; automated visual regression tests |
| **Platform consistency** | Medium | Regular cross-platform testing; use platform conventions (not forcing WPF UX onto Android) |
| **Second-screen support** | Low (deferred) | Noted for future; MVP single-screen only; document how to add multi-monitor later |

---

## Effort & Timeline Summary

| Phase | Name | Duration | Effort | Cumulative |
|-------|------|----------|--------|-----------|
| **1** | Foundation & Project Structure | 2 weeks | 80 pts | 80 |
| **2** | MVVM Refactor | 2 weeks | 70 pts | 150 |
| **3** | Maui UI Scaffold & Navigation | 2 weeks | 85 pts | 235 |
| **4** | Bracket Visualization | 3 weeks | 120 pts | 355 |
| **5** | Data Persistence | 1.5 weeks | 60 pts | 415 |
| **6** | Theming & Polish | 1.5 weeks | 70 pts | 485 |
| **7** | Testing & QA | 1.5 weeks | 80 pts | 565 |
| **8** | Release Prep | 1 week | 50 pts | 615 |
| **TOTAL** | | **15 weeks** | **615 pts** | **615** |

**Assuming 40 effort points/week:** ~15 weeks (3.5 months) for a single developer or small team.

**With 2 developers working in parallel** (e.g., one on UI, one on bracket rendering): ~8-10 weeks.

---

## Phase Sequencing & Dependencies

```
Phase 1 (Foundation)
  ├─ Phase 2 (MVVM) ← depends on Phase 1 DI setup
  │   ├─ Phase 3 (UI Scaffold) ← depends on Phase 2 ViewModels
  │   │   ├─ Phase 4 (Bracket Rendering) ← depends on Phase 3 pages
  │   │   ├─ Phase 5 (Data Persistence) ← independent, can start after Phase 1
  │   │   └─ Phase 6 (Theming) ← depends on Phase 3 pages
  │   └─ Phase 7 (Testing) ← depends on all above
  └─ Phase 8 (Release) ← depends on Phase 7
```

**Parallelizable work:**
- Phase 5 (Data) can start after Phase 1, independent of Phase 2-3
- Phase 6 (Theming) can partially overlap Phase 4
- Phase 7 (Testing) can start component-wise as each phase completes

---

## Critical Files for Implementation

The following files are most critical to this migration and should be carefully reviewed/refactored during each phase:

1. **`src/PoolTournamentManager.Core/Services/BracketGenerationService.cs`** - Core bracket generation logic; must remain unchanged to preserve all tournament format algorithms. Used directly in Maui with no modification.

2. **`src/PoolTournamentManager.Data/Persistence/PoolTournamentDbContext.cs`** - EF Core DbContext; needs minimal changes (only database path abstraction). All migrations must remain valid.

3. **`src/PoolTournamentManager.App/Services/TournamentStateService.cs`** - WPF-specific observable state management; must be refactored into platform-agnostic core (TournamentStateCore) + platform-specific wrappers (TournamentStateServiceWindows, TournamentStateServiceMaui).

4. **`src/PoolTournamentManager.App/Services/BracketLayoutBuilder.cs`** - Complex WPF-specific bracket visualization layout logic; core positioning algorithm extracted to cross-platform `BracketLayout` class, rendering delegated to platform-specific `IBracketRenderer` implementations.

5. **`src/PoolTournamentManager.App/ViewModels/MainWindowViewModel.cs`** - Primary application controller; migrate to `AdminViewModel` cross-platform version; extract view coordination (tab switching, editor workflows) to `NavigationService`.

---

## Suggested Implementation Starting Points

**Immediate next steps after approval:**

1. **Week 1 of Phase 1:** Create Maui.Desktop and Maui.Android projects; get them to compile and instantiate Core services via DI.

2. **Week 2 of Phase 1:** Implement `PlatformStorageService` for database paths; verify EF Core DbContext can be instantiated and queried on both platforms.

3. **Spike (optional, parallel to Phase 1 Week 2):** Create minimal Maui page with `GraphicsView`; render a simple bracket box to prove rendering layer works.

4. **Phase 2 Week 1:** Extract `TournamentStateService` logic into platform-agnostic `TournamentStateCore`; verify Core tests still pass.

5. **Phase 3 Week 1:** Build basic page structure (Players, Teams, Tournament tabs); wire ViewModels to pages; verify data flows and CRUD operations work end-to-end without visual bracket.

---

## Next Steps

1. **Approve the approach** — confirm direction before starting Phase 1
2. **Create branch strategy** — work in a `.maui/` branch or separate exploration branch until Phase 8
3. **Kick off Phase 1** — create Maui projects, wire up DI, verify Core services are accessible
4. **Spike Phase 4** (early) — prototype GraphicsView bracket rendering to de-risk the riskiest part
