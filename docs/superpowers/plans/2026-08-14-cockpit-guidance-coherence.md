# Cockpit Guidance Coherence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the cockpit navigation block and heading-tape additions into one compact, trustworthy instrument whose course context, active command, and off-screen cues always agree.

**Architecture:** Add one dependency-free guidance function that selects the active command heading for direct and manual-course flight, then expose that single command through `CdiData` to every UI surface. Add a dependency-free cockpit presentation model so state-dependent wording and visibility are testable outside Unity. Rebuild only the cockpit block and tape cue glyphs; preserve the navigation panel, CDI scaling policy, hotkeys, HUD parenting, and flight math that are not directly involved.

**Tech Stack:** C# net472, Unity 2022.3 uGUI, BepInEx 5, dependency-free .NET 8 navigation harness.

---

## Scope and acceptance criteria

- The block, panel `STEER` readout, and amber tape cue consume the same active command heading.
- In direct mode, the command is the drift-corrected bearing to the selected field.
- In manual mode, the command is the drift-corrected proportional intercept track returned by `CdiScale.InterceptHeadingDegrees`; it converges to the selected course as cross-track reaches zero.
- The cockpit block has four readable levels: target/range, course context, active command, and support telemetry. The manual CDI occupies one clearly bounded rail between context and command.
- `TO`/`FROM` appears once in the block, not both beside the scale and in the course row.
- The CDI scale explicitly means full deflection on either side: for example, `TERM · ±1 NM`.
- A missing ETA is rendered honestly as `ETA --:--`; zero groundspeed remains visible as `GS 0 KT`.
- The heading tape never converts both cues into identical amber edge arrows. Course remains a green outlined triangle; command remains an amber solid diamond, including when clamped to an edge.
- Two cues clamped to the same edge remain distinguishable on their separate vertical lanes.
- No new panel layout, airport-search behavior, navigation modes, or flight-control automation are added.
- The existing 42-check harness expands to 51 checks, Release and Debug builds have zero warnings/errors, and the in-game acceptance matrix passes.

## Baseline precondition

The current worktree contains the uncommitted August 14 navigation-polish changes. Finish their in-game acceptance and land them as their own change before starting this plan. Do not mix those changes into this milestone. The baseline already passes `NavMathHarness: 42 passed` and `dotnet build NOVor.csproj -c Release` with zero warnings/errors.

## File structure

| Path | Change | Responsibility after change |
|---|---|---|
| `Core/GuidanceMath.cs` | Create | Dependency-free selection of the active drift-corrected command heading. |
| `Core/CockpitPresentation.cs` | Create | Dependency-free text/visibility model for the cockpit block. |
| `Core/CdiData.cs` | Modify | Carry one `CommandHeading` and `CommandError`; remove competing cockpit command fields. |
| `Core/NavController.cs` | Modify | Populate the command only after CDI deviation/intercept is known. |
| `Core/NavigationPresentation.cs` | Modify | Format CDI scale distance consistently in aviation and metric modes. |
| `UI/CdiInstrument.cs` | Modify | Consume the presentation model and render the compact four-level block. |
| `UI/HeadingTapeCues.cs` | Modify | Position semantic cue icons without replacing their identity off scale. |
| `UI/HudCueIcon.cs` | Create | Build outlined course and solid command shapes from uGUI rectangles. |
| `UI/HudGlyphs.cs` | Modify | Remove text-glyph constants no longer used by heading-tape cues. |
| `UI/NavPanel.cs` | Modify | Read the same active command heading as the cockpit surfaces. |
| `tests/NavMathHarness/NavMathHarness.csproj` | Modify | Link the two new dependency-free core files. |
| `tests/NavMathHarness/Program.cs` | Modify | Cover guidance selection and cockpit presentation states. |
| `AGENTS.md` | Modify | Record command-heading and cue-identity contracts. |

### Task 1: Establish one active command heading

**Files:**

- Create: `Core/GuidanceMath.cs`
- Modify: `tests/NavMathHarness/NavMathHarness.csproj`
- Modify: `tests/NavMathHarness/Program.cs`
- Modify: `Core/CdiData.cs`
- Modify: `Core/NavController.cs`
- Modify: `UI/CdiInstrument.cs`
- Modify: `UI/HeadingTapeCues.cs`
- Modify: `UI/NavPanel.cs`

