# HUD CDI Legibility Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the cockpit navigation block into an immediately readable intercept display with persistent course/steer geometry, honest CDI limits, concise action-oriented text, and sky-proof glyphs.

**Architecture:** `CdiData` carries the selected full-scale distance so `CdiInstrument` can distinguish valid in-scale displacement from off-scale guidance. `HeadingTapeCues` attaches its markers directly to `FlightHud`'s private native `compass` RawImage and converts angular delta through that tape's live UV span and rect width. MANUAL mode adds a separately labeled linear CDI block, while AUTO mode retains a compact steering command. HUD placement remains configurable but migrates the former default above-center position to a below-center default once.

**Tech Stack:** C# (`net472` plugin), Unity 2022 legacy uGUI, BepInEx 5, dependency-free .NET 8 navigation harness.

---

### Task 1: Carry explicit CDI scale state

**Files:**
- Modify: `Core/CdiData.cs`
- Modify: `Core/NavController.cs`

- [x] **Step 1: Add the selected full-scale distance to live HUD data**

Add `FullScaleNm` to `CdiData` and populate it from `Plugin.FullDeflectionNm.Value` beside `CrossTrackNm` and `Deflection`.

- [x] **Step 2: Keep deflection math clamped but preserve raw cross-track magnitude**

Continue using `NavMath.CrossTrackDeflection` for needle placement. Use `Mathf.Abs(data.CrossTrackNm) >= data.FullScaleNm` in the HUD to select the off-scale presentation, so clamping never masquerades as valid needle data.

### Task 2: Rebuild the cockpit information hierarchy

**Files:**
- Modify: `UI/CdiInstrument.cs`
- Create: `UI/HeadingTapeCues.cs`
- Modify: `Core/NavController.cs`

- [x] **Step 1: Add course and steering markers to Nuclear Option's native heading tape**

Reflect `FlightHud.compass`, parent a green hollow-down course caret and an amber hollow diamond directly beneath its `RawImage` transform, and position them from `DeltaAngleDegrees` using `compass.uvRect.width * 360` as the native visible angular span. Use distinct directional edge glyphs when either cue is outside the native tape.

- [x] **Step 2: Make the MANUAL CDI honest and self-describing**

Render four evenly spaced deviation dots, a categorical center triangle, a `1NM`-style full-scale label sourced from `FullScaleNm`, and an integrated `▲ TO` / `▼ FR` flag. Keep the valid needle green; when cross-track is at or beyond full scale, hide it and show only a differently shaped amber edge chevron.

- [x] **Step 3: Replace the long sentence with compact text**

Limit the field/range line to a compact uppercase field identifier plus range. Make `XTK` or `CMD` the larger, brighter action line; format cross-track to one decimal below 10 NM and whole nautical miles above it. Remove list index, normal-mode annunciation, bearing, course, steer-heading, and TO/FROM prose made redundant by symbology.

- [x] **Step 4: Outline all HUD graphics**

Attach a black `UnityEngine.UI.Outline` to every generated `Text` and `Image`, using a one-pixel effect distance, so both text and procedural symbology remain legible over clouds.

### Task 3: Move the default block clear of the flight reference

**Files:**
- Modify: `Plugin.cs`

- [x] **Step 1: Change the default vertical position**

Set `Hud.OffsetY` to `-180`, placing the navigation block below HUD center rather than over the pitch ladder.

- [x] **Step 2: Migrate only the legacy default**

Bind `Hud.LayoutVersion` with default `1`. When it is below `2`, change `OffsetY` from exactly `180` to `-180` and persist layout version `2`; preserve every non-default user position.

### Task 4: Verify behavior and compatibility

**Files:**
- Modify: `AGENTS.md`

- [x] **Step 1: Document the cockpit symbology**

Record the dual course/steer tape, explicit nautical-mile CDI scale, off-scale needle suppression, outlined glyphs, and below-center default.

- [x] **Step 2: Run the math harness**

Run `dotnet run --project tests\NavMathHarness -c Release` and expect all 18 checks to pass.

- [ ] **Step 3: Build both configurations**

Run `dotnet build NOVor.csproj -c Release` and `dotnet build NOVor.csproj -c Debug`; expect zero errors in both builds and Debug deployment to the configured ScriptEngine directory.

Current verification is blocked by unrelated in-progress `UI/NavPanel.cs` changes that do not compile; the native heading-tape files produce no reported compiler errors.
