# AGENTS.md

## Project Overview

**NO VOR CDI** (`NOVor.dll`) is a BepInEx 5 plugin for **Nuclear Option** that adds a VOR-style cockpit HUD CDI plus a searchable navigation panel with a heading-up HSI, airport ownership, runway courses, and live diversion telemetry.

- Plugin GUID: `com.novor.cdi`; version comes from `MyPluginInfo.cs`.
- Nuclear Option is Unity 2022.3.6 Mono, not IL2CPP.
- Integration uses polling and runtime object discovery, with no Harmony patches.
- UI is code-built uGUI. The cockpit HUD uses legacy `UnityEngine.UI.Text`; the panel uses TextMeshPro.

## Tech Stack

- C# targeting `net472`, `LangVersion latest`, `AllowUnsafeBlocks=true`.
- Game references resolve through `$(NuclearOptionRoot)` in `Directory.Build.props` / `Local.props`.
- Main references: `Assembly-CSharp`, `Mirage`, `UnityEngine.*`, `UnityEngine.UI`, `Unity.TextMeshPro`, `BepInEx`, and `0Harmony`.
- The navigation math harness targets .NET 8 and has no package dependencies.

## Build and Test Cycle

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
dotnet build NOVor.csproj -c Debug
```

- The harness must report all angle, CDI, steering, and ETA checks passing.
- Release and Debug builds must complete with 0 errors.
- Debug auto-deploys `NOVor.dll` and its PDB to `$(NuclearOptionRoot)\BepInEx\scripts`.
- Never deploy `NOVor.dll` to `BepInEx\plugins`; ScriptEngine is the single loader.
- Hot reload occurs through ScriptEngine's watcher or the Insert key.

## Code Layout

| Path | Responsibility |
|---|---|
| `Plugin.cs` | BepInEx entry point, config, lifecycle |
| `Core/NavController.cs` | game-state polling, model population, UI ownership |
| `Core/NavMath.cs` | dependency-free angle, deviation, steering, and ETA math |
| `Core/GuidanceMath.cs` | dependency-free active command-heading selection |
| `Core/RunwayGuidance.cs` | dependency-free 3 NM runway capture, rollout, and phase policy |
| `Core/CockpitPresentation.cs` | dependency-free cockpit text and visibility state |
| `Core/CdiData.cs` | live HSI/CDI flight data |
| `Core/NavModels.cs` | course/sort modes and airport/runway models |
| `Core/CdiScale.cs` | range-based CDI scaling, off-scale state, and intercept guidance |
| `UI/CockpitHud.cs` | single owner of both cockpit surfaces (block and native tape cues) |
| `UI/HudGlyphs.cs` | shared cockpit font, outline, and glyph construction |
| `UI/HudCueIcon.cs` | procedural course and command shapes for the native heading tape |
| `UI/CdiInstrument.cs` | green cockpit HUD CDI block (linear cues and text rows) |
| `UI/NavPanel.cs` | landscape airport/course panel |
| `UI/KeyBindingPanel.cs` | in-panel keyboard/HOTAS binding capture and Controls UI |
| `UI/PanelHsi.cs` | compass card, pointers, CDI bar, and TO/FROM output |
| `UI/HsiCourseSelector.cs` | HSI twist-drag and scroll input |
| `UI/UiColors.cs` | native dark/amber panel chrome and green HUD colors |
| `UI/TextureFactory.cs` | procedural framed sprites |
| `UI/FontLoader.cs` | TextMeshPro font resolution |
| `UI/WindowDragHandler.cs` | panel drag and screen clamping |
| `Integrations/ModBarBridge.cs` | optional NoModBar reflection bridge |

## Conventions

- Namespaces are `NOVor`, `NOVor.Core`, `NOVor.UI`, and `NOVor.Integrations`.
- Do not add code comments; prefer clear names and small methods.
- Config is exposed as static `ConfigEntry<T>` fields on `Plugin`.
- The panel owns no game assembly types. It consumes Core models and emits C# events consumed by `NavController`.
- Optional integrations must fail safely through reflection and exception guards.
- The standalone panel uses dark neutral chrome and amber state. The independent cockpit HUD remains phosphor green.
- Panel toolbars and tab rows are fixed-height; only the main body and HSI well may expand vertically.
- Panel instrument surfaces are opaque, active filters use thin amber rails, and the HSI labels direct-to guidance as bearing rather than course.
- Preserve complete cleanup for hot reload: restore cursor/camera state and destroy owned UI/EventSystem objects.
- `NavController` owns exactly one cockpit component (`CockpitHud`); per-surface construction, visibility, and teardown live inside it.
- Cockpit glyphs are built through `HudGlyphs` so font resolution, outlining, and off-scale symbology stay identical across surfaces.

## Game API Patterns

- Local aircraft: `GameManager.GetLocalAircraft(out Aircraft)`.
- Position/heading/velocity: `_aircraft.rb.transform` and `_aircraft.rb.velocity`.
- Airbases: `Object.FindObjectsOfType<Airbase>()`, excluding disabled or centerless entries.
- Ownership: `Airbase.CurrentHQ.faction`; friendly matches `_aircraft.Player.HQ.faction`.
- Mobile fields: `Airbase.AttachedAirbase`; target velocity comes from `Airbase.Runway.GetVelocity()`.
- Runway heading/name/length: `GetDirection`, `GetName`, and `Length`.
- `NavMath` owns TO/FROM, signed cross-track, steering-error, drift-corrected heading, and relative-closure ETA semantics.
- `GuidanceMath` owns the active command heading. Direct mode commands the drift-corrected field bearing; manual mode commands the drift-corrected proportional intercept track and converges to the selected course on centerline.
- `CdiData.CommandHeading` is the single command consumed by the panel `STEER` readout, cockpit command line, and amber heading-tape diamond. `Course` and `Bearing` are context, not competing commands.
- `CdiScale` owns CDI sensitivity: 5 NM enroute, 1 NM within 30 NM, and 0.3 NM within 2 NM, with hysteresis so the scale does not flutter at a threshold. `Navigation.AutoScaleCdi` disables it and falls back to `Navigation.FullDeflectionNauticalMiles`.
- MANUAL cockpit guidance uses a range-scaled CDI. Positive cross-track means the aircraft is right of course; the needle always moves toward the desired course. When cross-track exceeds full scale the needle is suppressed, one amber block edge flag lights, and the shared command heading uses the intercept angle capped by `Navigation.MaxInterceptDegrees`.
- The cockpit block presents target/range, course context, active command, and support telemetry in that order. TO/FROM appears once, and CDI full scale is labeled as a plus/minus distance.
- The procedural course triangle and command diamond are children of `FlightHud`'s native `compass` RawImage; their positions use the native tape's current UV span and rect width.
- All cockpit HUD graphics use black outlines for sky contrast, and the default navigation block sits 180 px below HUD center.
- Navigation math, CDI scaling, and config inputs remain nautical miles/knots internally. `Navigation.DisplayUnits` controls user-facing range, CDI full-scale distance, runway length, and groundspeed text (`Aviation` = NM/KT, `Metric` = km/km/h). Field elevation remains meters.
- The heading tape preserves semantic identity on and off scale: the green outlined triangle is course/bearing context and the amber solid diamond is the active command. Edge clamping never recolors or substitutes either cue. Cue motion is display-only and bounded by `Hud.HeadingCueResponseDegreesPerSecond`.
- Selecting a runway preserves its label and directional threshold and enters `CourseMode.Runway`; course nudges rotate that runway's inbound approach line about the threshold without discarding runway identity.
- Runway guidance targets the inbound extended centerline and plans capture by the fixed 3 NM final gate. Intercept angle uses remaining along-track distance; rollout distance scales with groundspeed; established-state hysteresis prevents cue chatter.
- On the heading tape, the outlined green triangle is selected runway course and the amber diamond is the drift-corrected aircraft heading to fly. Align the native current-heading marker with the amber diamond. Wind may keep the two navigation cues separated while established.
- Runway capture is lateral-only. No glidepath or aircraft-control automation is provided.

## Default Hotkeys

- `N` / `B`: next / previous airport
- `C`: toggle cockpit HUD CDI
- `F9`: toggle navigation panel
- `[` / `]`: decrease / increase manual course
- `\`: set manual course direct to the selected field's current bearing
- `Ctrl+Arrow`: nudge cockpit HUD CDI position
- Select `CFG` in the navigation panel to view, clear, or rebind controls to keyboard keys/chords or HOTAS buttons.

## Controls Input

- The Controls screen persists keyboard binds through the existing BepInEx `[Hotkeys]` `KeyboardShortcut` entries and HOTAS binds through `[Hotas]` string entries (`deviceName|deviceGuid|deviceId|buttonIndex`).
- Keyboard-main bindings retain BepInEx exact-chord behavior. HOTAS bindings are captured and polled through Rewired (`ReInput.controllers`), trigger on their button down edge, and coexist with the keyboard bind on the same action (either triggers).
- Capture listens for a keyboard chord or a Rewired joystick button press. Legacy `KeyCode.Joystick*` values are excluded from keyboard capture because the game's Rewired native input never surfaces them through `UnityEngine.Input`.
- Analog axes and POV hats are not supported.

## Commit Style

Use short conventional-style subjects such as `feat:`, `fix:`, `tune:`, `polish:`, and `chore:`.