- [ ] **Step 1: Link the guidance file in the standalone harness**

Add this compile item after `CdiScale.cs` in `tests/NavMathHarness/NavMathHarness.csproj`:

```xml
<Compile Include="..\..\Core\GuidanceMath.cs" Link="Core\GuidanceMath.cs" />
```

- [ ] **Step 2: Write four failing command-heading checks**

Insert these checks after the existing `CdiScale.InterceptHeadingDegrees` checks in `tests/NavMathHarness/Program.cs`:

```csharp
Equal(110d, GuidanceMath.CommandHeadingDegrees(false, 0d, 100d, 0d, 45d, 90d, 80d),
    "direct command applies left-drift correction");
Equal(55d, GuidanceMath.CommandHeadingDegrees(true, 90d, 0d, 5d, 45d, 90d, 80d),
    "manual command uses right-of-course intercept with drift correction");
Equal(45d, GuidanceMath.CommandHeadingDegrees(true, 10d, 0d, -5d, 45d, 90d, 100d),
    "manual command uses left-of-course intercept with drift correction");
Equal(357d, GuidanceMath.CommandHeadingDegrees(true, 359d, 0d, 0d, 45d, 359d, 1d),
    "manual command converges to selected course across wraparound");
```

- [ ] **Step 3: Run the harness and verify the missing contract fails**

Run:

```powershell
dotnet run --project tests\NavMathHarness -c Release
```

Expected: compilation fails because `GuidanceMath` does not exist.

- [ ] **Step 4: Implement the dependency-free command selector**

Create `Core/GuidanceMath.cs`:

```csharp
namespace NOVor.Core
{
    public static class GuidanceMath
    {
        public static double CommandHeadingDegrees(bool manual, double course, double bearing,
            double crossTrackNm, double maxInterceptDegrees, double heading, double groundTrack)
        {
            double desiredTrack = manual
                ? CdiScale.InterceptHeadingDegrees(course, crossTrackNm, maxInterceptDegrees)
                : NavMath.NormalizeDegrees(bearing);
            return NavMath.DriftCorrectedHeadingDegrees(desiredTrack, heading, groundTrack);
        }
    }
}
```

- [ ] **Step 5: Replace competing command fields in `CdiData`**

In `Core/CdiData.cs`, replace `SteeringError`, `SteerHeading`, and `InterceptHeading` with:

```csharp
public float CommandHeading;
public float CommandError;
```

Keep `Course`, `Bearing`, `CrossTrackNm`, `OffScale`, and the CDI scale fields unchanged; they remain context and deviation data rather than commands.

- [ ] **Step 6: Populate the command after evaluating CDI deviation**

In `Core/NavController.UpdateData`, remove the existing `SteerHeading` and `SteeringError` assignments before `CdiScale.Evaluate`. Immediately after assigning `Data.Deflection`, add:

```csharp
Data.CommandHeading = (float)GuidanceMath.CommandHeadingDegrees(
    _mode == CourseMode.Manual,
    Data.Course,
    Data.Bearing,
    Data.CrossTrackNm,
    Plugin.MaxInterceptDegrees.Value,
    Data.Heading,
    Data.GroundTrack);
Data.CommandError = (float)NavMath.SteeringErrorDegrees(Data.Heading, Data.CommandHeading);
```

- [ ] **Step 7: Move all three consumers to the shared command**

Make these exact substitutions in `UI/HeadingTapeCues.cs`, `UI/NavPanel.cs`, and `UI/CdiInstrument.cs`, respectively:

```csharp
float steerDelta = (float)NavMath.DeltaAngleDegrees(data.Heading, data.CommandHeading);
string steer = $"{Mathf.RoundToInt(_navigation.CommandHeading):000}°";
_actionText.text = "INTCP " + FormatDegrees(data.CommandHeading) + "°";
```

In `CdiInstrument.SetAutoData`, replace both `SteeringError` reads with `CommandError`.

