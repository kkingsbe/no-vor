# Native Nav Panel and HSI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign NO VOR's airport panel as a compact, native-looking Nuclear Option avionics page that replaces the course knob with a useful HSI/CDI and adds ownership, mobile-field, runway, elevation, ETA, and steering information.

**Architecture:** Keep `NavController` as the only game-facing adapter and feed plain navigation models into the UI. Extract course/deviation/ETA calculations into a dependency-free `NavMath` core that can be tested outside Unity. Replace the portrait `NavPanel` course deck with a two-column landscape layout and a reusable panel HSI; leave the existing cockpit HUD CDI independent and green because it belongs to the HUD layer rather than the game's menu chrome.

**Tech Stack:** C# / .NET Framework 4.7.2, BepInEx 5, Unity 2022.3 uGUI, TextMeshPro, game `Airbase`/`Aircraft`/`Faction` APIs, dependency-free .NET 8 console test harness.

---

## Product and design decisions

The subject is an in-flight diversion and course-selection instrument for Nuclear Option pilots. Its single job is to answer, at a glance: **which usable field am I navigating to, and how do I fly the selected course?**

### Visual tokens

Use these panel tokens in `UiColors`; do not recolor the separate cockpit HUD CDI:

| Token | Value | Purpose |
|---|---:|---|
| `Chrome` | `#101312` at 92% alpha | translucent panel body |
| `ChromeRaised` | `#191D1C` at 96% alpha | header, fields, inactive controls |
| `Rule` | `#3B403E` | one-pixel dividers and outlines |
| `Amber` | `#E3A64B` | active state, HSI course pointer, focus |
| `Text` | `#D8DDD9` | primary labels and data |
| `Muted` | `#8B928E` | captions and inactive values |

Faction chips use the live `Faction.color`; no invented red/blue mapping. The panel stays square-cornered, flat, and thin-bordered like Nuclear Option's VOR tab. The one expressive element is the heading-up HSI.

### Layout

Reference size: `820 x 430` at the existing `1920 x 1080` canvas reference resolution.

```text
+ NAV / CDI ------- Sandrift Airbase  128°  9.8 NM ------------------ [–] [×] +
| SEARCH FIELDS...       [NEAR | A–Z] [FRIENDLY] | [ AUTO | MANUAL ]          |
| FIELD                 BRG      NM                |          184°           |
| ▌ Dustbowl Strip        350     0.2               |       heading-up HSI      |
| ▌ Sandrift Airbase      128     9.8               |   course / CDI / TO-FROM  |
| ▌ K92 Highway Strip     015    11.3               |                           |
| ▌ Hyperion Carrier MOV  214    17.3          | SET BRG  SET HDG  RECIP     |
| ...                         [scrollbar]          | RWY 18  184°  2.4 km    |
|                                                  | ELEV 74 m  ETA 02:41         |
|                                                  | STEER 137°  GS 221 kt    |
+--------------------------------------------------+---------------------------+
```

The header is the canonical selected-target readout and remains visible when collapsed. The selected list row naturally identifies the same item, but the old bottom target sentence is removed. TO/FROM is derived navigation output inside the HSI, never a mode button.

### Interaction grammar

- Filled amber with dark text always means a persistent selection: `AUTO`, `MANUAL`, selected runway, active sort, or friendly-only filter.
- Dark outlined buttons always mean a momentary action: `SET BRG`, `SET HDG`, `RECIP`.
- Airport selection uses an amber left rail plus stronger text, not the same full-fill treatment as toggles.
- Runway buttons are mutually exclusive when the selected course is within `1°` of a runway heading.
- Mouse wheel or twist-drag on the HSI changes course and switches to manual mode. The top lubber line never moves.
- `NEAR` and `A–Z` are sort modes. `NEAREST` is removed as an ambiguous verb.
- The thin native scrollbar is always visible when content overflows; edge fades are removed.

## File map

| Path | Responsibility | Change |
|---|---|---|
| `Core/NavMath.cs` | dependency-free angle, deviation, steering, and ETA math | create |
| `Core/NavModels.cs` | airport/runway/telemetry data passed to UI | modify |
| `Core/CdiData.cs` | HSI/CDI flight data | modify |
| `Core/NavController.cs` | read game state and populate models | modify |
| `UI/HsiCourseSelector.cs` | pointer drag/scroll course input | create |
| `UI/PanelHsi.cs` | build and update the panel HSI | create |
| `UI/NavPanel.cs` | landscape layout, list/filter/sort, details, control states | modify substantially |
| `UI/UiColors.cs` | split native panel chrome from HUD colors | modify |
| `UI/TextureFactory.cs` | retain framed sprite; remove knob/fade assets | modify |
| `UI/CourseKnob.cs` | superseded input component | delete |
| `Plugin.cs` | add persisted filter/sort defaults only | modify |
| `Integrations/ModBarBridge.cs` | change user-facing label from VOR pun | modify |
| `tests/NavMathHarness/NavMathHarness.csproj` | package-free test executable | create |
| `tests/NavMathHarness/Program.cs` | table-driven core math checks | create |
| `AGENTS.md` | document new layout/components and verification command | modify |

