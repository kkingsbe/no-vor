# Cockpit HUD Consolidation and Enroute CDI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Collapse the two independent cockpit CDI renderers into one owned HUD with a single deviation policy, then make that policy usable for a whole diversion leg instead of only the last nautical mile.

**Architecture:** A new dependency-free `Core/CdiScale.cs` becomes the single source of truth for full-scale distance, deflection, off-scale state, and intercept guidance, and is unit-tested in the existing math harness. A new `UI/HudGlyphs.cs` holds the glyph/font/outline construction currently duplicated across two renderers. A new `UI/CockpitHud.cs` owns both display surfaces — the floating block parented to `FlightHud.GetHUDCenter()` and the cue pair parented to the native `compass` RawImage — so `NavController` creates, positions, shows, and feeds exactly one object. With one policy in place, auto-scaling and an intercept cue are added in one location rather than two.

**Tech Stack:** C# targeting `net472` (plugin) and `net8.0` (math harness), Unity 2022.3.6 Mono uGUI, BepInEx 5, TextMeshPro (panel only).

## Global Constraints

- Namespaces are `NOVor`, `NOVor.Core`, `NOVor.UI`, `NOVor.Integrations`.
- **Do not add code comments.** Prefer clear names and small methods.
- `Core/CdiScale.cs` must not reference `UnityEngine` — the `net8.0` harness compiles it directly.
- The cockpit HUD uses legacy `UnityEngine.UI.Text`; the panel uses TextMeshPro via `FontLoader`. Do not mix them.
- The cockpit HUD is phosphor green (`UiColors.HudGreen`); amber (`UiColors.HudAmber`) is reserved for command and off-scale states.
- Every generated `Text` and `Image` in the cockpit gets a black `Outline` for sky contrast.
- Config is exposed as static `ConfigEntry<T>` fields on `Plugin`.
- Navigation distances are nautical miles, speed is knots, field elevation stays metric.
- Never deploy `NOVor.dll` to `BepInEx\plugins`; ScriptEngine loads from `BepInEx\scripts`.
- Commit subjects use `feat:`, `fix:`, `tune:`, `polish:`, `refactor:`, or `chore:`.

**Design decision recorded up front:** auto-scaling alone does *not* fix the observed 21 NM cross-track at 23.5 NM range — GPS-style terminal scaling is 1 NM inside 30 NM, so that needle stays pegged. The intercept cue in Task 5 is what makes a grossly-off-course leg flyable; auto-scaling is what makes the 0.3–5 NM band meaningful. Both are required.

---

### Task 1: Single deviation and scale policy in Core

**Files:**
- Create: `Core/CdiScale.cs`
- Modify: `tests/NavMathHarness/NavMathHarness.csproj`
- Modify: `tests/NavMathHarness/Program.cs`

**Interfaces:**
- Consumes: `NavMath.NormalizeDegrees`, `NavMath.CrossTrackDeflection` from `Core/NavMath.cs`.
- Produces: `CdiScaleMode` enum (`Enroute`, `Terminal`, `Approach`, `Fixed`); `CdiDeviation` struct with fields `Mode` (`CdiScaleMode`), `FullScaleNm` (`double`), `Deflection` (`double`), `OffScale` (`bool`), `Side` (`int`), `InterceptHeading` (`double`); static methods `CdiScale.SelectMode(double distanceNm, CdiScaleMode previous)`, `CdiScale.FullScaleNm(CdiScaleMode mode, double fixedFullScaleNm)`, `CdiScale.InterceptHeadingDegrees(double course, double crossTrackNm, double maxInterceptDegrees)`, and `CdiScale.Evaluate(double course, double crossTrackNm, double distanceNm, CdiScaleMode previousMode, bool autoScale, double fixedFullScaleNm, double maxInterceptDegrees)`.

- [ ] **Step 1: Add the harness compile reference**

The harness only compiles the files it lists explicitly. Without this the new tests will not see `CdiScale`.

Replace the `<ItemGroup>` in `tests/NavMathHarness/NavMathHarness.csproj`:

```xml
  <ItemGroup>
    <Compile Include="..\..\Core\NavMath.cs" Link="Core\NavMath.cs" />
    <Compile Include="..\..\Core\CdiScale.cs" Link="Core\CdiScale.cs" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing tests**

In `tests/NavMathHarness/Program.cs`, insert these lines immediately after the existing `True(double.IsNaN(NavMath.EtaSeconds(1852d, 0.5d)), "no useful closure");` line and before the `if (_failures > 0)` line:

```csharp
        Same(CdiScaleMode.Enroute, CdiScale.SelectMode(45d, CdiScaleMode.Enroute), "enroute beyond thirty miles");
        Same(CdiScaleMode.Terminal, CdiScale.SelectMode(25d, CdiScaleMode.Enroute), "terminal inside thirty miles");
        Same(CdiScaleMode.Approach, CdiScale.SelectMode(1.5d, CdiScaleMode.Terminal), "approach inside two miles");
        Same(CdiScaleMode.Terminal, CdiScale.SelectMode(31d, CdiScaleMode.Terminal), "terminal hysteresis holds");
        Same(CdiScaleMode.Enroute, CdiScale.SelectMode(33d, CdiScaleMode.Terminal), "terminal hysteresis releases");
        Same(CdiScaleMode.Approach, CdiScale.SelectMode(2.2d, CdiScaleMode.Approach), "approach hysteresis holds");
        Same(CdiScaleMode.Terminal, CdiScale.SelectMode(2.5d, CdiScaleMode.Approach), "approach hysteresis releases");
        Equal(5d, CdiScale.FullScaleNm(CdiScaleMode.Enroute, 1d), "enroute full scale");
        Equal(1d, CdiScale.FullScaleNm(CdiScaleMode.Terminal, 1d), "terminal full scale");
        Equal(0.3d, CdiScale.FullScaleNm(CdiScaleMode.Approach, 1d), "approach full scale");
        Equal(2.5d, CdiScale.FullScaleNm(CdiScaleMode.Fixed, 2.5d), "fixed full scale from config");
        Equal(45d, CdiScale.InterceptHeadingDegrees(90d, 5d, 45d), "intercept turns left when right of course");
        Equal(55d, CdiScale.InterceptHeadingDegrees(10d, -5d, 45d), "intercept turns right when left of course");
        Equal(347.5d, CdiScale.InterceptHeadingDegrees(10d, 0.5d, 45d), "intercept shallows inside one mile");
        True(!CdiScale.Evaluate(90d, 3d, 45d, CdiScaleMode.Enroute, true, 1d, 45d).OffScale,
            "three miles is on the enroute scale");
        True(CdiScale.Evaluate(90d, 21d, 23.5d, CdiScaleMode.Terminal, true, 1d, 45d).OffScale,
            "twenty one miles is off the terminal scale");
        Equal(-0.6d, CdiScale.Evaluate(90d, 3d, 45d, CdiScaleMode.Enroute, true, 1d, 45d).Deflection,
            "enroute deflection scales to five miles");
        Equal(1d, CdiScale.Evaluate(90d, -9d, 45d, CdiScaleMode.Enroute, true, 1d, 45d).Deflection,
            "left of course clamps the needle right");
        Equal(1d, CdiScale.Evaluate(90d, 3d, 45d, CdiScaleMode.Enroute, true, 1d, 45d).Side,
            "positive cross track is right of course");
