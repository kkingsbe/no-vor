# Runway-Aware Course Capture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Guide the pilot onto the selected runway's actual inbound centerline, roll out smoothly, and be established by 3 NM from the threshold while keeping the heading tape as the primary flying surface.

**Architecture:** Add a dependency-free runway-guidance policy that consumes runway-relative cross-track, along-track distance, speed, track, and observed drift. `NavController` will retain the selected runway and its threshold geometry, feed the policy, and expose runway/phase identity through `CdiData`; existing panel and cockpit components remain presentation-only consumers.

**Tech Stack:** C# net472, Unity 2022.3 uGUI, BepInEx 5, dependency-free .NET 8 navigation harness.

---

## Scope

- Selecting a runway creates a distinct runway-navigation state instead of discarding everything except its numeric heading.
- The lateral path is the selected runway's inbound extended centerline through its actual threshold.
- Guidance plans capture at or before a fixed 3 NM final gate.
- Intercept angle uses available distance; rollout begins early based on groundspeed rather than switching sides at centerline.
- Established-state hysteresis prevents cue chatter.
- The green outlined tape triangle remains runway-course context.
- The amber diamond remains the drift-corrected aircraft heading to fly. The pilot aligns the native current-heading marker with this diamond; wind may keep it offset from the green course triangle.
- The cockpit block names the selected runway and includes the diamond symbol in its command.
- Course nudges preserve runway identity and rotate the approach line about the threshold. `SET BRG`, `SET HDG`, `RECIP`, direct-to, airport changes, and manual-mode selection clear runway identity.

## Non-goals

- Vertical or glidepath guidance.
- Automatic throttle, bank, turn, gear, or flight-control inputs.
- Mobile-carrier/deck-specific capture behavior beyond reevaluating current threshold positions.
- Arrival procedures, multiple waypoints, traffic patterns, or automatic runway selection.
- Reworking the airport list, HSI artwork, tape icon artwork, or input-binding system.

## File structure

| Path | Change | Responsibility after change |
|---|---|---|
| `Core/RunwayGuidance.cs` | Create | Pure 3 NM capture, speed-aware rollout, phase, and drift-corrected command policy. |
| `Core/NavModels.cs` | Modify | Add runway threshold geometry and the runway course mode. |
| `Core/CdiData.cs` | Modify | Carry selected-runway identity, threshold distance, and guidance phase to UI. |
| `Core/NavController.cs` | Modify | Own runway selection lifecycle and convert Unity positions to runway-relative guidance inputs. |
| `Core/CockpitPresentation.cs` | Modify | Present runway identity and connect the command text to the amber diamond. |
| `UI/NavPanel.cs` | Modify | Pass full runway identity, retain selected styling, and distinguish runway from generic manual state. |
| `UI/PanelHsi.cs` | Modify | Treat runway mode as lateral-course mode and label it `RWY`. |
| `UI/CdiInstrument.cs` | Modify | Pass runway/phase fields into the presentation contract. |
| `tests/NavMathHarness/NavMathHarness.csproj` | Modify | Link the pure runway-guidance source. |
| `tests/NavMathHarness/Program.cs` | Modify | Cover geometry, 3 NM planning, rollout, hysteresis, drift, and cockpit copy. |
| `AGENTS.md` | Modify | Record runway-selection, cue, and capture semantics. |

### Task 1: Define and test the runway-guidance policy

**Files:**

- Create: `Core/RunwayGuidance.cs`
- Modify: `tests/NavMathHarness/NavMathHarness.csproj`
- Modify: `tests/NavMathHarness/Program.cs`

- [ ] **Step 1: Link the absent policy into the harness**

Add after the `GuidanceMath.cs` compile item:

```xml
<Compile Include="..\..\Core\RunwayGuidance.cs" Link="Core\RunwayGuidance.cs" />
```

- [ ] **Step 2: Add failing capture-policy checks**

Add these checks before the existing cockpit-presentation checks:

```csharp
var planned = RunwayGuidance.Evaluate(new RunwayGuidanceInput
{
    CourseDegrees = 180d,
    CrossTrackNm = 4d,
    AlongTrackToThresholdNm = 15d,
    GroundSpeedKnots = 300d,
    HeadingDegrees = 180d,
    GroundTrackDegrees = 180d,
    MaxInterceptDegrees = 45d
});
Equal(161.565d, planned.DesiredTrackDegrees, "runway capture aims for three mile gate");
Same(RunwayGuidancePhase.Intercept, planned.Phase, "runway starts in intercept phase");

var late = RunwayGuidance.Evaluate(new RunwayGuidanceInput
{
    CourseDegrees = 180d,
    CrossTrackNm = 1d,
    AlongTrackToThresholdNm = 2.5d,
    GroundSpeedKnots = 300d,
    HeadingDegrees = 180d,
    GroundTrackDegrees = 180d,
    MaxInterceptDegrees = 45d
});
Equal(135d, late.DesiredTrackDegrees, "inside gate uses maximum recovery intercept");

var capture = RunwayGuidance.Evaluate(new RunwayGuidanceInput
{
    CourseDegrees = 180d,
    CrossTrackNm = 0.2d,
    AlongTrackToThresholdNm = 6d,
    GroundSpeedKnots = 300d,
    HeadingDegrees = 180d,
    GroundTrackDegrees = 180d,
    MaxInterceptDegrees = 45d
});
True(capture.DesiredTrackDegrees > 135d && capture.DesiredTrackDegrees < 180d,
    "speed-aware rollout shallows before centerline");
Same(RunwayGuidancePhase.Capture, capture.Phase, "rollout enters capture phase");

var established = RunwayGuidance.Evaluate(new RunwayGuidanceInput
{
    CourseDegrees = 180d,
    CrossTrackNm = 0.03d,
    AlongTrackToThresholdNm = 3d,
    GroundSpeedKnots = 300d,
    HeadingDegrees = 180d,
    GroundTrackDegrees = 183d,
    MaxInterceptDegrees = 45d
});
Same(RunwayGuidancePhase.Established, established.Phase, "centered inbound is established");

var held = RunwayGuidance.Evaluate(new RunwayGuidanceInput
{
    CourseDegrees = 180d,
    CrossTrackNm = 0.08d,
    AlongTrackToThresholdNm = 2d,
    GroundSpeedKnots = 300d,
    HeadingDegrees = 180d,
    GroundTrackDegrees = 188d,
    MaxInterceptDegrees = 45d,
    WasEstablished = true
});
Same(RunwayGuidancePhase.Established, held.Phase, "established hysteresis prevents chatter");

var wind = RunwayGuidance.Evaluate(new RunwayGuidanceInput
{
    CourseDegrees = 180d,
    CrossTrackNm = 0d,
    AlongTrackToThresholdNm = 3d,
    GroundSpeedKnots = 300d,
    HeadingDegrees = 170d,
    GroundTrackDegrees = 180d,
    MaxInterceptDegrees = 45d
});
Equal(170d, wind.CommandHeadingDegrees, "command diamond retains wind correction");

var passed = RunwayGuidance.Evaluate(new RunwayGuidanceInput
{
    CourseDegrees = 180d,
    CrossTrackNm = 0.02d,
    AlongTrackToThresholdNm = -0.1d,
    GroundSpeedKnots = 300d,
    HeadingDegrees = 180d,
    GroundTrackDegrees = 180d,
    MaxInterceptDegrees = 45d
});
Same(RunwayGuidancePhase.Passed, passed.Phase, "threshold crossing does not reverse guidance");
Equal(180d, passed.DesiredTrackDegrees, "passed runway holds inbound course");
```

Add this enum assertion helper beside `Same(CdiScaleMode, ...)`:

```csharp
private static void Same(RunwayGuidancePhase expected, RunwayGuidancePhase actual, string name)
{
    if (expected == actual) return;
    Console.Error.WriteLine($"FAIL {name}: expected {expected}, actual {actual}");
    _failures++;
}
```

- [ ] **Step 3: Run the harness and verify the missing source fails**

Run:

```powershell
dotnet run --project tests\NavMathHarness -c Release
```

Expected: compilation fails because `Core/RunwayGuidance.cs` does not exist.

- [ ] **Step 4: Implement the pure policy**

Create `Core/RunwayGuidance.cs`:

```csharp
using System;

namespace NOVor.Core
{
    public enum RunwayGuidancePhase
    {
        None,
        Intercept,
        Capture,
        Established,
        Passed
    }

    public struct RunwayGuidanceInput
    {
        public double CourseDegrees;
        public double CrossTrackNm;
        public double AlongTrackToThresholdNm;
        public double GroundSpeedKnots;
        public double HeadingDegrees;
        public double GroundTrackDegrees;
        public double MaxInterceptDegrees;
        public bool WasEstablished;
    }

    public struct RunwayGuidanceOutput
    {
        public double DesiredTrackDegrees;
        public double CommandHeadingDegrees;
        public double RolloutDistanceNm;
        public RunwayGuidancePhase Phase;
    }

    public static class RunwayGuidance
    {
        public const double FinalGateDistanceNm = 3d;
        private const double RolloutSeconds = 8d;
        private const double MinimumRolloutNm = 0.15d;
        private const double MaximumRolloutNm = 1d;
        private const double EstablishCrossTrackNm = 0.05d;
        private const double ReleaseCrossTrackNm = 0.1d;
        private const double EstablishTrackErrorDegrees = 5d;
        private const double ReleaseTrackErrorDegrees = 10d;

        public static RunwayGuidanceOutput Evaluate(RunwayGuidanceInput input)
        {
            double trackError = Math.Abs(NavMath.DeltaAngleDegrees(
                input.CourseDegrees, input.GroundTrackDegrees));
            bool established = input.WasEstablished
                ? Math.Abs(input.CrossTrackNm) <= ReleaseCrossTrackNm &&
                    trackError <= ReleaseTrackErrorDegrees
                : Math.Abs(input.CrossTrackNm) <= EstablishCrossTrackNm &&
                    trackError <= EstablishTrackErrorDegrees;

            double speedNmPerSecond = Math.Max(0d, input.GroundSpeedKnots) / 3600d;
            double rolloutNm = Clamp(speedNmPerSecond * RolloutSeconds,
                MinimumRolloutNm, MaximumRolloutNm);
            double magnitude = Math.Abs(input.CrossTrackNm);
            double availableNm = input.AlongTrackToThresholdNm - FinalGateDistanceNm;
            double requiredAngle = availableNm > 0.05d
                ? Math.Atan2(magnitude, availableNm) * 180d / Math.PI
                : input.MaxInterceptDegrees;
            double interceptAngle = Math.Min(input.MaxInterceptDegrees, requiredAngle);

            RunwayGuidancePhase phase;
            if (input.AlongTrackToThresholdNm < 0d)
            {
                phase = RunwayGuidancePhase.Passed;
                interceptAngle = 0d;
            }
            else if (established)
            {
                phase = RunwayGuidancePhase.Established;
                interceptAngle = 0d;
            }
            else if (magnitude <= rolloutNm)
            {
                phase = RunwayGuidancePhase.Capture;
                interceptAngle *= magnitude / rolloutNm;
            }
            else
            {
                phase = RunwayGuidancePhase.Intercept;
            }

            double desiredTrack = NavMath.NormalizeDegrees(input.CrossTrackNm > 0d
                ? input.CourseDegrees - interceptAngle
                : input.CourseDegrees + interceptAngle);
            return new RunwayGuidanceOutput
            {
                DesiredTrackDegrees = desiredTrack,
                CommandHeadingDegrees = NavMath.DriftCorrectedHeadingDegrees(desiredTrack,
                    input.HeadingDegrees, input.GroundTrackDegrees),
                RolloutDistanceNm = rolloutNm,
                Phase = phase
            };
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
```

- [ ] **Step 5: Run the harness**

Run:

```powershell
dotnet run --project tests\NavMathHarness -c Release
```

Expected: the new runway-guidance checks pass. Update the final reported count from 65 to the actual assertion total.

- [ ] **Step 6: Commit the isolated policy**

```powershell
git add Core\RunwayGuidance.cs tests\NavMathHarness\NavMathHarness.csproj tests\NavMathHarness\Program.cs
git commit -m "feat: add runway capture guidance policy"
```

### Task 2: Preserve selected runway identity and threshold geometry

**Files:**

- Modify: `Core/NavModels.cs`
- Modify: `Core/CdiData.cs`
- Modify: `UI/NavPanel.cs:81-98, 652-746`
- Modify: `Core/NavController.cs:15-55, 173-214, 270-371`

- [ ] **Step 1: Extend the navigation models**

Add `Runway` to `CourseMode` and extend `RunwayInfo`:

```csharp
public enum CourseMode
{
    Auto,
    Manual,
    Runway
}

public struct RunwayInfo
{
    public string Label;
    public float Heading;
    public float LengthMeters;
    public Vector3 ThresholdPosition;
    public Vector3 DepartureEndPosition;
}
```

Add these fields to `CdiData`:

