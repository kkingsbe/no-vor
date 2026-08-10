# AGENTS.md

## Project Overview

**NO VOR CDI** (`NOVor.dll`) is a BepInEx 5 plugin for **Nuclear Option** that adds a VOR-style CDI (Course Deviation Indicator) instrument to the HUD plus a searchable, draggable navigation panel for selecting airports and dialing a manual course (CRS) with TO/FROM logic.

- Plugin GUID: `com.novor.cdi`, name: `NO VOR CDI`, version from `MyPluginInfo.cs`.
- Game is Unity 2022.3.6 **Mono** (not IL2CPP) — all integration is runtime patching/polling, no official API.
- Mod UI is entirely uGUI built in code (no prefabs/assets); the CDI instrument uses legacy `UnityEngine.UI.Text`, the nav panel uses TextMeshPro (`TMPro`).

## Tech Stack & References

- C# targeting `net472`, `LangVersion latest`, `AllowUnsafeBlocks=true`, no external NuGet runtime deps.
- References resolved from the game install via `$(NuclearOptionRoot)` (see `Directory.Build.props` / `Local.props`, default `D:\SteamLibrary\steamapps\common\Nuclear Option`): `Assembly-CSharp`, `Mirage`, `UnityEngine.*`, `UnityEngine.UI`, `Unity.TextMeshPro`, `BepInEx`, `0Harmony`.
- `Plugin.cs` is a `BaseUnityPlugin` that binds config, spawns a persistent `NavController` GameObject, and performs no Harmony patches (all game data read via polling/`FindObjectsOfType`).

## Build & Test Cycle

There is **no unit-test harness** (game-dependent Mono plugin). Verification cycle is:

1. `dotnet build -c Debug` — must compile with 0 errors.
2. Manual in-game verification.
3. Commit with a conventional-style message (`feat:`, `fix:`, `tune:`, `polish:`, `chore:`).

### Deployment

- Debug builds **auto-deploy** `NOVor.dll`/`.pdb` to `$(NuclearOptionRoot)\BepInEx\scripts` via the `DeployToScripts` target in `NOVor.csproj`.
- **Never** copy `NOVor.dll` into `BepInEx/plugins/` — ScriptEngine is the single loader. `deploy.ps1` builds Debug, then deletes any stale `plugins\NOVor.dll`.
- Hot reload: ScriptEngine's file watcher reloads ~3s after a Debug build, or press `Insert` in-game. Debug builds don't require a game restart.

## Code Layout & Conventions

| Path | Contents |
|------|----------|
| `Plugin.cs` | BepInEx entry point, config binding, lifecycle |
| `Core/NavController.cs` | MonoBehaviour: polls game state, computes nav data, owns instrument/panel |
| `Core/CdiData.cs` | Nav data struct fed to the CDI instrument |
| `Core/NavModels.cs` | `CourseMode` enum (`Auto`/`Manual`) and `AirportInfo` struct |
| `UI/CdiInstrument.cs` | CDI HUD overlay (legacy `Text`), attached under the game HUD center |
| `UI/NavPanel.cs` | Standalone ScreenSpaceOverlay panel (TMPro), event-driven, decoupled from game types |
| `UI/UiColors.cs` | Green-phosphor HUD palette |
| `UI/TextureFactory.cs` | Procedural panel background + list fade sprites |
| `UI/FontLoader.cs` | `TMP_FontAsset` resolution |
| `UI/WindowDragHandler.cs` | Panel drag/clamp component |
| `Integrations/ModBarBridge.cs` | Optional NoModBar API bridge via reflection (never throws) |

- Namespaces: `NOVor`, `NOVor.Core`, `NOVor.UI`, `NOVor.Integrations`.
- **Repo convention: no code comments.** Write comment-free code; prefer clear naming and small methods.
- Config lives in `Plugin.cs` as `static ConfigEntry<...>` fields accessed statically (`Plugin.HudOffsetY.Value`), registered with `Config.Bind` under sections `Hotkeys`, `Navigation`, `Hud`, `Panel`. Use `KeyboardShortcut` for keybindings and `AcceptableValueRange` for numeric ranges.
- The CDI instrument is parented to `SceneSingleton<FlightHud>.i.GetHUDCenter()`; the nav panel is an independent `ScreenSpaceOverlay` canvas with `CanvasScaler` 1920x1080, sortingOrder 150.
- The panel owns no game-type references — it exposes C# events (`AirportSelected`, `ModeChanged`, `CourseAdjusted`, etc.) consumed by `NavController`.
- Optional integrations must degrade gracefully (reflection resolution + try/catch, return `false` when unavailable).

## Game API Patterns in Use

- Local aircraft: `GameManager.GetLocalAircraft(out Aircraft)`; position/heading from `_aircraft.rb.transform.position` and `.eulerAngles.y`.
- Airbases: `Object.FindObjectsOfType<Airbase>()`, filtered on `ab.disabled` and `ab.center != null`; clean names by stripping `"(Clone)"`.
- Bearing math: `Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg`, normalized to 0–360; deviation via `Mathf.DeltaAngle(course, heading)`; deflection clamped to `deviation / FullDeflectionDeg` in [-1, 1]; TO vs FROM via `Mathf.Abs(DeltaAngle(manualCourse, bearing)) <= 90`.
- Reference the `nuclearoption-modding` skill (`.agents/skills/nuclearoption-modding/SKILL.md`) for deeper game-modding context (BepInEx/Harmony, common types, pitfalls).

## Hotkeys (defaults, from config)

- `N` / `B` — next / previous airport
- `C` — toggle CDI HUD
- `F9` — toggle nav panel
- `[` / `]` — decrease / increase manual course
- `Ctrl+Arrow` — nudge CDI instrument position (persisted to `Hud` config)

## Commit Style

Conventional-ish short subjects from current history, e.g. `feat: register NO-VOR nav panel with the mod bar`, `tune: default CDI offset to Y=180`, `polish: fix title wrap`, `merge: ...`.