```

Add this helper method to `Program`, directly after the existing `True` method:

```csharp
    private static void Same(CdiScaleMode expected, CdiScaleMode actual, string name)
    {
        if (expected == actual) return;
        Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
        _failures++;
    }
```

Change the success line to the new total. Note that the existing literal is stale — the file prints `18 passed` but contains only 17 assertions, so the correct new total is 17 + 19 = 36, not 37:

```csharp
        Console.WriteLine("NavMathHarness: 36 passed");
```

- [ ] **Step 3: Run the harness and confirm it fails to compile**

```bash
dotnet run --project tests/NavMathHarness -c Release
```

Expected: FAIL — compiler errors `CS0246` / `CS0103` reporting that `CdiScale` and `CdiScaleMode` could not be found.

- [ ] **Step 4: Write the policy**

Create `Core/CdiScale.cs`:

```csharp
using System;

namespace NOVor.Core
{
    public enum CdiScaleMode
    {
        Enroute,
        Terminal,
        Approach,
        Fixed
    }

    public struct CdiDeviation
    {
        public CdiScaleMode Mode;
        public double FullScaleNm;
        public double Deflection;
        public bool OffScale;
        public int Side;
        public double InterceptHeading;
    }

    public static class CdiScale
    {
        public const double EnrouteFullScaleNm = 5d;
        public const double TerminalFullScaleNm = 1d;
        public const double ApproachFullScaleNm = 0.3d;
        public const double TerminalEntryNm = 30d;
        public const double ApproachEntryNm = 2d;
        public const double TerminalHysteresisNm = 2d;
        public const double ApproachHysteresisNm = 0.3d;
        private const double SideDeadbandNm = 0.005d;

        public static CdiScaleMode SelectMode(double distanceNm, CdiScaleMode previous)
        {
            if (previous == CdiScaleMode.Approach && distanceNm <= ApproachEntryNm + ApproachHysteresisNm)
                return CdiScaleMode.Approach;
            if (previous == CdiScaleMode.Terminal && distanceNm > ApproachEntryNm
                && distanceNm <= TerminalEntryNm + TerminalHysteresisNm)
                return CdiScaleMode.Terminal;
            if (distanceNm <= ApproachEntryNm) return CdiScaleMode.Approach;
            if (distanceNm <= TerminalEntryNm) return CdiScaleMode.Terminal;
            return CdiScaleMode.Enroute;
        }

        public static double FullScaleNm(CdiScaleMode mode, double fixedFullScaleNm)
        {
            switch (mode)
            {
                case CdiScaleMode.Approach: return ApproachFullScaleNm;
                case CdiScaleMode.Terminal: return TerminalFullScaleNm;
                case CdiScaleMode.Enroute: return EnrouteFullScaleNm;
                default: return fixedFullScaleNm;
            }
        }

        public static double InterceptHeadingDegrees(double course, double crossTrackNm,
            double maxInterceptDegrees)
        {
            double magnitude = Math.Abs(crossTrackNm);
            double angle = magnitude >= 1d ? maxInterceptDegrees : magnitude * maxInterceptDegrees;
            return NavMath.NormalizeDegrees(crossTrackNm > 0d ? course - angle : course + angle);
        }