- [ ] **Step 8: Run the harness and production build**

Run:

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
```

Expected: `NavMathHarness: 46 passed`; build succeeds with zero warnings/errors; `rg -n "data\.SteerHeading|_navigation\.SteerHeading|SteeringError|data\.InterceptHeading" Core UI` returns no matches. `AirportInfo.SteerHeading` remains a separate airport-list preview value and is not a cockpit command source.

- [ ] **Step 9: Commit the command contract**

```powershell
git add Core\GuidanceMath.cs Core\CdiData.cs Core\NavController.cs UI\CdiInstrument.cs UI\HeadingTapeCues.cs UI\NavPanel.cs tests\NavMathHarness\NavMathHarness.csproj tests\NavMathHarness\Program.cs
git commit -m "fix: unify active navigation command"
```

### Task 2: Make cockpit wording and state testable

**Files:**

- Create: `Core/CockpitPresentation.cs`
- Modify: `Core/NavigationPresentation.cs`
- Modify: `tests/NavMathHarness/NavMathHarness.csproj`
- Modify: `tests/NavMathHarness/Program.cs`

- [ ] **Step 1: Link the cockpit presentation file**

Add this item after `NavigationPresentation.cs` in the harness project:

```xml
<Compile Include="..\..\Core\CockpitPresentation.cs" Link="Core\CockpitPresentation.cs" />
```

- [ ] **Step 2: Add five failing presentation checks**

Insert this block after the existing navigation-presentation checks:

```csharp
var intercept = CockpitPresentation.Build(new CockpitPresentationInput
{
    AirportName = "Dustbowl Airbase",
    DistanceNm = 10f,
    Course = 157f,
    Bearing = 202f,
    CommandHeading = 202f,
    GroundSpeedKnots = 0f,
    FullScaleNm = 1f,
    Manual = true,
    ToStation = false,
    OffScale = true,
    HasEta = false,
    ScaleMode = CdiScaleMode.Terminal,
    Units = NavigationDisplayUnits.Aviation
});
Equal("DUSTBOWL  ·  10.0 NM", intercept.TargetLine, "cockpit target line");
Equal("CRS 157°  ·  FROM", intercept.ContextLine, "cockpit manual context");
Equal("INTCP 202°", intercept.CommandLine, "cockpit intercept command");
Equal("ETA --:--  ·  GS 0 KT", intercept.SupportLine, "cockpit unavailable eta");
Equal("TERM  ·  ±1 NM", intercept.ScaleLine, "cockpit full-deflection scale");
```

- [ ] **Step 3: Add compact CDI-scale formatting**

Add to `Core/NavigationPresentation.cs`:

```csharp
public static string FormatScaleDistance(float nauticalMiles, NavigationDisplayUnits units)
{
    if (units == NavigationDisplayUnits.Metric)
        return (nauticalMiles * KilometersPerNauticalMile).ToString("0.#") + " km";
    return nauticalMiles.ToString("0.#") + " NM";
}
```

- [ ] **Step 4: Implement the cockpit presentation contract**

Create `Core/CockpitPresentation.cs` with these public types and formatting rules:

```csharp
using System;

namespace NOVor.Core
{
    public struct CockpitPresentationInput
    {
        public string AirportName;
        public float DistanceNm;
        public float Course;
        public float Bearing;
        public float CommandHeading;
        public float GroundSpeedKnots;
        public float EtaSeconds;
        public float FullScaleNm;
        public bool Manual;
        public bool ToStation;
        public bool OffScale;
        public bool HasEta;
        public CdiScaleMode ScaleMode;
        public NavigationDisplayUnits Units;
    }

    public struct CockpitReadout
    {
        public string TargetLine;
        public string ContextLine;
        public string CommandLine;
        public string SupportLine;
        public string ScaleLine;
        public bool ShowCdi;
        public bool CommandAttention;
    }