The repository currently has intentional uncommitted work in `Core/NavController.cs`, `Core/NavModels.cs`, `UI/NavPanel.cs`, `UI/TextureFactory.cs`, and new `UI/CourseKnob.cs`. Treat that working tree as the implementation baseline. Do not reset or check out those files; the tasks below replace the knob prototype while retaining its runway extraction and camera-input fixes.

---

### Task 1: Lock the navigation math contract with a test harness

**Files:**
- Create: `Core/NavMath.cs`
- Create: `tests/NavMathHarness/NavMathHarness.csproj`
- Create: `tests/NavMathHarness/Program.cs`

- [ ] **Step 1: Create a package-free harness project**

Create `tests/NavMathHarness/NavMathHarness.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="..\..\Core\NavMath.cs" Link="Core\NavMath.cs" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the failing table-driven checks**

Create `tests/NavMathHarness/Program.cs`:

```csharp
using System;
using NOVor.Core;

internal static class Program
{
    private static int _failures;

    private static void Main()
    {
        Equal(359d, NavMath.NormalizeDegrees(-1d), "normalize negative");
        Equal(1d, NavMath.NormalizeDegrees(361d), "normalize overflow");
        True(NavMath.IsToStation(0d, 10d), "ten degrees is TO");
        True(!NavMath.IsToStation(0d, 190d), "reciprocal is FROM");
        Equal(10d, NavMath.CourseDeviationDegrees(0d, 10d), "TO deviation");
        Equal(10d, NavMath.CourseDeviationDegrees(0d, 190d), "FROM deviation");
        Equal(110d, NavMath.DriftCorrectedHeadingDegrees(100d, 90d, 80d), "left drift correction");
        Equal(90d, NavMath.DriftCorrectedHeadingDegrees(100d, 90d, 100d), "right drift correction");
        Equal(30d, NavMath.EtaSeconds(1852d, 1852d / 30d), "eta from closure");
        True(double.IsNaN(NavMath.EtaSeconds(1852d, 0.5d)), "no useful closure");

        if (_failures > 0)
            Environment.Exit(1);
        Console.WriteLine("NavMathHarness: 10 passed");
    }

    private static void Equal(double expected, double actual, string name)
    {
        if (Math.Abs(expected - actual) <= 0.001d) return;
        Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
        _failures++;
    }

    private static void True(bool value, string name)
    {
        if (value) return;
        Console.Error.WriteLine($"FAIL {name}");
        _failures++;
    }
}
```

- [ ] **Step 3: Run the harness and verify it fails to compile**

Run:

```powershell
dotnet run --project tests\NavMathHarness\NavMathHarness.csproj -c Release
```

Expected: compilation fails because `NOVor.Core.NavMath` does not exist.

- [ ] **Step 4: Implement the pure math**

Create `Core/NavMath.cs`:

```csharp
using System;

namespace NOVor.Core
{
    public static class NavMath
    {
        public static double NormalizeDegrees(double value)
        {
            value %= 360d;
            return value < 0d ? value + 360d : value;
        }

        public static double DeltaAngleDegrees(double from, double to)
        {
            double delta = NormalizeDegrees(to - from);
            return delta > 180d ? delta - 360d : delta;
        }

        public static bool IsToStation(double course, double bearingToStation)
        {
            return Math.Abs(DeltaAngleDegrees(course, bearingToStation)) <= 90d;
        }

        public static double CourseDeviationDegrees(double course, double bearingToStation)
        {
            double reference = IsToStation(course, bearingToStation) ? course : course + 180d;
            return DeltaAngleDegrees(reference, bearingToStation);
        }

        public static double DriftCorrectedHeadingDegrees(double bearingToStation, double heading, double groundTrack)
        {
            double drift = DeltaAngleDegrees(heading, groundTrack);
            return NormalizeDegrees(bearingToStation - drift);
        }