        public static CdiDeviation Evaluate(double course, double crossTrackNm, double distanceNm,
            CdiScaleMode previousMode, bool autoScale, double fixedFullScaleNm,
            double maxInterceptDegrees)
        {
            CdiScaleMode mode = autoScale ? SelectMode(distanceNm, previousMode) : CdiScaleMode.Fixed;
            double fullScale = FullScaleNm(mode, fixedFullScaleNm);
            double magnitude = Math.Abs(crossTrackNm);
            return new CdiDeviation
            {
                Mode = mode,
                FullScaleNm = fullScale,
                Deflection = NavMath.CrossTrackDeflection(crossTrackNm, fullScale),
                OffScale = fullScale > 0d && magnitude >= fullScale,
                Side = crossTrackNm > SideDeadbandNm ? 1 : crossTrackNm < -SideDeadbandNm ? -1 : 0,
                InterceptHeading = InterceptHeadingDegrees(course, crossTrackNm, maxInterceptDegrees)
            };
        }
    }
}
```

`CrossTrackDeflection` is a ratio, so passing nautical miles for both arguments is correct and avoids a redundant metre conversion.

- [ ] **Step 5: Run the harness and confirm it passes**

```bash
dotnet run --project tests/NavMathHarness -c Release
```

Expected: `NavMathHarness: 36 passed` and exit code 0.

- [ ] **Step 6: Commit**

```bash
git add Core/CdiScale.cs tests/NavMathHarness/NavMathHarness.csproj tests/NavMathHarness/Program.cs
git commit -m "feat: add auto-scaling CDI deviation policy with intercept guidance"
```

---

### Task 2: Feed the policy through the data contract

**Files:**
- Modify: `Core/CdiData.cs`
- Modify: `Core/NavController.cs:311-354`
- Modify: `Plugin.cs:72-74`

**Interfaces:**
- Consumes: `CdiScale.Evaluate`, `CdiDeviation`, `CdiScaleMode` from Task 1.
- Produces: `CdiData` gains `public CdiScaleMode ScaleMode`, `public bool OffScale`, `public int Side`, `public float InterceptHeading`. `Plugin` gains `internal static ConfigEntry<bool> AutoScaleCdi` and `internal static ConfigEntry<float> MaxInterceptDegrees`.

- [ ] **Step 1: Extend the data contract**

Replace the body of `Core/CdiData.cs`:

```csharp
namespace NOVor.Core
{
    public class CdiData
    {
        public float Heading;
        public float GroundTrack;
        public float Bearing;
        public float Course;
        public float CrossTrackNm;
        public float FullScaleNm;
        public float SteeringError;
        public float Deflection;
        public float DistanceNm;
        public float SteerHeading;
        public float InterceptHeading;
        public float GroundSpeedKnots;
        public float EtaSeconds;
        public string AirportName;
        public CourseMode Mode;
        public CdiScaleMode ScaleMode;
        public int Side;
        public bool ToStation;
        public bool OffScale;
        public bool HasEta;
    }
}
```

- [ ] **Step 2: Add the configuration entries**

In `Plugin.cs`, add these two fields alongside the other static config fields (after the existing `FullDeflectionNm` declaration on line 18):

```csharp
        internal static ConfigEntry<bool> AutoScaleCdi;
        internal static ConfigEntry<float> MaxInterceptDegrees;
```

In the same file, immediately after the existing `FullDeflectionNm = Config.Bind(...)` call, add:

```csharp
            AutoScaleCdi = Config.Bind("Navigation", "AutoScaleCdi", true,
                "Scale the CDI automatically by range: 5 NM enroute, 1 NM within 30 NM, 0.3 NM within 2 NM. When disabled, FullDeflectionNauticalMiles is used at all ranges.");
            MaxInterceptDegrees = Config.Bind("Navigation", "MaxInterceptDegrees", 45f,
                new ConfigDescription("Largest intercept angle commanded when the CDI is off scale.",
                    new AcceptableValueRange<float>(10f, 90f)));
```

Update the `FullDeflectionNm` description so it no longer claims to be the only scale:

```csharp
            FullDeflectionNm = Config.Bind("Navigation", "FullDeflectionNauticalMiles", 1f,
                new ConfigDescription("Cross-track distance in nautical miles that moves the manual CDI to full deflection when AutoScaleCdi is disabled.",
                    new AcceptableValueRange<float>(0.1f, 10f)));
```

- [ ] **Step 3: Hold the scale mode across frames and evaluate once**

The hysteresis in `CdiScale.SelectMode` needs the previous mode, so `NavController` must remember it. Add this field to `NavController` next to the existing `_manualCourse` field:

```csharp
        private CdiScaleMode _scaleMode = CdiScaleMode.Enroute;
```

In `NavController.UpdateData`, replace these five lines:

```csharp
            float crossTrackMeters = (float)NavMath.CrossTrackMeters(Data.Course, -horizontal.x, -horizontal.z);
            Data.CrossTrackNm = crossTrackMeters / 1852f;
            Data.FullScaleNm = Plugin.FullDeflectionNm.Value;
            Data.Deflection = _mode == CourseMode.Manual
                ? (float)NavMath.CrossTrackDeflection(crossTrackMeters, Plugin.FullDeflectionNm.Value * 1852f)
                : 0f;
```

with:

```csharp
            float crossTrackMeters = (float)NavMath.CrossTrackMeters(Data.Course, -horizontal.x, -horizontal.z);
            Data.CrossTrackNm = crossTrackMeters / 1852f;

            var deviation = CdiScale.Evaluate(Data.Course, Data.CrossTrackNm, Data.DistanceNm,
                _scaleMode, Plugin.AutoScaleCdi.Value, Plugin.FullDeflectionNm.Value,
                Plugin.MaxInterceptDegrees.Value);
            _scaleMode = deviation.Mode;

            Data.ScaleMode = deviation.Mode;
            Data.FullScaleNm = (float)deviation.FullScaleNm;
            Data.Side = deviation.Side;
            Data.OffScale = deviation.OffScale;
            Data.InterceptHeading = (float)deviation.InterceptHeading;
            Data.Deflection = _mode == CourseMode.Manual ? (float)deviation.Deflection : 0f;
```

`Data.DistanceNm` is assigned earlier in the same method, so it is already current at this point.

- [ ] **Step 4: Build and verify**

```bash
dotnet build NOVor.csproj -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add Core/CdiData.cs Core/NavController.cs Plugin.cs
git commit -m "feat: drive cockpit deviation from the shared CDI scale policy"
```

---

### Task 3: Shared cockpit glyph factory

This task is a pure refactor with no behavior change. `CdiInstrument` and `HeadingTapeCues` each build their own `Text` objects, resolve their own font, and attach their own outlines — `HeadingTapeCues` re-resolves `LegacyRuntime.ttf` on every glyph with no cache.

**Files:**
- Create: `UI/HudGlyphs.cs`
- Modify: `UI/CdiInstrument.cs:162-231`
- Modify: `UI/HeadingTapeCues.cs:46-88`

**Interfaces:**
- Produces: `internal static class HudGlyphs` with `Font Font { get; }`, `Text MakeText(string name, Color color, int size, FontStyle style)`, `Text MakeCue(RectTransform parent, string name, string glyph, Color color, Vector2 position, int fontSize)`, `RectTransform MakeRect(string name, Color color)`, `void Place(RectTransform rt, Vector2 position, Vector2 size)`, `void AddOutline(Graphic graphic)`, and the constants `string OffScaleLeft = "◀"` and `string OffScaleRight = "▶"`.
- Note: `MakeText` returns `Text`, not `RectTransform` as the old private helper did. Use `.rectTransform` at call sites that need the transform.

- [ ] **Step 1: Write the factory**

Create `UI/HudGlyphs.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

