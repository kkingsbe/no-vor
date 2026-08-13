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
| `Core/CdiData.cs` | live HSI/CDI flight data |
| `Core/NavModels.cs` | course/sort modes and airport/runway models |
| `Core/CdiScale.cs` | range-based CDI scaling, off-scale state, and intercept guidance |
| `UI/CockpitHud.cs` | single owner of both cockpit surfaces (block and native tape cues) |
| `UI/HudGlyphs.cs` | shared cockpit font, outline, and glyph construction |
| `UI/CdiInstrument.cs` | green cockpit HUD CDI block (linear cues and text rows) |
| `UI/NavPanel.cs` | landscape airport/course panel |
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
- Direct-to cockpit guidance uses separate bearing and drift-corrected steering cues; it does not drive the CDI bar with heading error.
- `CdiScale` owns CDI sensitivity: 5 NM enroute, 1 NM within 30 NM, and 0.3 NM within 2 NM, with hysteresis so the scale does not flutter at a threshold. `Navigation.AutoScaleCdi` disables it and falls back to `Navigation.FullDeflectionNauticalMiles`.
- MANUAL cockpit guidance uses a range-scaled CDI. Positive cross-track means the aircraft is right of course; the needle always moves toward the desired course. When cross-track exceeds full scale the needle is suppressed, one amber edge arrow lights, and the action line commands an intercept heading capped by `Navigation.MaxInterceptDegrees`.
- Both cockpit surfaces share one off-scale convention: the moving element is suppressed or clamped and a filled amber arrow marks the side.
- The cockpit block annunciates the active scale (`ENR`/`TERM`/`APP`/`FIX`), selected course or bearing, and ETA with groundspeed.
- The course caret and distinct steering diamond are children of `FlightHud`'s native `compass` RawImage; their positions use the native tape's current UV span and rect width.
- The MANUAL CDI labels its nautical-mile full scale, separates TO/FROM from prose, and replaces a pegged needle with an amber edge flag when cross-track is off scale.
- All cockpit HUD graphics use black outlines for sky contrast, and the default navigation block sits 180 px below HUD center.
- Navigation distances, runway length, and groundspeed use nautical miles and knots. Field elevation remains metric (meters).

## Default Hotkeys

- `N` / `B`: next / previous airport
- `C`: toggle cockpit HUD CDI
- `F9`: toggle navigation panel
- `[` / `]`: decrease / increase manual course
- `\`: set manual course direct to the selected field's current bearing
- `Ctrl+Arrow`: nudge cockpit HUD CDI position

## Commit Style

Use short conventional-style subjects such as `feat:`, `fix:`, `tune:`, `polish:`, and `chore:`.