    public static class CockpitPresentation
    {
        public static CockpitReadout Build(CockpitPresentationInput input)
        {
            string context = input.Manual
                ? "CRS " + Degrees(input.Course) + "°  ·  " + NavigationPresentation.ToFromLabel(input.ToStation)
                : "BRG " + Degrees(input.Bearing) + "°  ·  DIRECT";
            string command = input.Manual
                ? (input.OffScale ? "INTCP " : "TRACK ") + Degrees(input.CommandHeading) + "°"
                : "STEER " + Degrees(input.CommandHeading) + "°";
            return new CockpitReadout
            {
                TargetLine = CompactFieldName(input.AirportName) + "  ·  " +
                    NavigationPresentation.FormatDistance(input.DistanceNm, input.Units),
                ContextLine = context,
                CommandLine = command,
                SupportLine = FormatEta(input) + "  ·  GS " +
                    NavigationPresentation.FormatSpeed(input.GroundSpeedKnots, input.Units),
                ScaleLine = input.Manual
                    ? ScaleTag(input.ScaleMode) + "  ·  ±" +
                        NavigationPresentation.FormatScaleDistance(input.FullScaleNm, input.Units)
                    : string.Empty,
                ShowCdi = input.Manual,
                CommandAttention = input.Manual && input.OffScale
            };
        }