namespace NOVor.UI
{
    internal static class HudGlyphs
    {
        public const string OffScaleLeft = "◀";
        public const string OffScaleRight = "▶";

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        public static Text MakeText(string name, Color color, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var text = go.GetComponent<Text>();
            text.font = Font;
            text.color = color;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            AddOutline(text);
            return text;
        }

        public static Text MakeCue(RectTransform parent, string name, string glyph, Color color,
            Vector2 position, int fontSize)
        {
            var text = MakeText(name, color, fontSize, FontStyle.Bold);
            text.rectTransform.SetParent(parent, false);
            text.text = glyph;
            Place(text.rectTransform, position, new Vector2(24f, 24f));
            return text;
        }

        public static RectTransform MakeRect(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            AddOutline(image);
            return (RectTransform)go.transform;
        }

        public static void Place(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void AddOutline(Graphic graphic)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }
    }
}
```

- [ ] **Step 2: Delete the duplicated helpers from `CdiInstrument`**

In `UI/CdiInstrument.cs`, delete these six members entirely: `MakeCue`, `Place`, `MakeRect`, `MakeText`, `AddOutline`, `GetDefaultFont`, and the `private static Font _font;` field. Then update every remaining call site in that file to the factory. The `Build` and `BuildManualScale` methods become:

```csharp
        private void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(320f, 118f);

            _fieldText = HudGlyphs.MakeText("FieldRange", UiColors.TextSecondary, 12, FontStyle.Bold);
            _fieldText.rectTransform.SetParent(rt, false);
            HudGlyphs.Place(_fieldText.rectTransform, new Vector2(0f, 48f), new Vector2(260f, 18f));

            BuildManualScale(rt);

            _actionText = HudGlyphs.MakeText("Action", UiColors.HudGreen, 16, FontStyle.Bold);
            _actionText.rectTransform.SetParent(rt, false);
            HudGlyphs.Place(_actionText.rectTransform, new Vector2(0f, -47f), new Vector2(260f, 22f));

            SetVisible(false);
        }

        private void BuildManualScale(RectTransform parent)
        {
            var scale = MakeGroup(parent, "ManualCdi", new Vector2(0f, -10f), 24f);
            _manualGroup = scale.gameObject;

            foreach (float x in new[] { -ScaleHalfWidthPx, -ScaleHalfWidthPx * 0.5f,
                         ScaleHalfWidthPx * 0.5f, ScaleHalfWidthPx })
                HudGlyphs.MakeCue(scale, "DeviationDot", "•", UiColors.HudGreenDim, new Vector2(x, 0f), 14);

            HudGlyphs.MakeCue(scale, "CenterIndex", "▽", UiColors.HudGreen, Vector2.zero, 16);

            _needle = HudGlyphs.MakeRect("DeviationNeedle", UiColors.HudGreen);
            _needle.SetParent(scale, false);
            HudGlyphs.Place(_needle, Vector2.zero, new Vector2(3f, 20f));

            _offScaleLeft = HudGlyphs.MakeCue(scale, "OffScaleLeft", HudGlyphs.OffScaleLeft,
                UiColors.HudAmber, new Vector2(-(ScaleHalfWidthPx + 18f), 0f), 16);
            _offScaleRight = HudGlyphs.MakeCue(scale, "OffScaleRight", HudGlyphs.OffScaleRight,
                UiColors.HudAmber, new Vector2(ScaleHalfWidthPx + 18f, 0f), 16);
            _offScaleLeft.color = Hidden;
            _offScaleRight.color = Hidden;

            _scaleLabel = HudGlyphs.MakeCue(scale, "FullScale", "1NM", UiColors.TextSecondary,
                new Vector2(ScaleHalfWidthPx + 30f, 10f), 9);
            _scaleLabel.alignment = TextAnchor.MiddleLeft;
            _scaleLabel.rectTransform.sizeDelta = new Vector2(42f, 16f);

            _toFromFlag = HudGlyphs.MakeCue(scale, "ToFrom", "▲ TO", UiColors.TextSecondary,
                new Vector2(-(ScaleHalfWidthPx + 32f), 10f), 9);
            _toFromFlag.alignment = TextAnchor.MiddleRight;
            _toFromFlag.rectTransform.sizeDelta = new Vector2(48f, 16f);
        }

        private static RectTransform MakeGroup(RectTransform parent, string name, Vector2 position, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            HudGlyphs.Place(rt, position, new Vector2(ScaleHalfWidthPx * 2f + 90f, height));
            return rt;
        }
```

The `CenterIndex` glyph stays `▽` for now — Task 4 replaces it, and keeping it here holds this task to a pure refactor.

- [ ] **Step 3: Route `HeadingTapeCues` through the factory**

In `UI/HeadingTapeCues.cs`, delete the private `MakeCue` method entirely and replace `Build` with:

```csharp
        private void Build()
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            float quarterHeight = _compass.rectTransform.rect.height * 0.25f;
            _courseCue = HudGlyphs.MakeCue(rt, "NOVorCourseCue", "▽", UiColors.HudGreen,
                new Vector2(0f, quarterHeight), 15);
            _courseCue.rectTransform.sizeDelta = new Vector2(24f, 20f);
            _steeringCue = HudGlyphs.MakeCue(rt, "NOVorSteeringCue", "◇", UiColors.HudAmber,
                new Vector2(0f, -quarterHeight), 15);
            _steeringCue.rectTransform.sizeDelta = new Vector2(24f, 20f);
        }