```csharp
public string RunwayLabel;
public float AlongTrackToThresholdNm;
public RunwayGuidancePhase RunwayPhase;
public bool HasRunway;
```

- [ ] **Step 2: Pass the full runway selection out of the panel**

Replace the event and invocation in `NavPanel`:

```csharp
public event Action<int, RunwayInfo> RunwaySelected;

private void SelectRunway(int index, RunwayInfo runway)
{
    _selectedRunwayIndex = index;
    RefreshRunwaySelection(runway.Heading);
    RunwaySelected?.Invoke(index, runway);
}
```

Build each runway button with the full value:

```csharp
RunwayInfo runway = runways[i];
int runwayIndex = i;
var button = MakeFlexButton(row, label, () => SelectRunway(runwayIndex, runway), 9);
```

Delete the course-difference block that resets `_selectedRunwayIndex` in `RefreshRunwaySelection`. Runway provenance remains selected after course nudges; only controller lifecycle events clear it.

Add an explicit panel reset used by the controller:

```csharp
public void ClearRunwaySelection()
{
    _selectedRunwayIndex = -1;
    for (int i = 0; i < _runwayButtons.Count && i < _runwayLabels.Count; i++)
        StyleRunway(_runwayButtons[i], _runwayLabels[i], false);
}
```

Remove the fallback loop that re-infers `_selectedRunwayIndex` from a course within one degree. Selection styling must represent an actual runway choice, not an accidental numeric match.

- [ ] **Step 3: Populate directional thresholds**

Replace `AddRunwayDirection`'s object construction with:

```csharp
Vector3 threshold = reverse ? runway.End.position : runway.Start.position;
Vector3 departureEnd = reverse ? runway.Start.position : runway.End.position;
result.Add(new RunwayInfo
{
    Label = label,
    Heading = heading,
    LengthMeters = runway.Length,
    ThresholdPosition = threshold,
    DepartureEndPosition = departureEnd
});
```

Verify in-game that RWY 18's threshold is the approach-side end for heading 184 degrees and RWY 36 uses the opposite end. If the game's `GetDirection(reverse)` semantics are opposite, swap only the two assignments; do not compensate later in guidance math.

- [ ] **Step 4: Add explicit controller selection lifecycle**

Add fields:

```csharp
private int _selectedRunwayIndex = -1;
private RunwayGuidancePhase _runwayPhase = RunwayGuidancePhase.None;
```

Replace the panel subscriptions with named methods:

```csharp
_panel.AirportSelected += SelectAirport;
_panel.ModeChanged += SetCourseMode;
_panel.RunwaySelected += SelectRunway;
```

Add:

```csharp
private void SelectAirport(int index)
{
    _selectedIndex = index;
    ClearRunwaySelection();
}

private void SetCourseMode(CourseMode mode)
{
    _mode = mode;
    if (mode != CourseMode.Runway) ClearRunwaySelection();
}

private void SelectRunway(int index, RunwayInfo runway)
{
    _selectedRunwayIndex = index;
    _manualCourse = Mathf.Repeat(runway.Heading, 360f);
    _mode = CourseMode.Runway;
    _runwayPhase = RunwayGuidancePhase.Intercept;
}

private void ClearRunwaySelection()
{
    _selectedRunwayIndex = -1;
    _runwayPhase = RunwayGuidancePhase.None;
    _panel?.ClearRunwaySelection();
}
```

Keep `AdjustCourse` runway-aware:

```csharp
private void AdjustCourse(float delta)
{
    _manualCourse = Mathf.Repeat(_manualCourse + delta, 360f);
    if (_mode != CourseMode.Runway) _mode = CourseMode.Manual;
}
```

Generic setters clear runway identity before entering manual mode:

```csharp
private void SetManualCourse(float value)
{
    ClearRunwaySelection();
    _manualCourse = Mathf.Repeat(value, 360f);
    _mode = CourseMode.Manual;
}
```

Call `ClearRunwaySelection()` when cycling airports and when refresh invalidates the selected runway index.

- [ ] **Step 5: Compile the model and event changes**

Run:

```powershell
dotnet build NOVor.csproj -c Release
```

Expected: 0 errors. Presentation comparisons for `CourseMode.Manual` may still require Task 4, but no event-signature or model errors remain.

- [ ] **Step 6: Commit runway identity**

```powershell
git add Core\NavModels.cs Core\CdiData.cs Core\NavController.cs UI\NavPanel.cs
git commit -m "feat: preserve selected runway geometry"
```