        public static double EtaSeconds(double distanceMeters, double closureMetersPerSecond)
        {
            return distanceMeters >= 0d && closureMetersPerSecond > 1d
                ? distanceMeters / closureMetersPerSecond
                : double.NaN;
        }
    }
}
```

- [ ] **Step 5: Run both checks**

Run:

```powershell
dotnet run --project tests\NavMathHarness\NavMathHarness.csproj -c Release
dotnet build NOVor.csproj -c Release
```

Expected: `NavMathHarness: 10 passed`, then `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Commit**

```powershell
git add Core\NavMath.cs tests\NavMathHarness
git commit -m "test: define navigation math contract"
```

---

### Task 2: Enrich the game-facing airport and CDI models

**Files:**
- Modify: `Core/NavModels.cs`
- Modify: `Core/CdiData.cs`
- Modify: `Core/NavController.cs`

- [ ] **Step 1: Extend the plain models**

Replace `Core/NavModels.cs` with:

```csharp
using UnityEngine;

namespace NOVor.Core
{
    public enum CourseMode
    {
        Auto,
        Manual
    }

    public enum AirportSortMode
    {
        Nearest,
        Name
    }

    public struct RunwayInfo
    {
        public string Label;
        public float Heading;
        public float LengthMeters;
    }

    public struct AirportInfo
    {
        public string Name;
        public float Bearing;
        public float DistanceNm;
        public float ElevationMeters;
        public float EtaSeconds;
        public float SteerHeading;
        public float GroundSpeedKnots;
        public bool HasPosition;
        public bool HasEta;
        public bool IsFriendly;
        public bool HasFaction;
        public bool IsMobile;
        public Color FactionColor;
        public string FactionTag;
        public int SourceIndex;
        public RunwayInfo[] Runways;
    }
}
```

Add the following fields to `CdiData`:

```csharp
public float GroundTrack;
public float SteerHeading;
public float GroundSpeedKnots;
public float EtaSeconds;
public bool HasEta;
```

Rename `DistanceKm` to `DistanceNm` in `CdiData` and all consumers.

- [ ] **Step 2: Correct CDI deviation semantics in `UpdateData`**

In `NavController.UpdateData`, keep live target bearing/distance calculation, then use:

```csharp
float horizontalSpeed = new Vector2(rb.velocity.x, rb.velocity.z).magnitude;
float groundTrack = horizontalSpeed > 2f
    ? Mathf.Repeat(Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg, 360f)
    : heading;
Vector3 targetVelocity = GetAirbaseVelocity(target);
Vector3 line = tpos - pos;
Vector3 lineHorizontal = new Vector3(line.x, 0f, line.z);
float closure = lineHorizontal.sqrMagnitude > 1f
    ? Vector3.Dot(rb.velocity - targetVelocity, lineHorizontal.normalized)
    : 0f;
double eta = NavMath.EtaSeconds(lineHorizontal.magnitude, closure);

Data.Heading = heading;
Data.GroundTrack = groundTrack;
Data.Bearing = bearing;
Data.DistanceNm = distance / 1852f;
Data.AirportName = GetAirbaseName(target);
Data.Mode = _mode;
Data.Course = _mode == CourseMode.Manual ? _manualCourse : bearing;
Data.ToStation = NavMath.IsToStation(Data.Course, bearing);
Data.Deviation = (float)NavMath.CourseDeviationDegrees(Data.Course, bearing);
Data.Deflection = Mathf.Clamp(Data.Deviation / Plugin.FullDeflectionDeg.Value, -1f, 1f);
Data.SteerHeading = (float)NavMath.DriftCorrectedHeadingDegrees(bearing, heading, groundTrack);
Data.GroundSpeedKnots = horizontalSpeed * 1.9438445f;
Data.HasEta = !double.IsNaN(eta) && !double.IsInfinity(eta);
Data.EtaSeconds = Data.HasEta ? (float)eta : 0f;
```

Add this helper to `NavController`:

```csharp
private static Vector3 GetAirbaseVelocity(Airbase airbase)
{
    if (airbase == null || airbase.runways == null) return Vector3.zero;
    for (int i = 0; i < airbase.runways.Length; i++)
    {
        var runway = airbase.runways[i];
        if (runway != null) return runway.GetVelocity();
    }
    return Vector3.zero;
}
```

This uses closure rate rather than raw groundspeed, so ETA remains meaningful for moving carriers.

- [ ] **Step 3: Populate ownership, mobility, runway length, and field data**

At the start of `UpdatePanel`, resolve the local faction once:

```csharp
Faction localFaction = null;
if (hasAircraft && _aircraft != null && _aircraft.Player != null && _aircraft.Player.HQ != null)
    localFaction = _aircraft.Player.HQ.faction;
```

For each `Airbase`, populate:

```csharp
var faction = ab.CurrentHQ != null ? ab.CurrentHQ.faction : null;
Vector3 targetVelocity = GetAirbaseVelocity(ab);
var info = new AirportInfo
{
    Name = GetAirbaseName(ab),
    HasPosition = pos.HasValue,
    SourceIndex = i,
    Runways = GetRunways(ab),
    ElevationMeters = ab.center != null ? ab.center.position.y : 0f,
    IsMobile = ab.AttachedAirbase,
    HasFaction = faction != null,
    IsFriendly = faction != null && localFaction != null && faction == localFaction,
    FactionColor = faction != null ? faction.color : UiColorsFallbackFactionColor,
    FactionTag = faction != null ? faction.factionTag : "---"
};
```

Define `private static readonly Color UiColorsFallbackFactionColor = new Color(0.35f, 0.37f, 0.36f, 1f);` in `NavController`, avoiding a Core-to-UI dependency.

When position exists, compute the same bearing, ground track, closure, ETA, and steering fields used by `UpdateData`, then set:

```csharp
info.DistanceNm = horizontalDistance / 1852f;
info.SteerHeading = (float)NavMath.DriftCorrectedHeadingDegrees(brg, heading, groundTrack);
info.GroundSpeedKnots = horizontalSpeed * 1.9438445f;
info.HasEta = !double.IsNaN(eta) && !double.IsInfinity(eta);
info.EtaSeconds = info.HasEta ? (float)eta : 0f;
```

Change `AddRunwayDirection` to include the physical runway's public `Length` property:

```csharp
result.Add(new RunwayInfo
{
    Label = label,
    Heading = heading,
    LengthMeters = runway.Length
});
```

Sort by `DistanceNm` in the controller only to preserve the existing hotkey order. The panel will apply its own `NEAR`/`A–Z` presentation sort without changing `SourceIndex`.

- [ ] **Step 4: Run the automated and compile checks**

```powershell
dotnet run --project tests\NavMathHarness\NavMathHarness.csproj -c Release
dotnet build NOVor.csproj -c Release
```