```

- [ ] **Step 4: Build and verify no behavior changed**

```bash
dotnet build NOVor.csproj -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. The cockpit HUD must look pixel-identical to before this task — this is a refactor.

- [ ] **Step 5: Commit**

```bash
git add UI/HudGlyphs.cs UI/CdiInstrument.cs UI/HeadingTapeCues.cs
git commit -m "refactor: extract shared cockpit glyph factory"
```

---

### Task 4: One owner for both cockpit surfaces

`NavController` currently runs two discovery paths (`hud.GetHUDCenter()` and reflection on the private `compass` field), holds two references, and fans `SetVisible` out to both. This task moves all of that into one component and settles the division of labor: the native tape owns angular cues, the floating block owns linear cues.

**Files:**
- Create: `UI/CockpitHud.cs`
- Modify: `UI/CdiInstrument.cs:32-59`
- Modify: `UI/HeadingTapeCues.cs:24-44`
- Modify: `Core/NavController.cs:20-22, 72-84, 268-273, 356-401, 470-477`

**Interfaces:**
- Consumes: `CdiInstrument.SetData(CdiData)`, `CdiInstrument.ApplyOffsets(float, float)`, `CdiInstrument.SetVisible(bool)`, `HeadingTapeCues.Initialize(RawImage)`, `HeadingTapeCues.SetData(CdiData)`, `HeadingTapeCues.SetVisible(bool)`.
- Produces: `public class CockpitHud : MonoBehaviour` with `void Initialize(RawImage compass)`, `void ApplyOffsets(float x, float y)`, `void SetVisible(bool visible)`, `void SetData(CdiData data)`.
- Breaking change: `CdiInstrument.SetData` loses its unused `int index, int count` parameters and becomes `SetData(CdiData data)`.

- [ ] **Step 1: Drop the dead parameters from `CdiInstrument`**

Replace the `SetData` signature and body in `UI/CdiInstrument.cs`. `index` and `count` have been unused since the legibility pass:

```csharp
        public void SetData(CdiData data)
        {
            if (!isActiveAndEnabled) return;

            bool manual = data.Mode == CourseMode.Manual;
            _manualGroup.SetActive(manual);
            _fieldText.text = CompactFieldName(data.AirportName) + "  " + FormatRange(data.DistanceNm);

            if (manual)
                SetManualData(data);
            else
                SetAutoData(data);
        }
```

- [ ] **Step 2: Consume the shared off-scale state instead of recomputing it**

`SetManualData` currently derives `side` and `offScale` locally. Both now arrive on `CdiData`. Replace `SetManualData` in `UI/CdiInstrument.cs`:

```csharp
        private void SetManualData(CdiData data)
        {
            float magnitude = Mathf.Abs(data.CrossTrackNm);
            string side = data.Side > 0 ? "R" : data.Side < 0 ? "L" : "ON";
            _actionText.text = "XTK " + FormatCrossTrack(magnitude) + " " + side;

            _needle.gameObject.SetActive(!data.OffScale);
            _needle.anchoredPosition = new Vector2(data.Deflection * ScaleHalfWidthPx, 0f);
            _offScaleLeft.color = data.OffScale && data.Deflection < 0f ? UiColors.HudAmber : Hidden;
            _offScaleRight.color = data.OffScale && data.Deflection > 0f ? UiColors.HudAmber : Hidden;
            _scaleLabel.text = FormatScale(data.FullScaleNm);
            _toFromFlag.text = data.ToStation ? "▲ TO" : "▼ FR";
        }
```

- [ ] **Step 3: Replace the block's duplicate `▽` with an unambiguous center reference**

Two `▽` glyphs currently appear in the cockpit meaning different things — the block's center index and the tape's course caret. The block's becomes a drawn reference line. In `BuildManualScale`, replace the `HudGlyphs.MakeCue(scale, "CenterIndex", ...)` line with:

```csharp
            var centerIndex = HudGlyphs.MakeRect("CenterIndex", UiColors.HudGreenDim);
            centerIndex.SetParent(scale, false);
            HudGlyphs.Place(centerIndex, Vector2.zero, new Vector2(2f, 26f));
```

The needle is created after this line, so it draws on top of the reference line. Do not reorder them.

- [ ] **Step 4: Give the tape the same off-scale convention as the block**

The tape currently swaps its glyph to an outline arrow when clamped; the block uses a filled amber arrow. Unify on the block's convention: the cue turns amber and becomes a filled arrow at the edge. Replace `SetCue` in `UI/HeadingTapeCues.cs`:

```csharp
        private void SetCue(Text cue, float delta, float halfSpan, string glyph, Color color)
        {
            float halfWidth = _compass.rectTransform.rect.width * 0.5f;
            bool offScale = Mathf.Abs(delta) > halfSpan;
            float x = Mathf.Clamp(delta / halfSpan, -1f, 1f) * halfWidth;
            cue.rectTransform.anchoredPosition = new Vector2(x, cue.rectTransform.anchoredPosition.y);
            cue.text = !offScale ? glyph
                : delta < 0f ? HudGlyphs.OffScaleLeft : HudGlyphs.OffScaleRight;
            cue.color = offScale ? UiColors.HudAmber : color;
        }
```

And update the two calls in `SetData` to match the new signature:

```csharp
            SetCue(_courseCue, courseDelta, halfSpan, "▽", UiColors.HudGreen);
            SetCue(_steeringCue, steerDelta, halfSpan, "◇", UiColors.HudAmber);
```

- [ ] **Step 5: Write the owner**

Create `UI/CockpitHud.cs`. It is parented to the HUD centre; it creates the block beneath itself and the tape cues beneath the native compass, and destroys the tape cues itself because they are not its children:

```csharp
using UnityEngine;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    public class CockpitHud : MonoBehaviour
    {
        private CdiInstrument _block;
        private HeadingTapeCues _tapeCues;

        public void Initialize(RawImage compass)
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            if (_block == null)
            {
                var host = new GameObject("NOVorCdiBlock", typeof(RectTransform));
                host.transform.SetParent(transform, false);
                _block = host.AddComponent<CdiInstrument>();
            }

            if (_tapeCues == null && compass != null)
            {
                var cuesHost = new GameObject("NOVorNativeHeadingTapeCues", typeof(RectTransform));
                cuesHost.transform.SetParent(compass.rectTransform, false);
                _tapeCues = cuesHost.AddComponent<HeadingTapeCues>();
                _tapeCues.Initialize(compass);
            }
        }

        public bool NeedsTapeCues => _tapeCues == null;

        public void ApplyOffsets(float x, float y)
        {
            _block?.ApplyOffsets(x, y);
        }

        public void SetVisible(bool visible)
        {
            _block?.SetVisible(visible);
            _tapeCues?.SetVisible(visible);
        }

        public void SetData(CdiData data)
        {
            _block?.SetData(data);
            _tapeCues?.SetData(data);
        }

        private void OnDestroy()
        {
            if (_tapeCues != null) Destroy(_tapeCues.gameObject);
        }
    }
}
```

`NeedsTapeCues` exists because `FlightHud`'s private `compass` field can resolve later than the HUD centre; `NavController` retries `Initialize` until both surfaces exist.

The stretch anchors on the root are load-bearing. The block used to be parented straight to the HUD centre, and a new `RectTransform` defaults to corner anchors at the parent's bottom-left. Without stretching this new intermediate root to fill the HUD centre, the block's own centre anchors would resolve against a zero-sized rect in the corner and the whole instrument would shift off position.

- [ ] **Step 6: Collapse `NavController` onto the single owner**

In `Core/NavController.cs`, replace the two fields:

```csharp
        private CdiInstrument _instrument;
        private HeadingTapeCues _headingTapeCues;
```

with one:

```csharp
        private CockpitHud _cockpitHud;
```

Replace the three per-surface calls in `Update`:

```csharp
                _instrument?.SetData(Data, _selectedIndex, _airbases.Count);
                _headingTapeCues?.SetData(Data);
                _panel?.SetNavigation(Data);
```

with:

```csharp
                _cockpitHud?.SetData(Data);
                _panel?.SetNavigation(Data);
```

Replace `EnsureInstrument` and `SetInstrumentVisible` entirely:

```csharp
        private void EnsureInstrument()
        {
            if (_cockpitHud != null && !_cockpitHud.NeedsTapeCues) return;

            FlightHud hud = null;
            Transform hudCenter = null;
            try
            {
                hud = SceneSingleton<FlightHud>.i;
                if (hud != null) hudCenter = hud.GetHUDCenter();
            }
            catch
            {
                hudCenter = null;
            }

            if (hudCenter == null) return;

            if (_cockpitHud == null)
            {
                var host = new GameObject("NOVorCockpitHud", typeof(RectTransform));
                host.transform.SetParent(hudCenter, false);
                _cockpitHud = host.AddComponent<CockpitHud>();
            }

            var compassField = typeof(FlightHud).GetField("compass",
                BindingFlags.NonPublic | BindingFlags.Instance);
            _cockpitHud.Initialize(compassField?.GetValue(hud) as RawImage);
            _cockpitHud.ApplyOffsets(Plugin.HudOffsetX.Value, Plugin.HudOffsetY.Value);
        }

        private void SetInstrumentVisible(bool visible)
        {
            if (_cockpitHud != null) _cockpitHud.SetVisible(visible);
        }
```

Update `NudgeInstrument` to use the owner:

```csharp
            _cockpitHud?.ApplyOffsets(Plugin.HudOffsetX.Value, Plugin.HudOffsetY.Value);
```

And in `OnDestroy`, replace the two destroy calls with one:

```csharp
            if (_cockpitHud != null) Destroy(_cockpitHud.gameObject);
```

- [ ] **Step 7: Build and verify**

```bash
dotnet build NOVor.csproj -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 8: Verify hot-reload cleanup in game**

Deploy and reload, then confirm no orphans remain. Run:

```bash
dotnet build NOVor.csproj -c Debug
```

In game, press `Insert` to force a ScriptEngine reload, then confirm: exactly one CDI block below HUD centre (not two stacked), exactly one course caret and one steering diamond on the heading tape, and the block's centre reference is a short vertical line rather than a second `▽`.

- [ ] **Step 9: Commit**

```bash
git add UI/CockpitHud.cs UI/CdiInstrument.cs UI/HeadingTapeCues.cs Core/NavController.cs
git commit -m "refactor: consolidate cockpit CDI surfaces under one owner"
```

---

### Task 5: Make a saturated needle actionable

With one policy and one owner in place, the enroute payoff lands in a single renderer. The block gains a course/ETA line, annunciates the active scale, and replaces the bare amber arrow with an intercept heading.

**Files:**
- Modify: `UI/CdiInstrument.cs`
- Modify: `UI/PanelHsi.cs:66-80`

**Interfaces:**
- Consumes: `CdiData.ScaleMode`, `CdiData.OffScale`, `CdiData.Side`, `CdiData.InterceptHeading`, `CdiData.EtaSeconds`, `CdiData.HasEta`, `CdiData.GroundSpeedKnots`, `CdiData.Course`, `CdiData.Bearing` from Task 2.
- Produces: no new public surface.

- [ ] **Step 1: Add the two new text rows to the block**

In `UI/CdiInstrument.cs`, add these fields beside the existing ones:

```csharp
        private Text _courseText;
        private Text _etaText;
```

Grow the block and add the rows. In `Build`, change the size line and add the two rows after `_actionText` is placed:

```csharp
            rt.sizeDelta = new Vector2(320f, 160f);