        private static string FormatEta(CockpitPresentationInput input)
        {
            if (!input.HasEta || input.EtaSeconds <= 0f || input.EtaSeconds > 359940f)
                return "ETA --:--";
            int total = (int)Math.Round(input.EtaSeconds);
            return "ETA " + (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        private static string Degrees(float value)
        {
            int rounded = (int)Math.Round(NavMath.NormalizeDegrees(value));
            if (rounded == 360) rounded = 0;
            return rounded.ToString("000");
        }

        private static string CompactFieldName(string value)
        {
            string compact = (value ?? "NAV").ToUpperInvariant()
                .Replace("ANNEX CLASS CARRIER", "ANNEX CV")
                .Replace("INTERNATIONAL", "INTL")
                .Replace("AIRFIELD", "")
                .Replace("AIRSTRIP", "")
                .Replace("AIRPORT", "")
                .Replace("AIRBASE", "")
                .Trim();
            while (compact.Contains("  ")) compact = compact.Replace("  ", " ");
            if (compact.Length > 12)
            {
                int cut = compact.LastIndexOf(' ', 12);
                compact = cut > 3 ? compact.Substring(0, cut) : compact.Substring(0, 12);
            }
            return compact.Length > 0 ? compact : "NAV";
        }

        private static string ScaleTag(CdiScaleMode mode)
        {
            switch (mode)
            {
                case CdiScaleMode.Approach: return "APP";
                case CdiScaleMode.Terminal: return "TERM";
                case CdiScaleMode.Enroute: return "ENR";
                default: return "FIX";
            }
        }
    }
}
```

- [ ] **Step 5: Run the harness and update its pass line**

Change the final success line to `NavMathHarness: 51 passed`, then run:

```powershell
dotnet run --project tests\NavMathHarness -c Release
```

Expected: `NavMathHarness: 51 passed`.

- [ ] **Step 6: Commit the presentation contract**

```powershell
git add Core\CockpitPresentation.cs Core\NavigationPresentation.cs tests\NavMathHarness\NavMathHarness.csproj tests\NavMathHarness\Program.cs
git commit -m "feat: add cockpit presentation contract"
```

### Task 3: Recompose the cockpit block around one visual hierarchy

**Files:**

- Modify: `UI/CdiInstrument.cs`
- Modify: `UI/HudGlyphs.cs`

- [ ] **Step 1: Replace local formatting with the presentation model**

At the start of `CdiInstrument.SetData`, create the readout and bind its state:

```csharp
var readout = CockpitPresentation.Build(new CockpitPresentationInput
{
    AirportName = data.AirportName,
    DistanceNm = data.DistanceNm,
    Course = data.Course,
    Bearing = data.Bearing,
    CommandHeading = data.CommandHeading,
    GroundSpeedKnots = data.GroundSpeedKnots,
    EtaSeconds = data.EtaSeconds,
    FullScaleNm = data.FullScaleNm,
    Manual = data.Mode == CourseMode.Manual,
    ToStation = data.ToStation,
    OffScale = data.OffScale,
    HasEta = data.HasEta,
    ScaleMode = data.ScaleMode,
    Units = Plugin.DisplayUnits.Value
});

_fieldText.text = readout.TargetLine;
_courseText.text = readout.ContextLine;
_actionText.text = readout.CommandLine;
_etaText.text = readout.SupportLine;
_scaleLabel.text = readout.ScaleLine;
_actionText.color = readout.CommandAttention ? UiColors.HudAmber : UiColors.HudContext;
_manualGroup.SetActive(readout.ShowCdi);
ApplyLayout(readout.ShowCdi);
```

Keep only needle position, off-scale visibility, and deviation state in `SetManualData`. Remove `CompactFieldName`, `FormatCrossTrack`, `FormatScale`, `FormatDegrees`, `ScaleTag`, and `FormatEta` from this Unity component.

- [ ] **Step 2: Add one explicit CDI rail and five ticks**

Use these layout constants at the top of `CdiInstrument`:

```csharp
private const float BlockWidth = 288f;
private const float ManualHeight = 136f;
private const float DirectHeight = 104f;
private const float ScaleHalfWidthPx = 72f;
```

In `BuildManualScale`, add a two-pixel horizontal rail before the ticks:

```csharp
var rail = HudGlyphs.MakeRect("DeviationRail", UiColors.HudGreenDim);
rail.SetParent(scale, false);
HudGlyphs.Place(rail, Vector2.zero, new Vector2(ScaleHalfWidthPx * 2f, 2f));

foreach (float x in new[] { -ScaleHalfWidthPx, -ScaleHalfWidthPx * 0.5f, 0f,
             ScaleHalfWidthPx * 0.5f, ScaleHalfWidthPx })
{
    var tick = HudGlyphs.MakeRect("DeviationTick", UiColors.HudGreenDim);
    tick.SetParent(scale, false);
    HudGlyphs.Place(tick, new Vector2(x, 0f), new Vector2(2f, x == 0f ? 20f : 10f));
}
```

Delete the four bullet glyphs and the separate center-index rectangle. Keep the moving needle three pixels wide. Place the scale label at `(0, 13)` with a `150 × 16` centered rect. Delete `_toFromFlag` and its construction entirely; `ContextLine` is now the sole TO/FROM annunciation.

- [ ] **Step 3: Apply compact mode-specific vertical positions**

Add this method and call it after `Build` and on every data update:

```csharp
private void ApplyLayout(bool manual)
{
    var rt = (RectTransform)transform;
    rt.sizeDelta = new Vector2(BlockWidth, manual ? ManualHeight : DirectHeight);
    HudGlyphs.Place(_fieldText.rectTransform, new Vector2(0f, manual ? 52f : 36f), new Vector2(270f, 18f));
    HudGlyphs.Place(_courseText.rectTransform, new Vector2(0f, manual ? 30f : 14f), new Vector2(270f, 18f));
    HudGlyphs.Place(_actionText.rectTransform, new Vector2(0f, manual ? -34f : -14f), new Vector2(270f, 22f));
    HudGlyphs.Place(_etaText.rectTransform, new Vector2(0f, manual ? -58f : -38f), new Vector2(270f, 16f));
}
```

Place the manual scale group at `(0, 1)` with a `210 × 26` rect. This removes the empty manual-CDI gap from direct mode without creating a second renderer.

- [ ] **Step 4: Keep type hierarchy restrained**

Use these construction styles:

```csharp
_fieldText = HudGlyphs.MakeText("FieldRange", UiColors.HudContext, 12, FontStyle.Bold);
_courseText = HudGlyphs.MakeText("Course", UiColors.HudSupport, 11, FontStyle.Bold);
_actionText = HudGlyphs.MakeText("Action", UiColors.HudContext, 17, FontStyle.Bold);
_etaText = HudGlyphs.MakeText("Eta", UiColors.HudSupport, 10, FontStyle.Normal);
```

The command becomes amber only through `CommandAttention`; selected target and course context never compete with an off-scale intercept.

- [ ] **Step 5: Build and inspect both modes**

Run:

```powershell
dotnet build NOVor.csproj -c Release
dotnet build NOVor.csproj -c Debug
```

Expected: zero warnings/errors. In game, direct mode has no blank CDI-sized gap; manual mode shows one bounded rail; `FROM` appears only in the context line; and the default block remains clear of the central reticle at `Hud.OffsetY = -180`.

- [ ] **Step 6: Commit the block recomposition**

```powershell
git add UI\CdiInstrument.cs UI\HudGlyphs.cs
git commit -m "polish: recompose cockpit navigation block"
```

### Task 4: Preserve tape-cue identity at every position

**Files:**

- Create: `UI/HudCueIcon.cs`
- Modify: `UI/HeadingTapeCues.cs`
- Modify: `UI/HudGlyphs.cs`

- [ ] **Step 1: Build procedural semantic icons**

Create `UI/HudCueIcon.cs`. The course factory builds a green outlined downward triangle from three two-pixel rectangles; the command factory builds an amber eight-pixel square rotated 45 degrees. Store the root and expose only position and rotation:

```csharp
using UnityEngine;

namespace NOVor.UI
{
    internal sealed class HudCueIcon
    {
        public RectTransform Rect { get; }

