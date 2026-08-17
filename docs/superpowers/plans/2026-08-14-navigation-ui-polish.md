# Navigation UI Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make NO VOR CDI's navigation panel and in-flight HUD easier to read and interpret under flight workload while preserving its native Nuclear Option visual language.

**Architecture:** Keep navigation calculations in nautical miles and knots; add a small dependency-free presentation layer that formats user-facing course status and unit-dependent values. `NavPanel`, `PanelHsi`, and `CdiInstrument` will consume that presentation layer and the shared semantic palette. `HeadingTapeCues` remains the sole owner of native compass markers, adding bounded visual smoothing without changing steering math.

**Tech Stack:** C# net472, Unity 2022.3 uGUI, TextMeshPro, BepInEx 5, .NET 8 `NavMathHarness`.

---

## Scope and acceptance criteria

- The planning panel continues to use dark neutral surfaces with amber active state, while the flight HUD remains phosphor green with amber navigation commands.
- In the panel, sorting, friendly filtering, and navigation mode are visually and semantically distinct groups.
- `FROM` is written in full anywhere it represents the CDI state; the `FRIENDLY` field filter remains unchanged.
- Secondary labels and available runway controls are legible over the existing dark surfaces; selected, hover, pressed, and disabled states remain visually distinct.
- The selected row uses one amber selection rail and a separate, subtler faction marker with an explicit legend/label.
- HUD priority is: intercept command, course/bearing context, ETA and groundspeed. The amber compass marker has a unique navigation-cue shape and remains stable across heading-tape wraparound.
- Users can choose aviation units (`NM`/`KT`) or metric units (`km`/`km/h`) for navigation distance and speed. Internal math, CDI scaling, and config values stay in nautical miles and knots.
- `dotnet run --project tests\NavMathHarness -c Release`, `dotnet build NOVor.csproj -c Release`, and `dotnet build NOVor.csproj -c Debug` all pass.

## File structure

| Path | Change | Responsibility after change |
|---|---|---|
| `Core/NavigationPresentation.cs` | Create | Dependency-free unit and terminology formatting used by panel/HUD surfaces. |
| `Plugin.cs` | Modify | Persist `DisplayUnits` and heading-cue response configuration. |
| `UI/UiColors.cs` | Modify | Name contrast-safe panel and HUD semantic colors; remove direct presentation hex usage. |
| `UI/NavPanel.cs` | Modify | Group toolbars, persistent search label, selected/faction row treatment, runway states, formatted panel readout. |
| `UI/PanelHsi.cs` | Modify | Show `FROM` in full and align HSI copy with the HUD/panel terminology. |
| `UI/CdiInstrument.cs` | Modify | Apply explicit course status, unit formatting, and the intended three-level HUD hierarchy. |
| `UI/HeadingTapeCues.cs` | Modify | Use a distinct navigation marker and smooth visual cue deltas with bounded response. |
| `UI/HudGlyphs.cs` | Modify | Add named glyph constants for the course and steering markers rather than embedding glyphs in consumers. |
| `tests/NavMathHarness/NavMathHarness.csproj` | Modify | Link the new dependency-free presentation file. |
| `tests/NavMathHarness/Program.cs` | Modify | Cover terminology and both formatting modes. |
| `AGENTS.md` | Modify | Document display-unit semantics and the cockpit cue convention. |

### Task 1: Add a tested presentation contract for units and CDI terminology

**Files:**

- Create: `Core/NavigationPresentation.cs`
- Modify: `tests/NavMathHarness/NavMathHarness.csproj`
- Modify: `tests/NavMathHarness/Program.cs`

