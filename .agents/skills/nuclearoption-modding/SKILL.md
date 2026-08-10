---
name: nuclearoption-modding
description: Use when creating or modifying BepInEx mods for Nuclear Option, setting up a mod development environment, patching game methods via Harmony, or referencing game assembly types and community mod patterns.
---

# Nuclear Option Modding

## Overview

Nuclear Option is a Unity 2022.3.6f1 **Mono** game (not IL2CPP), making it fully moddable via BepInEx 5 + Harmony runtime patching. No official code-modding API exists — all mods work by hooking `Assembly-CSharp.dll` methods at runtime.

## When to Use

- Creating a new BepInEx plugin for Nuclear Option
- Setting up project references to game assemblies (Assembly-CSharp, Mirage, Rewired_Core)
- Writing Harmony patches for the first time
- Needing to access private/internal game fields or methods
- Looking for existing mods to reference for patterns

## Setup

### BepInEx Installation

1. Download [BepInEx 5 Mono from Thunderstore](https://thunderstore.io/c/nuclear-option/p/BepInEx/BepInExPack/)
2. Extract to game root directory
3. Set `BepInEx/config/BepInEx.cfg` → `Chainloader.HideGameManagerObject = true`
4. Place plugin `.dll` in `BepInEx/plugins/`

### Project Template (SDK-style .csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BepInEx.PluginInfoProps" Version="2.1.0" />
    <PackageReference Include="UnityEngine.Modules" Version="2022.3.6" />
  </ItemGroup>
  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(NuclearOptionRoot)\NuclearOption_Data\Managed\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Mirage">
      <HintPath>$(NuclearOptionRoot)\NuclearOption_Data\Managed\Mirage.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="BepInEx">
      <HintPath>$(NuclearOptionRoot)\BepInEx\core\BepInEx.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(NuclearOptionRoot)\BepInEx\core\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Create a `Directory.Build.props` for `$(NuclearOptionRoot)` path, and a local `Directory.Build.user.props` override. Use `BepInEx.AssemblyPublicizer.MSBuild` with `Publicize="true"` on `Assembly-CSharp` to bypass internal/private member restrictions.

### Plugin Entry Point

```csharp
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    internal static Plugin Instance { get; private set; }
    private Harmony _harmony;

    private void Awake()
    {
        Instance = this;
        Logger = base.Logger;
        _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        _harmony.PatchAll();
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }
}
```

## Harmony Patch Patterns

### PatchAll() with attributes (recommended)

```csharp
[HarmonyPatch(typeof(Pilot), nameof(Pilot.Fire))]
internal static class PilotFirePatch
{
    private static bool Prefix(Pilot __instance)
    {
        return false; // Skip original method
    }
}
```

### Patch Types

| Type | Returns | Use Case |
|------|---------|----------|
| **Prefix** | `void` or `bool` | Modify args or skip original (`return false`) |
| **Postfix** | `void` | Read return values, side effects after original |
| **Transpiler** | `IEnumerable<CodeInstruction>` | Replace IL instructions within a method body |
| **Prefix (skip)** | `return false` | Completely replace original method logic |

### Private Field Access

Prefer `AccessTools.FieldRefAccess<TType, TField>("fieldName")` (zero-allocation) over reflection for hot paths.

### Guard Clause Pattern

```csharp
private static void Postfix(Player __instance, FactionHQ newHQ)
{
    if (__instance == null || !__instance.IsLocalPlayer) return;
}
```

## Common Game Types

| Namespace | Key Types |
|-----------|-----------|
| `NuclearOption.Networking` | `Player`, `BasePlayer`, `MissionManager` |
| `(root)` | `Pilot`, `PilotPlayerState`, `Aircraft`, `ChatManager` |
| `(root)` | `WeaponManager`, `FlightHud`, `CombatHUD` |
| `(root)` | `GameManager`, `SceneSingleton<T>`, `UnitRegistry` |
| `(root)` | `CountermeasureManager`, `Encyclopedia` |

No namespace `NuclearOption.Chat` exists — `ChatManager` is at root level.

## UI Rendering

All mod UI renders **inside the Unity game window** — no mod uses an external transparent overlay.

### IMGUI (Settings/Config Panels)

```csharp
void OnGUI()
{
    if (!showWindow) return;
    GUI.Window(1, rect, DrawWindow, "Settings");
}

void DrawWindow(int id)
{
    GUILayout.Label("Volume: " + volume);
    volume = GUILayout.HorizontalSlider(volume, 0f, 1f);
    if (GUILayout.Button("Close")) showWindow = false;
    GUI.DragWindow();
}
```

Toggle with `KeyboardShortcut` config binding. Used by WeatherController (F6), no-autopilot-mod (F8).

### uGUI (HUD Overlays)

Create persistent `GameObject` children on the game's HUD canvas. Supports rich styling, alpha transparency, and efficient rendering.

```csharp
// Parent to the game's HUD center transform
var canvas = SceneSingleton<FlightHud>.i.GetHUDCenter();
var label = new GameObject("Label");
label.transform.SetParent(canvas, false);
var text = label.AddComponent<Text>();
text.text = "Hello";
text.color = new Color(0.2f, 1f, 0.6f, 0.9f); // green with alpha
```

For a standalone overlay not tied to the game HUD:

```csharp
var go = new GameObject("OverlayCanvas", typeof(Canvas), typeof(CanvasScaler));
go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
DontDestroyOnLoad(go);
```

**Canvas targets**:
- HUD center: `SceneSingleton<FlightHud>.i.GetHUDCenter()`
- Aircraft extras: `AircraftDefinition.aircraftParameters.HUDExtras.transform`
- New standalone: `new GameObject("Canvas", typeof(Canvas))`

**Used by**: MissileETA (ETA labels, off-screen arrows, backdrops with alpha), no-autopilot-mod (fuel/AP status), HUDCountermeasureComp

## Config Pattern

```csharp
var setting = Config.Bind("Section", "Key", defaultValue,
    new ConfigDescription("description",
        new AcceptableValueRange<float>(0f, 100f)));
```

For keybindings:

```csharp
var key = Config.Bind("Hotkeys", "ActionKey", new KeyboardShortcut(KeyCode.F5));
```

## Existing Mod Repos (Reference)

| Mod | Author | Notable For |
|-----|--------|-------------|
| [no-autopilot-mod](https://github.com/qwerty1423/no-autopilot-mod) | qwerty1423 | Most complex mod (824 commits). Docker build, AssemblyPublicizer, thorough OnDestroy cleanup, PID profiles |
| [NuclearMods](https://github.com/nikkorap/NuclearMods) | nikkorap | 10+ mod collection. Canonical `Config.Bind()` usage, source-gen PluginInfo |
| [NuclearOptionSDK](https://github.com/Mursisru/NuclearOptionSDK) | Mursisru | WebSocket bridge, Roslyn REPL, scene explorer |
| [HoldToLaunch](https://github.com/Mursisru/HoldToLaunch) | Mursisru | Clean Harmony per-file `Patches/` organization |
| [NOBlackBox](https://github.com/KopterBuzz/NOBlackBox) | KopterBuzz | No Harmony — uses Unity events + polling. Dual BepInEx 5/6 support |
| [NuclearOptionMiscMods](https://github.com/Assassin1076/NuclearOptionMiscMods) | Assassin1076 | Transpiler IL rewrite examples, InputFramework usage |

**Most prolific author**: [Mursisru](https://github.com/Mursisru) (15+ repos)

## Community

| Resource | URL |
|----------|-----|
| Official Discord | https://discord.gg/nuclear-option |
| Thunderstore | https://thunderstore.io/c/nuclear-option/ |
| Mod Manager (NOMM) | https://github.com/Combat787/NOMM |
| Mods Wiki | https://nuclearoptionmods.miraheze.org/ |

## Common Mistakes

- **Forgetting `HideManagerGameObject = true`** — BepInEx console won't work without it
- **Targeting IL2CPP** — Game uses Mono; all Harmony IL features and reflection are available
- **Skipping OnDestroy cleanup** — Acceptable for process-scoped mods, but blocks live reload (ScriptEngine/F6 requires it)
- **Dynamic method scanning over attributes** — SITREP's approach is unique; `PatchAll()` with `[HarmonyPatch]` is simpler and the community standard