### Task 3: Drive navigation from the actual inbound runway centerline

**Files:**

- Modify: `Core/NavController.cs:312-375`
- Modify: `tests/NavMathHarness/Program.cs`

- [ ] **Step 1: Add a pure along-track helper and checks**

Add to `NavMath`:

```csharp
public static double AlongTrackToThresholdMeters(double course, double aircraftEastOfThreshold,
    double aircraftNorthOfThreshold)
{
    double radians = course * Math.PI / 180d;
    double inboundEast = Math.Sin(radians);
    double inboundNorth = Math.Cos(radians);
    return -(aircraftEastOfThreshold * inboundEast + aircraftNorthOfThreshold * inboundNorth);
}
```

Add harness checks:

```csharp
Equal(1852d, NavMath.AlongTrackToThresholdMeters(0d, 0d, -1852d),
    "aircraft one mile before northbound threshold");
Equal(-1852d, NavMath.AlongTrackToThresholdMeters(0d, 0d, 1852d),
    "aircraft one mile past northbound threshold");
```

- [ ] **Step 2: Resolve the selected runway on every update**

Add:

```csharp
private bool TryGetSelectedRunway(Airbase airbase, out RunwayInfo runway)
{
    runway = default;
    if (_mode != CourseMode.Runway || _selectedRunwayIndex < 0) return false;
    RunwayInfo[] runways = GetRunways(airbase);
    if (runways == null || _selectedRunwayIndex >= runways.Length) return false;
    runway = runways[_selectedRunwayIndex];
    return true;
}
```

- [ ] **Step 3: Replace runway-mode geometry and command calculation**

In `UpdateData`, preserve existing direct/manual behavior, then branch for runway mode:

```csharp
bool hasRunway = TryGetSelectedRunway(target, out RunwayInfo runway);
Vector3 navTarget = hasRunway ? runway.ThresholdPosition : target.center.position;
Vector3 to = navTarget - pos;
Vector3 horizontal = new Vector3(to.x, 0f, to.z);
float bearing = Mathf.Repeat(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 360f);

Data.HasRunway = hasRunway;
Data.RunwayLabel = hasRunway ? runway.Label : null;
Data.Mode = hasRunway ? CourseMode.Runway : _mode;
Data.Course = Data.Mode == CourseMode.Auto ? bearing : _manualCourse;
Data.DistanceNm = horizontal.magnitude / 1852f;
Data.ToStation = NavMath.IsToStation(Data.Course, bearing);

float eastOfReference = -horizontal.x;
float northOfReference = -horizontal.z;
Data.CrossTrackNm = (float)(NavMath.CrossTrackMeters(Data.Course,
    eastOfReference, northOfReference) / 1852d);
Data.AlongTrackToThresholdNm = hasRunway
    ? (float)(NavMath.AlongTrackToThresholdMeters(Data.Course,
        eastOfReference, northOfReference) / 1852d)
    : 0f;
```

After CDI evaluation, choose the command source:

```csharp
if (hasRunway)
{
    RunwayGuidanceOutput runwayGuidance = RunwayGuidance.Evaluate(new RunwayGuidanceInput
    {
        CourseDegrees = Data.Course,
        CrossTrackNm = Data.CrossTrackNm,
        AlongTrackToThresholdNm = Data.AlongTrackToThresholdNm,
        GroundSpeedKnots = horizontalSpeed * 1.9438445f,
        HeadingDegrees = Data.Heading,
        GroundTrackDegrees = Data.GroundTrack,
        MaxInterceptDegrees = Plugin.MaxInterceptDegrees.Value,
        WasEstablished = _runwayPhase == RunwayGuidancePhase.Established
    });
    _runwayPhase = runwayGuidance.Phase;
    Data.RunwayPhase = runwayGuidance.Phase;
    Data.CommandHeading = (float)runwayGuidance.CommandHeadingDegrees;
}
else
{
    Data.RunwayPhase = RunwayGuidancePhase.None;
    Data.CommandHeading = (float)GuidanceMath.CommandHeadingDegrees(
        _mode == CourseMode.Manual, Data.Course, Data.Bearing, Data.CrossTrackNm,
        Plugin.MaxInterceptDegrees.Value, Data.Heading, Data.GroundTrack);
}
```

Keep `Data.Deflection` enabled for both manual and runway modes:

```csharp
Data.Deflection = Data.Mode == CourseMode.Auto ? 0f : (float)deviation.Deflection;
```

- [ ] **Step 4: Run harness and builds**

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
```

Expected: all harness checks pass and Release reports 0 errors.

- [ ] **Step 5: Commit runtime guidance integration**

```powershell
git add Core\NavMath.cs Core\NavController.cs tests\NavMathHarness\Program.cs
git commit -m "feat: guide to runway threshold centerline"
```

### Task 4: Make runway and diamond semantics explicit in UI

**Files:**

- Modify: `Core/CockpitPresentation.cs`
- Modify: `UI/CdiInstrument.cs`
- Modify: `UI/NavPanel.cs:440-450, 703-731`
- Modify: `UI/PanelHsi.cs:60-81`
- Modify: `tests/NavMathHarness/Program.cs`

- [ ] **Step 1: Extend the cockpit presentation input**

Add:

```csharp
public string RunwayLabel;
public RunwayGuidancePhase RunwayPhase;
public bool HasRunway;
```

Build the target, context, and command strings with runway identity:

```csharp
string targetName = CompactFieldName(input.AirportName);
if (input.HasRunway && !string.IsNullOrEmpty(input.RunwayLabel))
    targetName += "  " + input.RunwayLabel.ToUpperInvariant();

string context = input.HasRunway
    ? "CRS " + Degrees(input.Course) + "Â°  Â·  RWY"
    : input.Manual
        ? "CRS " + Degrees(input.Course) + "Â°  Â·  " +
            NavigationPresentation.ToFromLabel(input.ToStation)
        : "BRG " + Degrees(input.Bearing) + "Â°  Â·  DIRECT";

string verb = input.HasRunway
    ? input.RunwayPhase == RunwayGuidancePhase.Established ? "TRACK" : "INTCP"
    : input.Manual ? input.OffScale ? "INTCP" : "TRACK" : "STEER";
string command = verb + "  â—†  " + Degrees(input.CommandHeading) + "Â°";
```

Use `targetName` in `TargetLine`. Set `CommandAttention` when runway guidance is in `Intercept` or `Capture`, as well as for generic manual off-scale guidance.

- [ ] **Step 2: Add cockpit-copy checks**

Add:

```csharp
var runwayReadout = CockpitPresentation.Build(new CockpitPresentationInput
{
    AirportName = "Sandrift Airbase",
    RunwayLabel = "RWY 18",
    DistanceNm = 3f,
    Course = 184f,
    CommandHeading = 176f,
    GroundSpeedKnots = 300f,
    Manual = true,
    HasRunway = true,
    RunwayPhase = RunwayGuidancePhase.Capture,
    ScaleMode = CdiScaleMode.Angular,
    Units = NavigationDisplayUnits.Aviation
});
Equal("SANDRIFT  RWY 18  Â·  3.0 NM", runwayReadout.TargetLine,
    "cockpit confirms selected runway");
Equal("CRS 184Â°  Â·  RWY", runwayReadout.ContextLine, "runway course context");
Equal("INTCP  â—†  176Â°", runwayReadout.CommandLine, "diamond is explicit fly-to command");
True(runwayReadout.CommandAttention, "runway capture command receives attention");
```

- [ ] **Step 3: Feed runway fields into the cockpit block**

Add to `CockpitPresentationInput` construction in `CdiInstrument.SetData`:

```csharp
RunwayLabel = data.RunwayLabel,
RunwayPhase = data.RunwayPhase,
HasRunway = data.HasRunway,
Manual = data.Mode != CourseMode.Auto,
```

- [ ] **Step 4: Treat runway mode as manual-course presentation**

In `PanelHsi.SetData`, use:

```csharp
bool manual = data.Mode != CourseMode.Auto;
_deviationBar.gameObject.SetActive(manual);
_courseReadout.text = data.Mode == CourseMode.Runway
    ? $"CRS {Mathf.RoundToInt(data.Course):000}Â° RWY"
    : manual
        ? $"CRS {Mathf.RoundToInt(data.Course):000}Â° {ScaleTag(data.ScaleMode)}"
        : $"BRG {Mathf.RoundToInt(data.Bearing):000}Â°";
_toFromFlag.text = data.Mode == CourseMode.Runway
    ? data.RunwayPhase == RunwayGuidancePhase.Established ? "EST" : "RWY"
    : manual
        ? NavigationPresentation.ToFromLabel(data.ToStation)
        : "DIRECT";