- [ ] **Step 1: Write the failing formatting tests**

  Add the new file to the harness project:

  ```xml
  <Compile Include="..\..\Core\NavigationPresentation.cs" Link="Core\NavigationPresentation.cs" />
  ```

  Then append these checks before the existing failure exit in `Program.Main`:

  ```csharp
  Equal("9.7 NM", NavigationPresentation.FormatDistance(9.7f, NavigationDisplayUnits.Aviation), "aviation distance");
  Equal("18.0 km", NavigationPresentation.FormatDistance(9.7f, NavigationDisplayUnits.Metric), "metric distance");
  Equal("166 KT", NavigationPresentation.FormatSpeed(166f, NavigationDisplayUnits.Aviation), "aviation speed");
  Equal("307 km/h", NavigationPresentation.FormatSpeed(166f, NavigationDisplayUnits.Metric), "metric speed");
  Equal("TO", NavigationPresentation.ToFromLabel(true), "to label");
  Equal("FROM", NavigationPresentation.ToFromLabel(false), "from label");
  ```

  Add this overload alongside the existing numeric `Equal` helper so the assertions compile without changing its tolerance behavior:

  ```csharp
  private static void Equal(string expected, string actual, string name)
  {
      if (string.Equals(expected, actual, StringComparison.Ordinal)) return;
      Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
      _failures++;
  }
  ```

- [ ] **Step 2: Run the harness and verify it fails because the presentation API is absent**

  Run:

  ```powershell
  dotnet run --project tests\NavMathHarness -c Release
  ```

  Expected: compilation errors naming `NavigationPresentation` and `NavigationDisplayUnits`.

- [ ] **Step 3: Implement the dependency-free presentation API**

  Create `Core/NavigationPresentation.cs` with this public contract. Keep it free of `UnityEngine` so the existing harness can compile it directly.

  ```csharp
  using System;

  namespace NOVor.Core
  {
      public enum NavigationDisplayUnits
      {
          Aviation,
          Metric
      }

      public static class NavigationPresentation
      {
          private const float KilometersPerNauticalMile = 1.852f;

          public static string FormatDistance(float nauticalMiles, NavigationDisplayUnits units)
          {
              if (units == NavigationDisplayUnits.Metric)
                  return (nauticalMiles * KilometersPerNauticalMile).ToString("0.0") + " km";
              return nauticalMiles.ToString("0.0") + " NM";
          }

          public static string FormatSpeed(float knots, NavigationDisplayUnits units)
          {
              if (units == NavigationDisplayUnits.Metric)
                  return Math.Round(knots * KilometersPerNauticalMile).ToString("0") + " km/h";
              return Math.Round(knots).ToString("0") + " KT";
          }

          public static string ToFromLabel(bool toStation)
          {
              return toStation ? "TO" : "FROM";
          }
      }
  }
  ```

  Do not move CDI scale calculations or field-elevation formatting into this class: all current navigation math and elevation-in-meters semantics remain unchanged.

- [ ] **Step 4: Run the harness and record the expected pass count**

  Run:

  ```powershell
  dotnet run --project tests\NavMathHarness -c Release
  ```

  Expected: exit code 0 and the final line reports the original 36 checks plus the six new presentation checks.

- [ ] **Step 5: Commit the isolated formatting contract**

  ```powershell
  git add Core\NavigationPresentation.cs tests\NavMathHarness\NavMathHarness.csproj tests\NavMathHarness\Program.cs
  git commit -m "feat: add navigation presentation formatting"
  ```

### Task 2: Establish semantic contrast and configurable presentation settings

**Files:**

- Modify: `UI/UiColors.cs`
- Modify: `Plugin.cs`

- [ ] **Step 1: Add failing compile references to the new configuration values**

  In the code consumers to be changed in Tasks 3–5, reference `Plugin.DisplayUnits.Value` and `Plugin.HeadingCueResponseDegreesPerSecond.Value`. Do not add temporary local defaults. This makes the next build fail until persistence is wired correctly.

- [ ] **Step 2: Define the palette roles in `UiColors`**

  Retain the established charcoal/amber/blue/green palette, but replace ambiguous low-contrast uses with named roles. Add these fields after the existing panel colors:

  ```csharp
  public static readonly Color PanelSecondaryText = Hex(0xaeb6b1);
  public static readonly Color PanelDisabledText = Hex(0x727b76);
  public static readonly Color PanelInteractive = Hex(0xc8cfca);
  public static readonly Color SelectionSurface = Hex(0x342b1b);
  public static readonly Color FactionRail = Hex(0x4f8fe0, 0.72f);
  public static readonly Color HudContext = Hex(0xb6dfc2);
  public static readonly Color HudSupport = Hex(0x8fb99b);
  ```

  Remove the earlier duplicate `SelectionSurface` declaration rather than creating two values. Keep `HudAmber` for an actual command or attention state only.