        private HudCueIcon(RectTransform rect)
        {
            Rect = rect;
        }

        public static HudCueIcon CreateCourse(RectTransform parent)
        {
            RectTransform root = MakeRoot(parent, "NOVorCourseCue");
            AddStroke(root, "Top", new Vector2(10f, 2f), new Vector2(0f, 4f), 0f, UiColors.HudGreen);
            AddStroke(root, "Left", new Vector2(10f, 2f), new Vector2(-2.5f, 0f), -58f, UiColors.HudGreen);
            AddStroke(root, "Right", new Vector2(10f, 2f), new Vector2(2.5f, 0f), 58f, UiColors.HudGreen);
            return new HudCueIcon(root);
        }

        public static HudCueIcon CreateCommand(RectTransform parent)
        {
            RectTransform root = MakeRoot(parent, "NOVorCommandCue");
            RectTransform diamond = HudGlyphs.MakeRect("Diamond", UiColors.HudAmber);
            diamond.SetParent(root, false);
            HudGlyphs.Place(diamond, Vector2.zero, new Vector2(8f, 8f));
            diamond.localEulerAngles = new Vector3(0f, 0f, 45f);
            return new HudCueIcon(root);
        }

        private static RectTransform MakeRoot(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            HudGlyphs.Place(rect, Vector2.zero, new Vector2(20f, 20f));
            return rect;
        }