```

```csharp
            _courseText = HudGlyphs.MakeText("Course", UiColors.TextSecondary, 12, FontStyle.Bold);
            _courseText.rectTransform.SetParent(rt, false);
            HudGlyphs.Place(_courseText.rectTransform, new Vector2(0f, -30f), new Vector2(260f, 18f));

            _etaText = HudGlyphs.MakeText("Eta", UiColors.TextMuted, 11, FontStyle.Normal);
            _etaText.rectTransform.SetParent(rt, false);
            HudGlyphs.Place(_etaText.rectTransform, new Vector2(0f, -74f), new Vector2(260f, 16f));
```

Move the action row down so it sits between them — change its `Place` call to:

```csharp
            HudGlyphs.Place(_actionText.rectTransform, new Vector2(0f, -53f), new Vector2(260f, 22f));
```

- [ ] **Step 2: Populate the shared rows on every update**

`SetData` fills the rows that apply in both modes. Replace `SetData` in `UI/CdiInstrument.cs`:

```csharp
        public void SetData(CdiData data)
        {
            if (!isActiveAndEnabled) return;

            bool manual = data.Mode == CourseMode.Manual;
            _manualGroup.SetActive(manual);
            _fieldText.text = CompactFieldName(data.AirportName) + "  " + FormatRange(data.DistanceNm);
            _courseText.text = manual
                ? "CRS " + FormatDegrees(data.Course) + (data.ToStation ? " TO" : " FR")
                : "BRG " + FormatDegrees(data.Bearing) + " DIR";
            _etaText.text = FormatEta(data) + "   " + Mathf.RoundToInt(data.GroundSpeedKnots) + "KT";

            if (manual)
                SetManualData(data);
            else
                SetAutoData(data);
        }
```

- [ ] **Step 3: Command an intercept when the needle is pegged**

Replace `SetManualData` so a saturated CDI produces a heading to fly instead of only an edge arrow, and so the scale label annunciates the active phase:

```csharp
        private void SetManualData(CdiData data)
        {
            float magnitude = Mathf.Abs(data.CrossTrackNm);
            string side = data.Side > 0 ? "R" : data.Side < 0 ? "L" : "ON";

            if (data.OffScale)
            {
                _actionText.text = "INTCP " + FormatDegrees(data.InterceptHeading) + "°";
                _actionText.color = UiColors.HudAmber;
            }
            else
            {
                _actionText.text = "XTK " + FormatCrossTrack(magnitude) + " " + side;
                _actionText.color = UiColors.HudGreen;
            }

            _needle.gameObject.SetActive(!data.OffScale);
            _needle.anchoredPosition = new Vector2(data.Deflection * ScaleHalfWidthPx, 0f);
            _offScaleLeft.color = data.OffScale && data.Deflection < 0f ? UiColors.HudAmber : Hidden;
            _offScaleRight.color = data.OffScale && data.Deflection > 0f ? UiColors.HudAmber : Hidden;
            _scaleLabel.text = ScaleTag(data.ScaleMode) + " " + FormatScale(data.FullScaleNm);
            _toFromFlag.text = data.ToStation ? "▲ TO" : "▼ FR";
        }
```

`SetAutoData` must reset the colour the manual branch may have left amber:

```csharp
        private void SetAutoData(CdiData data)
        {
            string command = data.SteeringError > 0.5f ? "R" : data.SteeringError < -0.5f ? "L" : "ON";
            _actionText.text = "CMD " + Mathf.Abs(data.SteeringError).ToString("F0") + "° " + command;
            _actionText.color = UiColors.HudGreen;
        }