- [ ] **Step 3: Persist display and smoothing settings in `Plugin.Awake`**

  Add fields alongside the existing HUD config fields:

  ```csharp
  internal static ConfigEntry<NavigationDisplayUnits> DisplayUnits;
  internal static ConfigEntry<float> HeadingCueResponseDegreesPerSecond;
  ```

  Bind them after `MaxInterceptDegrees` and after `HudNudgeStep`, respectively:

  ```csharp
  DisplayUnits = Config.Bind("Navigation", "DisplayUnits", NavigationDisplayUnits.Aviation,
      "Display navigation range and groundspeed in Aviation (NM/KT) or Metric (km/km/h) units.");

  HeadingCueResponseDegreesPerSecond = Config.Bind("Hud", "HeadingCueResponseDegreesPerSecond", 360f,
      new ConfigDescription("Maximum visual movement rate of heading-tape navigation cues.",
          new AcceptableValueRange<float>(90f, 1080f)));
  ```

  Add `using NOVor.Core;` only if `Plugin.cs` does not already resolve the enum through its namespace.

- [ ] **Step 4: Run the production build**

  Run:

  ```powershell
  dotnet build NOVor.csproj -c Release
  ```

  Expected: 0 errors. Resolve all stale color references in the same change; do not leave an unused competing palette role.

- [ ] **Step 5: Commit the visual and user-preference foundation**

  ```powershell
  git add UI\UiColors.cs Plugin.cs
  git commit -m "polish: define navigation contrast and display settings"
  ```

### Task 3: Clarify the navigation panel's control hierarchy and list states

**Files:**

- Modify: `UI/NavPanel.cs:13-221, 506-596, 788-831, 921-1098`

- [ ] **Step 1: Split the existing search toolbar into explicit sorting and filtering groups**

  In `BuildSearchRow`, keep the input on the left, wrap `NEAR` and `A–Z` in a child `SortGroup`, and put `FRIENDLY` in a sibling `FilterGroup`. Each group must have a noninteractive uppercase caption (`SORT` and `FILTER`) in `UiColors.PanelSecondaryText` above or immediately before its controls. Preserve the existing callbacks: `SetSortNearest`, `SetSortName`, and `ToggleFriendlyOnly`.

  The visible order must be:

  ```text
  SEARCH [input]   SORT  [NEAR] [A–Z]   FILTER  [FRIENDLY]
  ```

  Keep the existing active amber rail. Add selected text weight in `StyleToggle` so active state is represented by both rail and typography.

- [ ] **Step 2: Give the search field a persistent label**

  Change `MakeInput` to accept `labelText` separately from `placeholder`, then build a `TextMeshProUGUI` label inside the input root at its top-left. Use `SEARCH` as the label and `Search fields` as the placeholder. Offset the text viewport down by 7 px so typed text does not overlap the label.

  Keep `TMP_InputField.placeholder` for an empty field, but never make placeholder text the only statement of the field's purpose.

- [ ] **Step 3: Make selection, affiliation, and disabled states distinct**

  In `AddAirportRow` and `RefreshRows`:

  - Retain the 3 px amber `SelectionRail` as the sole selected-state rail.
  - Change the 5 px `FactionRail` to `UiColors.FactionRail` for friendly fields and make unknown/enemy rails use their existing faction colors at 0.55 alpha.
  - Add a noninteractive `AFFILIATION` legend in the column header with a blue 5 px sample rail and `FRIENDLY` text; do not use color alone to convey the blue rail's meaning.
  - Render unselected names with `PanelSecondaryText` and numeric columns with `PanelInteractive`; retain `PanelText` + bold for the selected name.
  - Use `PanelDisabledText` for unavailable position values (`---`, `--.-`) and ensure any unavailable row is not presented as selectable navigation data.

  Preserve the selected-field pinning behavior in `DisplayedAirports`; it is valuable because filters must not silently hide the active navigation target.

- [ ] **Step 4: Improve button state feedback and header affordances**

  Update `ApplyButtonTint` to use the semantic text colors and a 0.12–0.18 second fade. Keep layout bounds fixed. In `StyleAction`, `StyleToggle`, and `StyleHeaderControl`, set labels to:

  ```csharp
  label.color = selected ? UiColors.Amber : UiColors.PanelSecondaryText;
  label.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
  ```

  Increase the minimize and close button layout widths and heights to at least `36f` while retaining their current visual glyph size. Their hover state must be brighter than idle and their pressed state darker.