        private static void AddStroke(RectTransform parent, string name, Vector2 size,
            Vector2 position, float rotation, Color color)
        {
            RectTransform stroke = HudGlyphs.MakeRect(name, color);
            stroke.SetParent(parent, false);
            HudGlyphs.Place(stroke, position, size);
            stroke.localEulerAngles = new Vector3(0f, 0f, rotation);
        }
    }
}
```

- [ ] **Step 2: Replace `Text` cue fields with semantic icons**

In `HeadingTapeCues`, replace the two `Text` fields with:

```csharp
private HudCueIcon _courseCue;
private HudCueIcon _commandCue;
```

In `Build`, create them and retain the existing upper/lower lane positions:

```csharp
_courseCue = HudCueIcon.CreateCourse(rt);
_commandCue = HudCueIcon.CreateCommand(rt);
```

- [ ] **Step 3: Clamp without changing color or shape**

Replace `SetCue` with:

```csharp
private void SetCue(HudCueIcon cue, float delta, float halfSpan, float laneY,
    bool pointOutward)
{
    float halfWidth = Mathf.Max(1f, _compass.rectTransform.rect.width * 0.5f - 12f);
    bool offScale = Mathf.Abs(delta) > halfSpan;
    float x = Mathf.Clamp(delta / halfSpan, -1f, 1f) * halfWidth;
    cue.Rect.anchoredPosition = new Vector2(x, laneY);
    cue.Rect.localEulerAngles = pointOutward && offScale
        ? new Vector3(0f, 0f, delta < 0f ? -90f : 90f)
        : Vector3.zero;
}
```

Call it after smoothing with:

```csharp
float lane = _compass.rectTransform.rect.height * 0.25f;
SetCue(_courseCue, _smoothedCourseDelta, halfSpan, lane, true);
SetCue(_commandCue, _smoothedSteerDelta, halfSpan, -lane, false);
```

Remove the old glyph and color parameters. Remove `CourseCue` and `SteeringCue` from `HudGlyphs`; retain the block's `OffScaleLeft` and `OffScaleRight` because one amber CDI edge flag is unambiguous there.

- [ ] **Step 4: Verify edge and overlap states in game**

Build Debug, then capture these cases:

1. Course and command both visible near center: green outlined triangle above, amber diamond below.
2. Course left off scale and command right off scale: green left-pointing outline at the upper-left edge, amber diamond at the lower-right edge.
3. Both off scale at the right edge: both shapes visible on separate lanes; neither changes identity.
4. Heading 359° with targets at 001°, then the inverse: both take the short path.
5. HUD toggle and hot reload: neither cue sweeps from a stale position.

- [ ] **Step 5: Commit the semantic tape cues**

```powershell
git add UI\HudCueIcon.cs UI\HeadingTapeCues.cs UI\HudGlyphs.cs
git commit -m "polish: preserve heading cue identity"
```

### Task 5: Complete regression and visual acceptance

**Files:**

- Modify: `AGENTS.md`

- [ ] **Step 1: Record the guidance contract**

Replace the current direct/manual cockpit-guidance bullets in `AGENTS.md` with:

```markdown
- `GuidanceMath` owns the active command heading. Direct mode commands the drift-corrected field bearing; manual mode commands the drift-corrected proportional intercept track and converges to the selected course on centerline.
- `CdiData.CommandHeading` is the single command consumed by the panel `STEER` readout, cockpit command line, and amber heading-tape diamond. `Course` and `Bearing` are context, not competing commands.
- The heading tape preserves semantic identity on and off scale: green outlined triangle is course/bearing context; amber solid diamond is the active command. Edge clamping never recolors or substitutes either cue.
- The cockpit block presents target/range, course context, active command, and support telemetry in that order. TO/FROM appears once, and CDI full scale is labeled as a plus/minus distance.
```

- [ ] **Step 2: Run the automated verification set**

Run in this order:

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
dotnet build NOVor.csproj -c Debug
```

Expected: `NavMathHarness: 51 passed`; both builds report zero warnings/errors; Debug deploys only to `BepInEx\scripts`.

- [ ] **Step 3: Run the cockpit acceptance matrix**

Verify at 16:9 and ultrawide aspect ratios, against bright cloud, terrain, cockpit frame, and dark sky:

- Direct, moving: `DIRECT`, valid ETA, and one shared steer heading across block/panel/tape.
- Direct, stationary: `ETA --:-- · GS 0 KT` without visual garbage or missing glyphs.
- Manual, on scale, TO: centered CDI and normal-color `TRACK` command.
- Manual, on scale, FROM: `FROM` appears once and the command converges to selected course.
- Manual, off scale: needle hidden, one block edge flag, amber `INTCP`, and matching amber tape diamond.
- Course and command on opposite tape edges, then on the same edge.
- Heading wrap at 359°/001°, HUD toggle, scene transition, and one ScriptEngine hot reload.

- [ ] **Step 4: Capture comparison evidence**

Capture one image matching each supplied screenshot's framing: the manual off-scale block and a heading tape with both cues off scale. Add a third direct-mode block capture because its collapsed layout is not represented in the original screenshots.

- [ ] **Step 5: Commit documentation**

```powershell
git add AGENTS.md
git commit -m "docs: define cockpit guidance coherence"
```

## Self-review result

- The plan fixes the command mismatch before changing visuals.
- Text and state rules are covered by the standalone harness; Unity-only geometry has explicit in-game cases.
- The block and tape remain owned by `CockpitHud`, and no new Harmony patch or persistent scene object is introduced.
- The panel changes only one field name at its readout boundary; its layout is intentionally excluded.
- Existing auto-scale thresholds, CDI deflection signs, target selection, camera handling, and hot-reload cleanup remain untouched.