Expected: 10 harness checks pass; plugin builds with 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add Core\NavModels.cs Core\CdiData.cs Core\NavController.cs
git commit -m "feat: add field ownership and flight navigation telemetry"
```

---

### Task 3: Replace the knob with an interactive heading-up HSI

**Files:**
- Create: `UI/HsiCourseSelector.cs`
- Create: `UI/PanelHsi.cs`
- Delete: `UI/CourseKnob.cs`
- Modify: `UI/TextureFactory.cs`

- [ ] **Step 1: Replace the knob input handler**

Create `UI/HsiCourseSelector.cs`. Reuse the proven angular drag behavior from `CourseKnob`, but attach it to the full HSI hit area and do not rotate a dial child:

```csharp
using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NOVor.UI
{
    public sealed class HsiCourseSelector : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private const float MinDragRadiusFraction = 0.18f;
        private RectTransform _rect;
        private bool _dragging;
        private float _lastAngle;

        public event Action<float> Delta;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!TryAngle(eventData, out _lastAngle)) return;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !TryAngle(eventData, out float angle)) return;
            float delta = Mathf.DeltaAngle(_lastAngle, angle);
            _lastAngle = angle;
            if (Mathf.Abs(delta) > 0.001f) Delta?.Invoke(delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            eventData.Use();
            Delta?.Invoke(eventData.scrollDelta.y > 0f ? 1f : -1f);
        }

        private bool TryAngle(PointerEventData eventData, out float angle)
        {
            angle = 0f;
            if (_rect == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return false;
            float radius = Mathf.Min(_rect.rect.width, _rect.rect.height) * 0.5f;
            if (radius <= 0f || local.magnitude < radius * MinDragRadiusFraction) return false;
            angle = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
            return true;
        }
    }
}
```

- [ ] **Step 2: Build `PanelHsi` from uGUI primitives**

Create `UI/PanelHsi.cs` as a focused component with:

```csharp
public event Action<float> CourseAdjusted;
public void Build(Transform parent, float size);
public void SetData(CdiData data);
public void Destroy();
```

`Build` must create these named children in this draw order:

1. `CompassCard`: circular outline plus 36 ticks, rotating by `data.Heading` so aircraft heading is always at the fixed top lubber line.
2. `BearingPointer`: muted double-ended pointer at `-(data.Bearing - data.Heading)`.
3. `CourseAssembly`: amber course arrow at `-(data.Course - data.Heading)`.
4. `DeviationBar`: child of `CourseAssembly`, moving perpendicular to the course by `-data.Deflection * size * 0.22f`.
5. `AircraftSymbol`: fixed white center symbol.
6. `LubberLine`: fixed amber triangle/bar at 12 o'clock.
7. `CourseReadout`: `CRS 184°` at top center.
8. `ToFromFlag`: read-only `TO` in amber or `FR` in muted white at lower center.

Create ticks and pointers with `Image` rectangles; use TMP only for `N`, `E`, `S`, `W`, course, and TO/FROM. No procedural shaded knob textures or faux-metal gradients. `HsiCourseSelector.Delta` forwards to `CourseAdjusted`.

Use a fixed top lubber line and rotate the card with:

```csharp
_compassCard.localEulerAngles = new Vector3(0f, 0f, data.Heading);
_bearingPointer.localEulerAngles = new Vector3(0f, 0f, -(data.Bearing - data.Heading));
_courseAssembly.localEulerAngles = new Vector3(0f, 0f, -(data.Course - data.Heading));
_deviationBar.anchoredPosition = new Vector2(-data.Deflection * _size * 0.22f, 0f);
```

Verify signs in game during Task 7; the acceptance rule is behavioral: a station right of the selected inbound course moves the deviation bar right, and turning/intercepting toward it reduces deflection.

- [ ] **Step 3: Remove superseded textures and component**

Delete `UI/CourseKnob.cs`. Remove `_knobBezels`, `_knobDials`, `CreateKnobBezel`, `CreateKnobDial`, and `Smooth` from `TextureFactory.cs`. Retain `CreateFramedSprite`; list fades will be removed in Task 5, after which `CreateFadeSprite` can also be deleted.

- [ ] **Step 4: Compile**

```powershell
dotnet build NOVor.csproj -c Release
```

Expected: build succeeds with 0 errors.

- [ ] **Step 5: Commit**

```powershell
git add UI\HsiCourseSelector.cs UI\PanelHsi.cs UI\TextureFactory.cs
git rm UI\CourseKnob.cs
git commit -m "feat: replace course knob with interactive panel HSI"
```

---

### Task 4: Establish native avionics chrome and consistent controls

**Files:**
- Modify: `UI/UiColors.cs`
- Modify: `UI/NavPanel.cs`

- [ ] **Step 1: Separate panel and HUD palettes**

Keep `HudGreen`, `HudAmber`, and HUD text colors for `CdiInstrument`. Replace panel-specific green tokens with:

```csharp
public static readonly Color Chrome = Hex(0x101312, 0.92f);
public static readonly Color ChromeRaised = Hex(0x191d1c, 0.96f);
public static readonly Color Rule = Hex(0x3b403e);
public static readonly Color Amber = Hex(0xe3a64b);
public static readonly Color AmberDim = Hex(0x78562c, 0.72f);
public static readonly Color PanelText = Hex(0xd8ddd9);
public static readonly Color PanelMuted = Hex(0x8b928e);
public static readonly Color OnAmber = Hex(0x17120a);
public static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
```

Panel opacity is intentional: `Chrome` at 92% reveals motion/horizon without compromising text contrast.

- [ ] **Step 2: Add explicit selected/action styling helpers**

In `NavPanel`, replace call-site tint improvisation with:

```csharp
private static void StyleToggle(Button button, TextMeshProUGUI label, bool selected)
{
    ApplyButtonTint(button, selected ? UiColors.Amber : UiColors.ChromeRaised);
    label.color = selected ? UiColors.OnAmber : UiColors.PanelMuted;
}