- [ ] **Step 5: Verify the panel in game**

  Build Debug, open the panel, and verify all of the following:

  - `SORT` and `FILTER` identify the different toolbar concepts without reading a tooltip.
  - `SEARCH` remains visible while text is entered.
  - The active selection, friendly affiliation, and pinned target can each be identified independently.
  - `A–Z`, `NEAR`, and `FRIENDLY` visibly change state on click and preserve selection/scroll behavior.
  - The close/minimize controls are easily clickable without resizing or moving the panel.

  Run:

  ```powershell
  dotnet build NOVor.csproj -c Debug
  ```

- [ ] **Step 6: Commit the planner readability changes**

  ```powershell
  git add UI\NavPanel.cs
  git commit -m "polish: clarify panel controls and field states"
  ```

### Task 4: Make HSI, runway controls, and panel telemetry unambiguous

**Files:**

- Modify: `UI/NavPanel.cs:367-403, 619-751`
- Modify: `UI/PanelHsi.cs:25-81`

- [ ] **Step 1: Replace abbreviated CDI terminology in the HSI**

  In `PanelHsi.SetData`, replace:

  ```csharp
  _toFromFlag.text = manual ? data.ToStation ? "TO" : "FR" : "DIR";
  ```

  with:

  ```csharp
  _toFromFlag.text = manual
      ? NavigationPresentation.ToFromLabel(data.ToStation)
      : "DIRECT";
  ```

  Widen `FlagWell` and `_toFromFlag` from 44/40 px to 64/60 px so `FROM` is never compressed. Keep the field as context, not the primary course command.

- [ ] **Step 2: Give runway buttons an available state and an explicit selection state**

  In `RefreshRunwaySelection`, style every displayed runway as an available action using `UiColors.PanelInteractive` text and `UiColors.ChromeRaised` surface. Apply amber rail + bold only to the runway that matches `data.Course` within one degree. When no runway matches, leave all buttons visibly available rather than styling them like disabled controls.

  Add `button.navigation = new Navigation { mode = Navigation.Mode.None };` when creating each runway button so keyboard focus movement cannot enter an accidental spatial order while the panel is open.

- [ ] **Step 3: Format panel telemetry through the presentation layer**

  Replace the raw strings in `RefreshReadout` with:

  ```csharp
  NavigationDisplayUnits units = Plugin.DisplayUnits.Value;
  string gs = NavigationPresentation.FormatSpeed(_navigation.GroundSpeedKnots, units);
  string range = NavigationPresentation.FormatDistance(info.DistanceNm, units);
  ```

  Add `RNG` before `range`, retain `ELEV` in meters, and use centered-dot separators:

  ```text
  GS 166 KT  ·  ETA 03:30  ·  RNG 9.7 NM  ·  ELEV 146 m
  ```

  Increase the readout's preferred height if required rather than allowing the last value to clip. The `STEER` row remains the panel's dominant operational readout.

- [ ] **Step 4: Apply the same units to runway length and the panel header**

  In `RefreshRunways`, replace the hard-coded nautical-mile suffix with `NavigationPresentation.FormatDistance(lengthNm, Plugin.DisplayUnits.Value)`. In `RefreshHeader`, use the same formatter for the selected-field range. Preserve bearing in degrees in every display mode.

- [ ] **Step 5: Verify both display-unit modes and manual/direct modes**

  In game, choose a field with runways, then:

  - Switch `DisplayUnits` from `Aviation` to `Metric` and confirm range, runway length, and groundspeed all change together while elevation stays meters.
  - Switch between `DIRECT TO` and `MANUAL`; confirm the HSI reads `DIRECT`, `TO`, or `FROM` without `FR`.
  - Select a runway, confirm its button becomes amber/bold, then adjust course by one degree and confirm it returns to the available neutral state.

- [ ] **Step 6: Commit the HSI and panel telemetry changes**

  ```powershell
  git add UI\NavPanel.cs UI\PanelHsi.cs
  git commit -m "polish: clarify HSI and navigation telemetry"
  ```

