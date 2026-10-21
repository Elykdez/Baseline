# UI

## Prefab authoring

Author static panels, buttons, captions, tabs, layout defaults, fonts and colors in prefabs.
This is a project-wide requirement for shared controls, scene UI and demo content. Change the
saved prefab or scene, including its serialized responsive settings, rather than adding a runtime
styling helper. Runtime code must not impose literal palettes, font sizes/ranges, label padding,
button skins, panel dimensions or static anchors during startup, refresh or resize. Do not create
canvases, modal shields, labels, images or layout groups in a runtime fallback.

Runtime code binds actions, fills data and shows/hides authored elements. Dynamic lists instantiate
an authored row/control template; they should not rebuild a hierarchy with `new GameObject`,
`Image`, TMP labels and layout groups on every refresh. Editor-only prefab authoring tools may
create that structure and save it for inspection. Such tools must preserve existing customized
assets and must not be called as a player-time repair path.

State-dependent presentation reads authored data: selected/error/danger colors, Markdown styling,
icon insets and responsive breakpoints belong in prefab fields or the existing content definitions.
Typography belongs on the TMP components. Responsive scaling may scale their authored sizes;
it must not substitute a separate code-defined type scale. Runtime geometry is appropriate for
safe areas, content measurement, scrolling, dragging, progress, parallax and gameplay animation.
Definition-driven speaker art and accents are also data binding. Preserve these behaviors when
removing static overrides.

Start by inspecting `Assets/Bundles/Core/Prefab/UI`. It contains the starter UI, localized text,
conversation and alert prefabs; see [Dialogue](Dialogue.md). If a new feature needs a choice or
sheet template, author one with the relevant component and serialized references. Check actual
prefab assets and bindings before using a component.

Use nested prefabs/variants when shared styling should remain linked. Reuse cached list instances
or a panel-owned pool; reset data, parent, sibling order, selected/active state and listeners when
reusing them. A single-instance panel does not need a global pool. Destroy/return items when the
owning feature ends, and avoid adding duplicate button callbacks each time a panel opens.

## Component wiring

| Component | Integration detail |
| --- | --- |
| `UIChoice`, `UIOptionGroup` | Author the choice template and container. `SetOptions(...)` reuses instances; programmatic `Select(...)` does not raise `Changed`. |
| `UITabs` | Assign its options and localized labels; page order/count passed to `SetTabs(...)` must agree. |
| `UIPopupMenu` | `Show(anchor)` opens authored items. Bind fixed actions once with `Bind(Button, Action)`. Data-driven entries use a template/cache; literal labels need no active placeholder localizer. |
| `UIFoldout` | Inspect its persistence key and `ConfigSettingsPersistence` use; avoid unrelated foldouts sharing state by accident. |
| `UIRepeatPress`, `UIStepper` | Verify pointer-down, held repeats, release and bounds; a release after holding must not cause an extra action. |
| `UISheet`, `UISheetHandle` | Assign the sheet/handle references and test open, close, drag, resize and reopen with final geometry. |
| `UIStackedText`, `UIToast` | Assign authored references and test actual runtime content; these components do not supply a complete visual prefab. |
| `DialoguePanel`, `UICharacterDialog` | Use the shared catalog's authored prefabs. The scene owns conversation input and pause policy; alert requests are queued and cancelled on scene changes. See [Dialogue](Dialogue.md). |
| `UISafeArea`, `UIResponsiveGridLayout` | Inspect safe-area behavior and resize/reflow at the game's supported viewport sizes. |
| `UIParallax` | Assign aligned artwork layers, their depths and a shared aspect ratio. The reusable `ui_parallax.prefab` is installed behind the Index menu. |
| `UIDropdownLocalizer`, `UISliderValueLabel`, `UIJoystick`, `UIPointerEffect` | Retained reusable helpers; inspect their serialized contracts and the starter consumers before extending them. |

Source is in [the UI directory](../Assets/Scripts/Generics/ui). `BasicUITests` checks several
component behaviors; it does not prove that every newly authored prefab has correct references,
translations, click targets or layout.

`MarkdownText` is an internal static TMP-rich-text formatter with a `Format(...)` helper for
presentation code in the same assembly. It is not a component to attach or configure in the Inspector.

`IndexMenu.prefab` packages the starter UI. Its nested `ui_main.prefab` holds the title and tagline,
a top-right toolbar whose Help and Settings buttons `IndexManager` binds to the panels, the game
grid of `ui_gridButton` cards and an empty-state label that `IndexManager` shows while no game is
registered. Author the menu's look in these prefabs; the `IndexMenu` instances should only
override their own placement, name and active state.

`ui_config_panel.prefab` and `ui_help_panel.prefab` share one card layout: a title and close button,
`ui_tabs` over a divider, then the page. The dimmed backdrop also closes the panel. Each settings
page is a vertical list of rows; a row is a keyed `ui_text` label with its control as a child on the
right. A new setting copies an existing row, so it keeps the spacing, divider and control styling.