```

In `NavPanel`, keep the `MANUAL` control active for both manual and runway modes:

```csharp
StyleToggle(_manualButton, _manualLabel, data.Mode != CourseMode.Auto);
```

Keep `_selectedRunwayIndex` as the source of runway-button styling rather than re-inferring selection from a one-degree course match.

- [ ] **Step 5: Run the automated checks and builds**

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
dotnet build NOVor.csproj -c Debug
```

Expected: all checks pass; both builds report 0 errors; Debug deploys only to `BepInEx\scripts`.

- [ ] **Step 6: Commit runway-aware presentation**

```powershell
git add Core\CockpitPresentation.cs UI\CdiInstrument.cs UI\NavPanel.cs UI\PanelHsi.cs tests\NavMathHarness\Program.cs
git commit -m "polish: identify runway and command diamond"
```

### Task 5: Verify the complete intercept workflow in game

**Files:**

- Modify: `AGENTS.md`

- [ ] **Step 1: Record the runway guidance contract**

Add under Game API Patterns:

```markdown
- Selecting a runway preserves its label and directional threshold and enters `CourseMode.Runway`; course nudges rotate that runway's inbound approach line about the threshold without discarding runway identity.
- Runway guidance targets the inbound extended centerline and plans capture by the fixed 3 NM final gate. Intercept angle uses remaining along-track distance; rollout distance scales with groundspeed; established-state hysteresis prevents cue chatter.
- On the heading tape, the outlined green triangle is selected runway course and the amber diamond is the drift-corrected aircraft heading to fly. Align the native current-heading marker with the amber diamond. Wind may keep the two navigation cues separated while established.
- Runway capture is lateral-only. No glidepath or aircraft-control automation is provided.
```

- [ ] **Step 2: Run the automated verification set**

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
dotnet build NOVor.csproj -c Debug
```

Expected: every harness check passes, both builds report 0 errors, and Debug deploys to ScriptEngine.

- [ ] **Step 3: Verify runway identity and selection lifecycle**

In game:

1. Select Sandrift RWY 18 and confirm the panel button remains selected.
2. Confirm the cockpit block contains both `SANDRIFT` and `RWY 18`.
3. Nudge course from 184 to 186 degrees and confirm RWY 18 remains identified and selected.
4. Use `SET BRG`; confirm runway identity clears and the state becomes generic manual course.
5. Select another airport; confirm the old runway identity cannot leak into the new field.

- [ ] **Step 4: Verify the lateral capture matrix with the panel closed**

Fly each case while following the amber diamond with the native current-heading marker:

1. 10-15 NM out, 2-4 NM lateral offset, 250-350 KT: command points toward a capture at or before 3 NM.
2. 5 NM out, 1 NM lateral offset: command uses an assertive recovery intercept without reversing away from the runway.
3. Near capture: amber diamond rolls toward course before crossing the centerline and does not jump to the opposite side.
4. Established at 3 NM: cross-track is at most 0.05 NM and ground-track error at most 5 degrees.
5. Mild crosswind: green course and amber command remain distinct; aligning aircraft heading with amber holds the green course.
6. Threshold crossing: guidance does not reverse, produce `FROM`, or command a turn back toward the airport.
7. Heading wrap near 359/001 degrees: both tape cues take the short path.

Capture three screenshots or a short clip: initial intercept, rollout near centerline, and established inbound at 3 NM.

- [ ] **Step 5: Verify hot reload and cleanup**

With runway guidance active, rebuild Debug and trigger ScriptEngine reload. Confirm the old panel/HUD/tape objects disappear, the cursor/camera state restores, and the reloaded plugin starts without retaining a stale runway index or guidance phase.

- [ ] **Step 6: Commit documentation and any verification-only corrections**

```powershell
git add AGENTS.md
git commit -m "docs: define runway capture guidance"
```

## Self-review result

- The plan covers the chosen runway, actual threshold geometry, inbound-only lateral guidance, 3 NM establishment goal, speed-aware rollout, persistent runway confirmation, and unambiguous diamond semantics.
- Vertical guidance, automation, mobile-field specialization, and unrelated panel redesign are explicitly excluded.
- Pure geometry and policy behavior are harnessed; Unity-only runway endpoint mapping and tape behavior have explicit in-game checks.
- The existing dirty working tree must be committed or otherwise preserved before execution. The plan must not overwrite unrelated local changes.
