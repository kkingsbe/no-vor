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

## CI Build via GitHub Actions

The standard setup builds on Linux (`ubuntu-24.04`) **without a local game install** by reconstructing the expected `NuclearOptionRoot` layout from reference assemblies. Model your workflow on SITREP's `.github/workflows/release-build.yml`.

### How it works

1. **Checkout + `actions/setup-dotnet`** (`.NET 8.0.x`).
2. **Game reference assemblies** — the free Nuclear Option **dedicated server** (Steam app `3930080`) is downloaded via SteamCMD and provides `Assembly-CSharp.dll`, `Mirage.dll`, `Rewired_Core.dll`, UnityEngine modules, etc. Cache the managed folder (e.g. `~/nomod-managed`, key `nomod-managed-v1`). SteamCMD is flaky on CI — install it with `sudo add-apt-repository multiverse` + `sudo apt-get install steamcmd`, and retry `+app_update 3930080 validate` up to 3 times, clearing Steam's `appcache` between attempts. Free dedicated-server apps like this one are the community standard for CI refs (no paid game install needed).
3. **BepInEx core refs** — download the official `BepInEx_win_x64_5.4.23.4.zip` and extract `BepInEx.dll` + `0Harmony.dll` from `BepInEx/core/`.
4. **Reconstruct `NuclearOptionRoot`** — copy managed assemblies to `<temp>/game/NuclearOption_Data/Managed/`, BepInEx core to `<temp>/game/BepInEx/core/`, then set `NuclearOptionRoot=$temp/game` via `$GITHUB_ENV`. Your `.csproj` HintPaths then resolve exactly as they do locally.
5. **Build** — `dotnet build -c Release` for both the test harness (if any, as a compile smoke check of shared sources) and the plugin.
6. **Verify the distribution zip** — a quick script asserts required files exist (e.g. `Sitrep.dll`, native runtime dll, `espeak/espeak-ng.exe`).
7. **Publish** — `actions/upload-artifact@v4`, then `softprops/action-gh-release@v2` to attach the zip (only on tag pushes).

### Workflow wiring

- **Triggers**: `push: tags: ["v*"]` and `workflow_dispatch` (manual build without a release). `permissions: contents: write` is needed for the Release step.
- **Concurrency**: `group: release-build-${{ github.ref }}` with `cancel-in-progress: true` so only one build per ref runs.
- **Release vs artifact**: `softprops/action-gh-release` step should be guarded with `if: startsWith(github.ref, 'refs/tags/')` so manual runs only upload the artifact.
- The release zip should be produced by the build itself — a `CreateDistZip` MSBuild target (`AfterTargets="Build"`, `Configuration == Release`) that stages a flat, NOMM-compatible archive (see below). Native DLLs (e.g. Windows `onnxruntime.dll`) should be pulled from their NuGet package (`runtimes/win-x64/native/`) so a Linux CI ships the correct win-x64 binary.

## Publishing to NOMM (NOMNOM Registry)

[NOMM](https://github.com/Combat787/NOMM) is the community **mod manager** — a desktop app that downloads and installs Nuclear Option mods. [NOMNOM](https://github.com/KopterBuzz/NOMNOM) is its self-updating **package registry** (a GitHub repo of manifests). To be installable via NOMM a mod must be registered in a `modManifests/*.json` manifest in the NOMNOM repo; NOMM reads that registry to present installable mods and auto-updates them. The CI workflow above is the prerequisite that makes a mod publishable.

### Acceptance Policy (required to pass review)

- **Open-source mandate**: any custom DLL/executable must be open-source with no obfuscation. No malicious code, no unwarranted system changes.
- Releases must be **GitHub Releases** (NOMNOM auto-discovers them via the GitHub API).
- **One mod per repository** — no multi-mod release repos.
- The **first release asset** is the one NOMNOM uses. Multi-file mods must ship a compressed archive (zip/rar/7z).
- Tag must be a parseable version (`1.2.3`, `v1.2.3`, etc.) and the mod must target **BepInEx 5**.

### Release Requirements

The artifact `downloadUrl` points at a GitHub Release asset, and the manifest needs its `sha256:` digest (copy it from the Release page). A CI workflow that builds on `v*` tags and attaches a flat zip is the standard setup — the zip root should be what lands in `BepInEx/plugins/<id>/` (i.e. `YourMod.dll` + deps at the root, `espeak/`-style data folders included).

### Manifest Schema (`modManifests/<id>.json`)

Filename must match `id`; `id` should be the BepInEx plugin assembly name. Minimal example:

```json
{
  "id": "YourMod",
  "displayName": "Your Mod",
  "description": "What the mod does.",
  "tags": ["mod", "QoL"],
  "urls": [
    { "name": "info", "url": "https://github.com/Owner/YourMod" }
  ],
  "authors": ["Owner"],
  "githubOwner": "Owner",
  "githubRepoName": "YourMod",
  "autoUpdateArtifacts": "True",
  "artifacts": [
    {
      "fileName": "YourMod-1.0.0.zip",
      "version": "1.0.0",
      "category": "release",
      "type": "plugin",
      "gameVersion": "0.34",
      "downloadUrl": "https://github.com/Owner/YourMod/releases/download/v1.0.0/YourMod-1.0.0.zip",
      "hash": "sha256:<from release page>"
    }
  ]
}
```

Key fields:
- `autoUpdateArtifacts` — `"True"` (string) enables NOMNOM's hourly auto-update; new releases are picked up **without** re-submitting the manifest.
- `artifact.version` **must match the DLL metadata version** (NOMNOM validates it).
- `type`: `"plugin"` for BepInEx plugins; `"addOn"` for extensions (voice packs etc.) requiring an `extends` object.
- Optional: `dependencies`, `incompatibilities` (arrays of `{id, version}`).

### Submission

1. Fork `KopterBuzz/NOMNOM`, add `modManifests/<id>.json`.
2. PR to `main` — CI validates schema + content (incl. hash), then a human approves.
3. After merge, NOMM users can find/install the mod. Later updates = just push a new `v*` tag; the hourly auto-update refreshes the artifact.

## Existing Mod Repos (Reference)

| Mod | Author | Notable For |
|-----|--------|-------------|
| [SITREP](https://github.com/KopterBuzz/no-sitrep) | KopterBuzz | SteamCMD CI build (dedicated-server refs, cached), flat NOMM zip + release workflow |
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
| Mod Registry (NOMNOM) | https://github.com/KopterBuzz/NOMNOM |
| Mods Wiki | https://nuclearoptionmods.miraheze.org/ |

## Common Mistakes

- **Forgetting `HideManagerGameObject = true`** — BepInEx console won't work without it
- **Targeting IL2CPP** — Game uses Mono; all Harmony IL features and reflection are available
- **Skipping OnDestroy cleanup** — Acceptable for process-scoped mods, but blocks live reload (ScriptEngine/F6 requires it)
- **Dynamic method scanning over attributes** — SITREP's approach is unique; `PatchAll()` with `[HarmonyPatch]` is simpler and the community standard
