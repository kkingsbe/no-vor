# HUD Navigation Cues Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give AUTO navigation a clear bearing/steering director and make MANUAL navigation a geometrically correct, distance-scaled CDI.

**Architecture:** `NavMath` will own signed cross-track and steering-error calculations, while `NavController` will populate distinct fields instead of overloading one deviation value. The cockpit HUD will switch between an AUTO azimuth/director strip and a MANUAL CDI, and the panel HSI will only show its deviation bar in MANUAL mode.

**Tech Stack:** C# (`net472` plugin and `net8.0` math harness), Unity 2022 uGUI, BepInEx 5.

---

### Task 1: Define and test unambiguous navigation math

**Files:**
- Modify: `Core/NavMath.cs`
- Modify: `tests/NavMathHarness/Program.cs`

- [x] **Step 1: Add failing behavioral tests**

Add cases proving that a course east/right of the aircraft produces positive needle displacement, a course west/left produces negative displacement, wraparound steering has the correct sign, and cross-track deflection clamps at full scale.

- [x] **Step 2: Run the harness and confirm the new API is missing**

Run: `dotnet run --project tests\NavMathHarness -c Release`

Expected: compilation fails because `CrossTrackMeters`, `CrossTrackDeflection`, or `SteeringErrorDegrees` does not exist.

- [x] **Step 3: Implement dependency-free geometry**

Compute the aircraft's signed position along the selected course's right-hand normal and negate it so a positive result always means “desired course is right.” Normalize by a positive full-scale distance and clamp to `[-1, 1]`. Define steering error as the shortest signed angle from aircraft heading to steering heading, positive right.

- [x] **Step 4: Run the harness**

Run: `dotnet run --project tests\NavMathHarness -c Release`

Expected: every angle, cross-track, steering, and ETA assertion passes.

### Task 2: Split the navigation data contract

**Files:**
- Modify: `Core/CdiData.cs`
- Modify: `Core/NavController.cs`
- Modify: `Plugin.cs`

- [x] **Step 1: Replace overloaded deviation data**

Replace `Deviation` with `CrossTrackNm` and `SteeringError`; retain `Deflection` exclusively for the manual CDI.

- [x] **Step 2: Populate both navigation solutions**

Use target-relative east/north displacement and the selected course to calculate signed cross-track distance. Calculate steering error independently from heading and drift-corrected steering heading. In AUTO, keep the direct-to course equal to bearing but leave CDI deflection centered.

- [x] **Step 3: Replace angular full-scale configuration**

Bind `Navigation.FullDeflectionNauticalMiles`, default `1.0`, constrained to `0.1–10.0 NM`. Use it only for manual CDI normalization.

### Task 3: Present separate AUTO and MANUAL HUD symbology

**Files:**
- Modify: `UI/CdiInstrument.cs`
- Modify: `UI/PanelHsi.cs`

- [x] **Step 1: Build two cockpit cue groups**

Keep the existing CDI scale for MANUAL mode. Add an AUTO azimuth strip with a bearing caret and amber steering-command diamond, positioning each from its signed angle relative to heading and clamping/off-scale-marking it at the strip edge.

- [x] **Step 2: Make text mode-specific**

AUTO reports `BRG`, distance, steering heading, and signed steering error. MANUAL reports `CRS`, TO/FROM, signed cross-track NM, and steering heading. Do not label steering error as CDI deviation.

- [x] **Step 3: Correct manual CDI movement**

Map positive `Deflection` directly to screen-right and light the matching edge arrow. The needle direction must always mean “desired course is this way.”

- [x] **Step 4: Simplify the panel HSI in AUTO**

Hide the HSI deviation bar outside MANUAL mode so bearing/course geometry is not mixed with heading-command error.

### Task 4: Verify plugin integration and documentation

**Files:**
- Modify: `AGENTS.md`

- [x] **Step 1: Document navigation semantics**

Record that AUTO uses bearing and steering director cues, MANUAL uses cross-track CDI displacement, and the CDI scale is nautical-mile based.

- [x] **Step 2: Run all verification commands**

Run:

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
dotnet build NOVor.csproj -c Debug
```

Expected: the harness reports all checks passing; both plugin builds complete with zero errors; Debug deploys to `BepInEx\scripts` when `NuclearOptionRoot` is configured.
