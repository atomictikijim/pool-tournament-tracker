---
name: wpf-theming
description: Reference for PoolTournamentManager's WPF theming — the color-scheme/palette structure and the dependency-property precedence traps that have repeatedly caused "the brush silently didn't apply" bugs. Read BEFORE touching anything in Themes/, adding a color scheme, restyling a control, editing a palette brush key, or changing a DataTrigger/Style that sets Foreground/Background/Visibility.
---

# WPF theming in PoolTournamentManager

Theming has two independent axes, both handled by
[ThemeService.cs](src/PoolTournamentManager.App/Services/ThemeService.cs):

- **Light/Dark** — tracks the Windows "choose your color mode" setting live
  (`HKCU\...\Themes\Personalize\AppsUseLightTheme` + `SystemEvents.UserPreferenceChanged`).
- **Color scheme** — `AppColorScheme` enum (Green/Red/Blue/Grey), user-picked on the Settings
  tab, persisted to `%LOCALAPPDATA%\PoolTournamentManager\settings.json` via `AppSettingsStore`.

The active palette dictionary is `Themes/Palette.{Scheme}.{Light,Dark}.xaml`, chosen from both
axes. Shared themed control styles live in `Themes/Generic.xaml`. All palettes define the SAME
brush keys so any scheme×mode combination resolves every key.

## Adding a color scheme

1. Create `Themes/Palette.{Scheme}.Light.xaml` and `.Dark.xaml`, defining every brush key the
   existing palettes define (copy Green's and rotate the hue; theme light/dark independently for
   contrast — a bright accent usually needs to darken on a light background).
2. Add the enum value and wire the Settings-tab swatch.
3. Verify BOTH modes for the new scheme, including the native title bar, and that switching to
   it live repaints open windows and survives a restart.

## The precedence traps (each cost real debugging time — see NOTES.md)

WPF dependency-property precedence, highest first: **local value > Style trigger > Style setter >
inherited > default**. Almost every theming bug here is a violation of that ordering:

1. **Local value beats a Style/DataTrigger.** Setting `Foreground="White"` (or any brush) as an
   attribute directly on an element makes a `Style.Trigger`/`DataTrigger` for that same property
   NEVER win. Fix: put the default in the `Style` as a base `Setter` so the trigger can compete —
   or, better for conditional display strings, compute the string in the ViewModel and bind it
   (the DisplayWindow "Open" vs "p1 vs p2" fallback does this) instead of using a trigger.

2. **An implicit `TextBlock` Style beats an inherited `Foreground`.** `Button.Content` is a bare
   string here, so WPF auto-wraps it in an anonymous `TextBlock` that matches the app's global
   implicit `TextBlock` style — whose `Foreground` Setter outranks the `Foreground` the button
   would pass down by inheritance. Any control whose content wraps into a `TextBlock` (Button,
   CheckBox, TabItem header…) is at risk. Fix: give the control an explicit `ContentTemplate`
   with a `TextBlock` whose `Foreground` binds back to the control (a real local value).

3. **Button / TabItem / ComboBox default templates ignore `Background`.** A `Setter` for
   `Background`/`Foreground`/`BorderBrush` has no visible effect on these three — their default
   chrome reads system theme colors, not the property. `TextBox`/`ListBox`/`DataGrid` are fine.
   Fix: give them a full `ControlTemplate` that binds `Border.Background`/`BorderBrush` to
   `{TemplateBinding ...}`. Same applies to `Window`/`TabControl` chrome — prefer a local
   `Background` attribute on `<Window>` over relying on an implicit Style.

4. **Nested template `TemplateBinding` refers to the immediate control, one hop only.** In the
   ComboBox template, the inner `ToggleButton`'s `{TemplateBinding Background}` reads the
   *ToggleButton's* Background — not the ComboBox's. You must explicitly relay
   `Background="{TemplateBinding Background}"` onto the `<ToggleButton>` element in the outer
   template first.

5. **`<StaticResource ResourceKey=...>` aliasing silently fails here.** Aliasing one palette key
   to another via `<StaticResource x:Key="Foo" ResourceKey="AccentPrimaryBrush"/>` made
   `DynamicResource` bindings resolve empty. Just duplicate the literal `SolidColorBrush`/`Color`
   under each key instead of aliasing.

6. **The native title bar is not reachable from WPF.** It's drawn by DWM. `TitleBarColorizer`
   (P/Invoke `DwmSetWindowAttribute`, `DWMWA_CAPTION_COLOR`/`DWMWA_TEXT_COLOR`, Win11 22000+
   only) sets it; each window calls it from `SourceInitialized` and `ThemeService` re-applies it
   to every open window on a live theme change.

## Verifying theming changes

Don't trust that a Setter applied — visually confirm. Check both light and dark, at least one
non-default color scheme, a live Windows light/dark toggle, and (for re-templated controls like
ComboBox) that the control still *functions*, not just that it looks right. UI Automation can't
see tab-page content in this app — verify via screenshots.