private static void StyleAction(Button button, TextMeshProUGUI label)
{
    ApplyButtonTint(button, UiColors.ChromeRaised);
    label.color = UiColors.PanelText;
    var image = button.GetComponent<Image>();
    image.sprite = TextureFactory.CreateFramedSprite(UiColors.ChromeRaised, UiColors.Rule, 1);
    image.type = Image.Type.Sliced;
}
```

Use `StyleToggle` only for persistent modes/filters/selections and `StyleAction` for momentary course actions. Selected airport rows use `UiColors.ChromeRaised`, an amber 4 px left rail, amber name text, and no full amber fill.

- [ ] **Step 3: Rename the visible panel title**

Change `NO-VOR NAV` to `NAV / CDI`. Keep plugin GUID and assembly names unchanged for compatibility.

- [ ] **Step 4: Compile and commit**

```powershell
dotnet build NOVor.csproj -c Release
git add UI\UiColors.cs UI\NavPanel.cs
git commit -m "style: match Nuclear Option avionics chrome"
```

Expected: build succeeds with 0 errors.

---

### Task 5: Rebuild the panel as a landscape airport-and-HSI page

**Files:**
- Modify: `UI/NavPanel.cs`
- Modify: `UI/TextureFactory.cs`
- Modify: `Plugin.cs`

- [ ] **Step 1: Replace the portrait root layout**

Set `PanelWidth = 820f`, `PanelHeight = 430f`, `HeaderHeight = 36f`, and use a fixed-size root rather than `ContentSizeFitter`. Keep the current draggable header and persisted `PanelX`/`PanelY`.

Under the header, create a horizontal `Body` with:

- `AirportPane`, preferred width `486f`, containing search/filter controls, the column header, and the scroll list.
- A one-pixel vertical `Rule`.
- `NavPane`, preferred width `303f`, containing AUTO/MANUAL, the `PanelHsi`, actions, runway modes, and field facts.

Use `PanelHsi.Build(navPane.transform, 214f)` and wire `CourseAdjusted` to the existing `CourseAdjusted` event.

- [ ] **Step 2: Make sort and friendly filter actual modes**

Add fields:

```csharp
private AirportSortMode _sortMode;
private bool _friendlyOnly;
private Button _nearButton;
private TextMeshProUGUI _nearLabel;
private Button _nameButton;
private TextMeshProUGUI _nameLabel;
private Button _friendlyButton;
private TextMeshProUGUI _friendlyLabel;
```

Initialize from config and refresh toggle styling:

```csharp
_sortMode = Plugin.SortByName.Value ? AirportSortMode.Name : AirportSortMode.Nearest;
_friendlyOnly = Plugin.FriendlyOnly.Value;
```

Bind in `Plugin.Awake`:

```csharp
SortByName = Config.Bind("Panel", "SortByName", false,
    "Sort airport rows alphabetically instead of by nearest distance.");
FriendlyOnly = Config.Bind("Panel", "FriendlyOnly", false,
    "Show only fields controlled by the local player's faction.");