### Task 5: Refine the cockpit block's priority and terminology

**Files:**

- Modify: `UI/CdiInstrument.cs:35-80, 134-216`
- Modify: `UI/HudGlyphs.cs:5-8`

- [ ] **Step 1: Name cockpit marker glyphs centrally**

  Add these constants in `HudGlyphs` beside `OffScaleLeft` and `OffScaleRight`:

  ```csharp
  public const string CourseCue = "▽";
  public const string SteeringCue = "◆";
  ```

  Keep off-scale glyphs unchanged. Consumers must refer to named constants rather than embedding marker characters.

- [ ] **Step 2: Replace the cockpit `FR` abbreviation and use presentation formatting**

  In `CdiInstrument.SetData` and `SetManualData`, use:

  ```csharp
  string toFrom = NavigationPresentation.ToFromLabel(data.ToStation);
  _courseText.text = manual
      ? "CRS " + FormatDegrees(data.Course) + "° · " + toFrom
      : "BRG " + FormatDegrees(data.Bearing) + "° · DIRECT";
  _toFromFlag.text = data.ToStation ? "▲ TO" : "▼ FROM";
  _etaText.text = FormatEta(data) + "  ·  " +
      NavigationPresentation.FormatSpeed(data.GroundSpeedKnots, Plugin.DisplayUnits.Value);
  _fieldText.text = CompactFieldName(data.AirportName) + "  " +
      NavigationPresentation.FormatDistance(data.DistanceNm, Plugin.DisplayUnits.Value);
  ```

  Widen the TO/FROM cue from 48 px to 64 px and move it 8 px farther left so it remains visually separate from the CDI scale.

- [ ] **Step 3: Encode the three-level HUD hierarchy in type and color**

  Use the following values in `Build`:

  ```csharp
  _actionText = HudGlyphs.MakeText("Action", UiColors.HudAmber, 18, FontStyle.Bold);
  _courseText = HudGlyphs.MakeText("Course", UiColors.HudContext, 12, FontStyle.Bold);
  _etaText = HudGlyphs.MakeText("Eta", UiColors.HudSupport, 11, FontStyle.Normal);
  ```

  In `SetManualData`, leave `_actionText` amber only when off-scale (`INTCP nnn°`). For on-scale cross-track, change it to `UiColors.HudContext` and keep the action text smaller through the single text style rather than adding another competing highlight. In `SetAutoData`, show `STEER nnn°` instead of `CMD nnn°` so the cockpit and panel use the same instruction verb.

- [ ] **Step 4: Verify contrast against representative flight scenes**

  Test the block against bright cloud, pale terrain, cockpit frame, and dark/night backgrounds. Confirm that context and support text remain readable while the amber intercept command is still the most prominent line. Verify no line overlaps the HUD reticle at the default `OffsetY = -180`.

- [ ] **Step 5: Commit the cockpit readout polish**

  ```powershell
  git add UI\CdiInstrument.cs UI\HudGlyphs.cs
  git commit -m "polish: prioritize cockpit navigation guidance"
  ```

### Task 6: Make native heading-tape guidance self-explanatory and stable

**Files:**

- Modify: `UI/HeadingTapeCues.cs:7-66`
- Test: in-game heading-tape acceptance sweep

- [ ] **Step 1: Distinguish course context from the amber steering command**

  Replace the literal glyph values in `SetData` with `HudGlyphs.CourseCue` for the green course/bearing context and `HudGlyphs.SteeringCue` for the amber steering command. Keep the two cues on their existing separate vertical lanes so the command never masks the course cue.

- [ ] **Step 2: Add bounded visual smoothing without changing `CdiData` or NavMath**

  Add these fields to `HeadingTapeCues`:

  ```csharp
  private bool _hasSmoothedDeltas;
  private float _smoothedCourseDelta;
  private float _smoothedSteerDelta;
  ```

  At the start of `SetData`, compute raw deltas exactly as today. Then use `Mathf.MoveTowardsAngle` and `Time.unscaledDeltaTime` before calling `SetCue`:

  ```csharp
  float maxStep = Plugin.HeadingCueResponseDegreesPerSecond.Value * Time.unscaledDeltaTime;
  if (!_hasSmoothedDeltas)
  {
      _smoothedCourseDelta = courseDelta;
      _smoothedSteerDelta = steerDelta;
      _hasSmoothedDeltas = true;
  }
  else
  {
      _smoothedCourseDelta = Mathf.MoveTowardsAngle(_smoothedCourseDelta, courseDelta, maxStep);
      _smoothedSteerDelta = Mathf.MoveTowardsAngle(_smoothedSteerDelta, steerDelta, maxStep);
  }
  ```

  Pass the smoothed values to `SetCue`. Do not smooth `data.Course`, `data.SteerHeading`, or the steering solution itself.