```

- [ ] **Step 4: Add the formatting helpers**

Add these three static methods to `UI/CdiInstrument.cs` beside the existing `FormatScale`. The scale label widens to fit the tag, so also update the `_scaleLabel` size in `BuildManualScale` from `new Vector2(42f, 16f)` to `new Vector2(70f, 16f)`:

```csharp
        private static string FormatDegrees(float degrees)
        {
            int rounded = Mathf.RoundToInt(Mathf.Repeat(degrees, 360f));
            if (rounded == 360) rounded = 0;
            return rounded.ToString("000");
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

        private static string FormatEta(CdiData data)
        {
            if (!data.HasEta || data.EtaSeconds <= 0f || data.EtaSeconds > 359940f) return "ETA --:--";
            int total = Mathf.RoundToInt(data.EtaSeconds);
            return "ETA " + (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }
```

`359940` is 99 minutes 59 seconds — beyond that the `mm:ss` form overflows its two digits and the dashes are the honest output.

- [ ] **Step 5: Annunciate the same scale on the panel HSI**

The panel HSI reads the same auto-scaled `Deflection`, so it must say which scale it is showing or the bar is ambiguous. In `UI/PanelHsi.cs`, replace the `_courseReadout.text` assignment in `SetData`:

```csharp
            _courseReadout.text = manual
                ? $"CRS {Mathf.RoundToInt(data.Course):000}°  {ScaleTag(data.ScaleMode)}"
                : $"BRG {Mathf.RoundToInt(data.Bearing):000}°";
```

Add the matching helper to `PanelHsi`:

```csharp
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
```

- [ ] **Step 6: Build and verify**

```bash
dotnet build NOVor.csproj -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 7: Verify the reported failure case in game**

```bash
dotnet build NOVor.csproj -c Debug
```

In game, select a field roughly 25 NM away, set a manual course perpendicular to the bearing to reproduce a large cross-track, and confirm: the scale label reads `TERM 1NM`, the needle is hidden, one amber edge arrow is lit, and the action line reads `INTCP` with a three-digit heading in amber. Fly the intercept heading and confirm the needle reappears and centres as cross-track falls below 1 NM. Then fly outside 30 NM and confirm the label switches to `ENR 5NM`.

- [ ] **Step 8: Commit**

```bash
git add UI/CdiInstrument.cs UI/PanelHsi.cs
git commit -m "feat: annunciate CDI scale and command an intercept when off scale"
```

---

### Task 6: Cockpit direct-to and field-name polish

Recovering from a large offset currently means opening the panel or pressing `[`/`]` dozens of times at one degree per press. `SetCourseToBearing` already exists as a panel event and only needs a key. The field name also truncates mid-word — `MARIS AIRPORT` renders as `MARIS AIRP`.

**Files:**
- Modify: `Plugin.cs`
- Modify: `Core/NavController.cs:252-266`
- Modify: `UI/CdiInstrument.cs:67-78`

**Interfaces:**
- Consumes: `NavController.SetManualCourse(float)` (existing private method), `CdiData.Bearing`.
- Produces: `internal static ConfigEntry<KeyboardShortcut> DirectToKey` on `Plugin`.

- [ ] **Step 1: Bind the direct-to key**

Add the field to `Plugin.cs` beside the other hotkey fields:

```csharp
        internal static ConfigEntry<KeyboardShortcut> DirectToKey;
```

And bind it after the existing `CourseIncreaseKey` binding. Backslash sits next to the existing bracket course keys and is not bound by the game's default flight controls:

```csharp
            DirectToKey = Config.Bind("Hotkeys", "DirectTo", new KeyboardShortcut(KeyCode.Backslash),
                "Set the manual course to the current bearing to the selected field.");
```

- [ ] **Step 2: Handle the key**

In `NavController.HandleInput`, add this line after the `CourseIncreaseKey` line:

```csharp
            if (Plugin.DirectToKey.Value.IsDown()) SetManualCourse(Data.Bearing);
```

`SetManualCourse` already switches the mode to `CourseMode.Manual`, matching the panel's `SetCourseToBearing` behaviour exactly.

- [ ] **Step 3: Strip generic field suffixes and truncate on a word boundary**

`AIRPORT` and `AIRSTRIP` are absent from the current replacement list, and the 10-character cut lands mid-word. Replace `CompactFieldName` in `UI/CdiInstrument.cs`:

```csharp
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
            return compact.Trim().Length > 0 ? compact.Trim() : "NAV";
        }
```

`AIRSTRIP` must be replaced before `AIRPORT` would ever match it, and both must come before `AIRBASE`; the order above is already correct. `LastIndexOf(' ', 12)` searches backwards from index 12 and is only reached when the string is longer than that, so it cannot throw.

- [ ] **Step 4: Build and verify**

```bash
dotnet build NOVor.csproj -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 5: Verify in game**

```bash
dotnet build NOVor.csproj -c Debug
```

Confirm: the field previously shown as `MARIS AIRP` now reads `MARIS`; pressing `\` with a large cross-track snaps the course to the current bearing, switches the mode annunciation to `CRS`, recentres the needle, and clears the `INTCP` amber action line.

- [ ] **Step 6: Commit**

```bash
git add Plugin.cs Core/NavController.cs UI/CdiInstrument.cs
git commit -m "feat: add cockpit direct-to hotkey and clean field name compaction"
```

---

### Task 7: Documentation and full verification

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Update the code layout table**

In `AGENTS.md`, add these two rows to the Code Layout table and correct the `CdiInstrument` description:

```markdown
| `Core/CdiScale.cs` | range-based CDI scaling, off-scale state, and intercept guidance |
| `UI/CockpitHud.cs` | single owner of both cockpit surfaces (block and native tape cues) |
| `UI/HudGlyphs.cs` | shared cockpit font, outline, and glyph construction |
| `UI/CdiInstrument.cs` | green cockpit HUD CDI block (linear cues and text rows) |
```

Replace the existing `UI/CdiInstrument.cs` row rather than duplicating it.

- [ ] **Step 2: Record the new navigation semantics**

Replace the two Game API Patterns bullets that describe the fixed scale:

```markdown
- `CdiScale` owns CDI sensitivity: 5 NM enroute, 1 NM within 30 NM, and 0.3 NM within 2 NM, with hysteresis so the scale does not flutter at a threshold. `Navigation.AutoScaleCdi` disables it and falls back to `Navigation.FullDeflectionNauticalMiles`.
- MANUAL cockpit guidance uses a range-scaled CDI. Positive cross-track means the aircraft is right of course; the needle always moves toward the desired course. When cross-track exceeds full scale the needle is suppressed, one amber edge arrow lights, and the action line commands an intercept heading capped by `Navigation.MaxInterceptDegrees`.
- Both cockpit surfaces share one off-scale convention: the moving element is suppressed or clamped and a filled amber arrow marks the side.
- The cockpit block annunciates the active scale (`ENR`/`TERM`/`APP`/`FIX`), selected course or bearing, and ETA with groundspeed.
```

- [ ] **Step 3: Add the new hotkey**

Add to the Default Hotkeys list in `AGENTS.md`:

```markdown
- `\`: set manual course direct to the selected field's current bearing
```

- [ ] **Step 4: Update the conventions note about ownership**

Add to the Conventions list in `AGENTS.md`:

```markdown
- `NavController` owns exactly one cockpit component (`CockpitHud`); per-surface construction, visibility, and teardown live inside it.
- Cockpit glyphs are built through `HudGlyphs` so font resolution, outlining, and off-scale symbology stay identical across surfaces.
```

- [ ] **Step 5: Run the full verification cycle**

```bash
dotnet run --project tests/NavMathHarness -c Release
```

Expected: `NavMathHarness: 36 passed`.

```bash
dotnet build NOVor.csproj -c Release
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` and a regenerated `bin\Release\novor-1.0.0.zip`.

```bash
dotnet build NOVor.csproj -c Debug
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` with `NOVor.dll` and its PDB deployed to `$(NuclearOptionRoot)\BepInEx\scripts`.

- [ ] **Step 6: Commit**

```bash
git add AGENTS.md
git commit -m "docs: record consolidated cockpit HUD and range-scaled CDI"
```