```

Click handlers update both config and row presentation. Replace `FilteredAirports()` with a method that filters by search/friendly state and sorts a copy by either `DistanceNm` or `Name`, always retaining `SourceIndex` as selection identity.

Remove `NearestRequested` from `NavPanel` and its `NavController.Awake` subscription. Hotkey cycling continues to follow the controller's nearest-distance order.

- [ ] **Step 3: Add a real column header and fixed-unit row columns**

Insert `FIELD`, `BRG`, and `NM` headers aligned to the same rects as the row data. Replace each row's combined metadata TMP object with separate right-aligned bearing and distance TMP objects:

```csharp
bearing.text = info.HasPosition ? $"{Mathf.RoundToInt(info.Bearing):000}°" : "---";
distance.text = info.HasPosition ? info.DistanceNm.ToString("0.0") : "--.-";
```

This guarantees aligned tabular columns and removes repeated `BRG` tokens. Use nautical miles for every airport row, including nearby fields; never switch to metres.

Add a 4 px faction rail from `info.FactionColor` to every row. Append a small `MOV` badge after the name when `info.IsMobile`. Do not color the entire row by faction.

- [ ] **Step 4: Replace fades with a native scrollbar**

Create a `Scrollbar` at the right edge of the list viewport with a `UiColors.ChromeRaised` track and `UiColors.Rule`/`UiColors.AmberDim` thumb. Assign it to `ScrollRect.verticalScrollbar` and set `verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport`.

Remove `_fadeTop`, `_fadeBottom`, `MakeListFade`, `RefreshFades`, and `TextureFactory.CreateFadeSprite`. The viewport must clip exactly at a row boundary: choose list height as `7 * RowHeight + 6 * RowSpacing`.

- [ ] **Step 5: Make collapse and close controls legible**

Increase both buttons to `32 x 28`. Use `–`/`+` for collapse/expand and `×` for close. Add TMP tooltips is unnecessary; the stateful glyph change plus larger hit area is sufficient.

- [ ] **Step 6: Compile and commit**

```powershell
dotnet build NOVor.csproj -c Release
git add UI\NavPanel.cs UI\TextureFactory.cs Plugin.cs
git commit -m "feat: rebuild nav panel as landscape airport page"
```

Expected: build succeeds with 0 errors.

---

### Task 6: Finish course, runway, and field-detail semantics

**Files:**
- Modify: `UI/NavPanel.cs`
- Modify: `Core/NavController.cs`

- [ ] **Step 1: Replace the bottom target sentence with HSI data**

Remove `_targetLabel`, `_targetText`, `_toFromBtn`, `_toFromText`, `CourseFlipToFrom`, and their controller subscription. Pass the full `CdiData` object to the panel:

```csharp
public void SetNavigation(CdiData data)
{
    _hsi?.SetData(data);
    StyleToggle(_autoBtn, _autoText, data.Mode == CourseMode.Auto);
    StyleToggle(_manualBtn, _manualText, data.Mode == CourseMode.Manual);
    RefreshRunwaySelection(data.Course);
    RefreshFieldFacts(data);
}
```

Replace the controller call to `SetCourse(...)` with `_panel?.SetNavigation(Data);`.

- [ ] **Step 2: Separate momentary course actions from state**

Below the HSI, create a row of three outlined action buttons:

- `SET BRG` invokes `SetCourseToBearing`.
- `SET HDG` invokes `SetCourseToHeading`.
- `RECIP` invokes a renamed `SetReciprocalCourse` event and sets `_manualCourse + 180f`.

These controls never stay highlighted. AUTO/MANUAL remains the only course-mode segmented control.

- [ ] **Step 3: Make runway direction a real selected state**

Store each runway button and text label. On every `SetNavigation`, select a runway when:

```csharp
Mathf.Abs(Mathf.DeltaAngle(runway.Heading, data.Course)) <= 1f
```

Use `StyleToggle` for the matching runway and inactive styling for the reciprocal. Include length in each label:

```csharp
$"{runway.Label}  {Mathf.RoundToInt(runway.Heading):000}°  {runway.LengthMeters / 1000f:0.0} km"
```

Clicking a runway still invokes `RunwaySelected` and changes to manual mode through `SetManualCourse`.

- [ ] **Step 4: Add compact field facts**

For the selected airport, display two fixed rows:

```text
ELEV 74 m     ETA 02:41
STEER 137°    GS 221 kt
```

Use `--:--` when `HasEta` is false and `---` for unavailable flight values. Format ETA with:

```csharp
private static string FormatEta(float seconds)
{
    if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)) return "--:--";
    int total = Mathf.Clamp(Mathf.RoundToInt(seconds), 0, 5999);
    return $"{total / 60:00}:{total % 60:00}";
}
```

The header remains the only full selected-target sentence. Field facts do not repeat the airport name, bearing, or distance.

- [ ] **Step 5: Update the existing cockpit HUD CDI for the corrected data contract**

In `CdiInstrument.SetData`, change `DistanceKm` to `DistanceNm` and render `NM`. Its needle continues to use `Deflection`, which now represents station/course geometry rather than aircraft heading error. Add `STEER` to the secondary line and retain green HUD styling.

- [ ] **Step 6: Compile and commit**

```powershell
dotnet build NOVor.csproj -c Release
git add UI\NavPanel.cs UI\CdiInstrument.cs Core\NavController.cs
git commit -m "feat: align course controls and field data with HSI semantics"
```

Expected: build succeeds with 0 errors.

---

### Task 7: In-game visual and behavioral verification

**Files:**
- Modify only if verification exposes a defect.

- [ ] **Step 1: Deploy through the project's supported path**

Run:

```powershell
dotnet build NOVor.csproj -c Debug
```

Expected: build succeeds and reports deployment to `BepInEx\scripts`; no DLL is copied to `BepInEx\plugins`.

- [ ] **Step 2: Verify native visual hierarchy at 1920x1080**

Open the panel with F9 and compare it directly to the game's top-left VOR tab.

Acceptance:

- Panel body is near-black and translucent; no mid-green body remains.
- Amber marks only persistent selections, focus, and HSI course data.
- Panel height is at most 430 reference pixels and does not obscure the central horizon.
- Text remains legible over bright desert and dark terrain.
- The selected target appears in the header and selected list row, with no third bottom sentence.
- Collapse leaves a single 36 px title bar with the complete target readout.

- [ ] **Step 3: Verify the HSI geometry**

Use a fixed airfield and set a runway course.

Acceptance:

- Lubber line is fixed at 12 o'clock.
- Compass card rotates opposite aircraft turns so current heading remains under the lubber line.
- Course pointer shows selected course relative to heading.
- Bearing pointer continuously points toward the station.
- Deviation bar shows left/right course displacement and trends toward center during a correct intercept.
- TO changes to FR only after passing the station or selecting the reciprocal geometry; there is no TO mode button.
- Twist-drag and mouse wheel adjust course and select MANUAL.

- [ ] **Step 4: Verify airport safety and mobile-field cues**

Join a mission with friendly, hostile, neutral, and carrier airbases.

Acceptance:

- Each owned field has the game's faction color rail.
- FRIENDLY hides hostile/neutral rows and persists after reload.
- Mobile carrier rows show `MOV`; their bearing updates as they move.
- Carrier ETA uses relative closure and does not simply equal distance divided by aircraft groundspeed.
- NEAR/A–Z changes presentation order without breaking selection or N/B hotkey cycling.

- [ ] **Step 5: Verify runway and telemetry data**

Acceptance:

- All row distances are NM with one decimal, including sub-NM distances.
- Runway length and elevation are plausible against known fields.
- Selecting RWY 18 highlights RWY 18 and not RWY 36; `RECIP` flips the selected runway.
- ETA reads `--:--` while stationary or moving away, then becomes finite with positive closure.
- STEER differs from BRG when current heading/ground track show drift, and converges toward BRG when drift is near zero.

- [ ] **Step 6: Verify hot reload and cleanup**

Build Debug twice while the game is open. After each reload, confirm exactly one panel, one HUD CDI, working F9/ModBar toggle, restored camera inputs when closed, and no duplicate EventSystem or cursor-lock regression.

- [ ] **Step 7: Run final automated checks and commit any verification fixes**

```powershell
dotnet run --project tests\NavMathHarness\NavMathHarness.csproj -c Release
dotnet build NOVor.csproj -c Release
git status --short
```

Expected: 10 tests pass, Release build succeeds with 0 errors, and only intentional files are modified.

If verification required code changes, commit them together by symptom:

```powershell
git add Core UI Plugin.cs
git commit -m "fix: correct HSI behavior found during flight verification"
```

---

### Task 8: Update user-facing naming and repository guidance

**Files:**
- Modify: `Integrations/ModBarBridge.cs`
- Modify: `AGENTS.md`

- [ ] **Step 1: Remove the ambiguous pun from visible navigation labels**

Keep stable identifiers (`com.novor.cdi`, `no.vor`, `NOVor.dll`) unchanged. Change the ModBar display label from `VOR` to `NAV` and tooltip from `NO-VOR Nav Panel` to `Navigation CDI`.

- [ ] **Step 2: Update `AGENTS.md` architecture and verification notes**

Document:

- `Core/NavMath.cs` and the harness command.
- `UI/PanelHsi.cs` and `UI/HsiCourseSelector.cs`.
- Native panel chrome versus green cockpit HUD palette.
- Ownership from `Airbase.CurrentHQ.faction`.
- Mobile-field detection from `Airbase.AttachedAirbase` and relative-closure ETA.
- All displayed navigation distances use NM; runway lengths/elevation use metric units.
- `UI/CourseKnob.cs` no longer exists.

- [ ] **Step 3: Final build and commit**

```powershell
dotnet run --project tests\NavMathHarness\NavMathHarness.csproj -c Release
dotnet build NOVor.csproj -c Release
git add Integrations\ModBarBridge.cs AGENTS.md
git commit -m "docs: document native nav panel and HSI architecture"
```

Expected: harness passes and Release build succeeds with 0 errors.

---

## Self-review

**Feedback coverage:**

- Belongs in game: Tasks 4, 5, and 7 use native dark/amber chrome and translucency.
- Knob replacement/lubber line: Tasks 3 and 7 deliver a heading-up HSI with fixed top index.
- Duplicate target: Tasks 5 and 6 retain header/list context and remove the bottom sentence.
- Control grammar: Tasks 4 and 6 distinguish state, selection, and actions; TO/FROM becomes output.
- Runway selection: Task 6 highlights the course-matching reciprocal and adds length.
- Units/repeated BRG/scrolling: Task 5 uses fixed NM columns, a single header, and a scrollbar.
- Ownership/friendly filter: Tasks 2, 5, and 7 use live faction color and friendly filtering.
- Moving carriers: Tasks 2, 5, and 7 add MOV plus relative-closure telemetry.
- NEAREST ambiguity: Task 5 replaces it with `NEAR | A–Z` sort modes.
- Landscape proportions/translucency: Task 5 sets `820 x 430` and 92% chrome.
- Elevation/ETA/heading-to-fly: Tasks 2 and 6 add all three; Task 7 verifies behavior.
- Minimize/close and naming: Tasks 5 and 8 enlarge controls and use `NAV / CDI`/`NAV`.

**Scope choice:** A persistent RECENT sort is intentionally not added. It would require defining recency lifetime and persistence semantics without improving the primary diversion workflow; `NEAR | A–Z` is enough to make `NEAR` unambiguously a sort mode.

**Type consistency:** `AirportInfo.DistanceNm`, `RunwayInfo.LengthMeters`, `CdiData.DistanceNm`, `PanelHsi.SetData(CdiData)`, and the renamed reciprocal action are used consistently across tasks.

**Working-tree safety:** The plan never resets the current runway/knob prototype. It preserves its runway extraction and camera safeguards, replaces only the superseded knob path, and commits changes in reviewable slices.