- [ ] **Step 3: Reset smoothing at visibility and reinitialization boundaries**

  Set `_hasSmoothedDeltas = false` in `SetVisible(false)` and at the end of `Initialize`. This prevents an old marker position from sweeping across the tape after HUD toggle, scene change, or hot reload.

- [ ] **Step 4: Verify the native tape under edge conditions**

  In flight, test all of the following:

  - Current heading 359° and command 001°, then the inverse; cue takes the two-degree route rather than crossing the full tape.
  - Command at each visible tape edge; it becomes the correct left/right off-scale arrow.
  - Rapid aircraft turns and target changes; cue movement is stable but reaches a new command promptly.
  - Toggle HUD and hot-reload; cues reappear directly at their current targets with no stale sweep.

- [ ] **Step 5: Commit the heading-tape cue polish**

  ```powershell
  git add UI\HeadingTapeCues.cs UI\HudGlyphs.cs
  git commit -m "polish: stabilize native navigation cues"
  ```

### Task 7: Document and complete the regression pass

**Files:**

- Modify: `AGENTS.md`
- Verify: `tests/NavMathHarness/Program.cs`, `NOVor.csproj`, Debug deployment

- [ ] **Step 1: Update the project contract in `AGENTS.md`**

  Replace the current units statement with:

  ```markdown
  - Navigation math, CDI scaling, and config inputs remain nautical miles/knots internally. `Navigation.DisplayUnits` controls only user-facing range, runway length, and groundspeed text (`Aviation` = NM/KT, `Metric` = km/km/h). Field elevation remains meters.
  ```

  Add this heading-tape convention beside the existing course-caret convention:

  ```markdown
  - The green outlined triangle is course/bearing context; the amber outlined diamond is the active steering command. Heading-tape cue motion is display-only and bounded by `Hud.HeadingCueResponseDegreesPerSecond`.
  ```

- [ ] **Step 2: Run the complete automated suite**

  Run in this order:

  ```powershell
  dotnet run --project tests\NavMathHarness -c Release
  dotnet build NOVor.csproj -c Release
  dotnet build NOVor.csproj -c Debug
  ```

  Expected: all commands exit 0; the harness includes the presentation checks added in Task 1; Debug deploys only to `BepInEx\scripts` when `NuclearOptionRoot` is configured.

- [ ] **Step 3: Perform the in-game acceptance pass**

  Capture four screenshots for review:

  1. Panel, selected friendly field, nearest sort, aviation units.
  2. Panel, selected field pinned through `FRIENDLY` filter, A–Z sort, metric units.
  3. Manual off-scale intercept showing `INTCP`, `FROM`, and the amber tape diamond.
  4. Direct-to state showing `DIRECT`, the green course cue, and an amber steering cue at a different heading.

  Also test F9 open/close, cursor/camera restoration, minimization, all toolbar buttons, a selected runway, heading 359°/000° wrap, and one hot reload.

- [ ] **Step 4: Commit documentation and final verification**

  ```powershell
  git add AGENTS.md
  git commit -m "docs: describe navigation presentation conventions"
  ```

## Review checklist

- The plan covers every prior review theme: contrast, control grouping, persistent search labeling, selected/faction states, readable runway states, `FROM` terminology, HUD hierarchy, marker clarity, smoothing, unit consistency, and regression coverage.
- No navigation geometry, intercept calculation, CDI scale, cursor ownership, or hot-reload cleanup is moved out of its existing owner.
- Every new cross-surface string is either defined in `NavigationPresentation` or is a clearly named control label; `FR` and raw unit suffixes are removed from user-facing CDI context.
- The presentation contract is testable without game assemblies; all Unity visual behavior has explicit in-game acceptance checks.