`ui_dialogue.prefab` owns its landscape/portrait layout settings, including illustrated lines and
required/closable actions, plus a paired layout and second portrait for dialogue definitions with
**Dual Characters** enabled. Author its active/listener tints, pair width/aspect thresholds and
portrait transforms on the prefab. Character defaults and per-line facing overrides are described
in [Dialogue](Dialogue.md#portrait-facing); the authored `UIPortrait` Images mirror their artwork
without changing layout or scale. Compact views select the active speaker without changing these
authored defaults. `UIRectLayout` stores native anchors and insets; edit these settings in
the prefab Inspector. `ui_character_dialog.prefab` owns its responsive settings, TMP typography,
action heights/spacing and separate primary, normal and destructive buttons. The alert canvas,
scaler, raycaster and input shield are authored in `ui_character_alerts.prefab`, which nests that
view and is referenced by `SharedUIContent`. There is no runtime button palette or canvas builder.

The shared-control audit also covers `UIChoice`, `UITabs`, `UIFoldout`, `UIPopupMenu`, `UIStepper`,
`UIToast` and help formatting. Author their serialized presentation fields when creating a prefab;
adding a component alone does not supply its visual configuration. Relay Run restores its authored
runner tint and reads the failure tint from the scene; its moving playfield and dust remain animation.

Rounded shapes use the sliced `Texture/Sprites/UI/ui_round.png` (20 px corners). Set the Image's
**Pixels Per Unit Multiplier** to `20 / radius`, for example 2.5 for an 8 px field or 2 for a 20 px
circle.

For new labels, instantiate `Assets/Bundles/Core/Prefab/UI/ui_text.prefab` through
`UILocalizedText.Create(...)`; retain its `LocalizeStringEvent`, `GameObjectLocalizer` and
`UILocalizationReady` components. The template's `LocalizeStringEvent` is disabled because code
enables it when it assigns a string. A label keyed in a prefab must enable it there; otherwise the
label keeps its fallback text in every language.

## Parallax backgrounds

`Assets/Bundles/Core/Prefab/UI/ui_parallax.prefab` is a reusable, masked backdrop. `IndexMenu` nests it
under `Canvas/Background`; menu controls and popups remain outside the moving layers. Its neutral
example uses the existing grid texture, soft lights and foreground markers. Customize a prefab
variant with your own art. The final readability overlay stays stationary and all decorative
graphics have **Raycast Target** disabled.

`UIParallax` accepts an ordered list of `Layer` entries with a `RectTransform target`, signed
`depth`, and optional `zoom`. Render order comes from sibling order in the hierarchy. Targets
must be direct children of the component's frame, with zero rotation; the component owns their
centered anchors, pivot, size, position and scale. Use `Image`, `RawImage`, or a container of
decorative children. Keep layout groups, safe-area drivers and interactive controls off these
targets. Transparent art plates should share one composition and aspect ratio.

Set `aspectRatio` to the source composition's width divided by height. The component cover-fits
the layers and reserves enough overscan for their maximum movement. `focalPoint` chooses the
part of the composition retained when a portrait or ultrawide viewport crops it: `(0, 0)` is
bottom-left, `(1, 1)` top-right. Crop positions retain a movement margin to prevent exposed edges.

Defaults use a 12-second figure-eight drift, 0.45-second smoothing and gentle pointer steering.
Depth is a fraction of the viewport: the sample uses `-0.00144`, `0.00864`, and `0.018` from rear
to front. Motion uses unscaled time, ignores pointers outside its own frame and released touches,
and recenters on disable. Set `driftAmplitude` to zero for pointer-only motion or
`pointerInfluence` to zero for idle motion only. `ApplyView(Vector2)` applies a normalized view
immediately for previews; disable the component when an external controller owns the view.

## Audit

Run affected `BasicUITests` cases and add focused checks for new authored contracts. Inspect supported viewport sizes and safe areas, mouse/touch input, tab/selection state, repeated open/close, and cached instance/listener reuse. Use the localization guide for text wiring and language acceptance.

When auditing UI changes, inspect runtime scripts across `Assets/Scripts` as well as their actual
prefab/scene consumers. Check hierarchy/component creation and writes to colors, TMP typography,
anchors, offsets, sizes and layout groups. Separate authored state/geometry calculations from
static appearance overrides; editor-only authoring and test fixtures are not runtime violations.
Verify that custom prefab settings survive initial display, repeated refresh, state changes,
orientation changes and locale changes. Check the complete catalog/reference chain after moving
runtime-created structure into an asset.

`UIParallaxTests` checks coverage at landscape, portrait, square and ultrawide sizes, extreme
depths/focal points, panel-relative pointer coordinates, released touch input, repeated activation
and the Index prefab's backdrop bindings. `UIParallaxRuntimeTests` verifies paused-time motion
and the Help/Settings controls through the Index scene in Play Mode.

Related: [Localization](Localization.md), [Audio](Audio.md), [Assets](Assets.md), [Utilities](Utilities.md).
