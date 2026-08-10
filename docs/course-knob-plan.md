# Course Input: Skeuomorphic OBS Knob

Replace the current course input (big text field + `-5 / -1 / +1 / +5` button row)
with a skeuomorphic rotary knob, styled like an aviation OBS (Omni Bearing
Selector) knob, matching the panel's green-phosphor cockpit aesthetic.

## Current state

- All UI is procedural uGUI — no prefabs, no image assets.
- `UI/NavPanel.cs` → `BuildCourseDeck` (line ~278) builds:
  - AUTO / MANUAL segmented row
  - "COURSE" caption + big `TMP_InputField` (click-to-type, scroll-wheel ±1)
  - `-5 / -1 / +1 / +5` button row  ← **removed by this change**
  - SET BRG / SET HDG / TO/FR row + target label
- Textures are generated in code (`UI/TextureFactory.cs`).
- Panel events: `CourseAdjusted(float delta)`, `CourseSet(float absolute)`.
- `Core/NavController.cs` consumes them; `AdjustCourse` wraps 0–360 and
  switches to manual mode — exactly the semantics a knob needs.
- **No changes required to `NavController` or the event surface.**

## Design decisions

- **Twist-to-turn (circular drag)**, not vertical drag — that's the
  skeuomorphic fantasy and handles 0/360 wraparound naturally.
- **Tick marks only on the bezel** (every 10°, major every 30°, tiny N/E/S/W
  cardinals). Numeric labels would be illegible at ~130 px; a digital readout
  covers exact value display.
- **Keep click-to-type** via a compact readout under the knob ("299°") so exact
  course entry is still possible.
- Knob reflects auto-mode course updates (spins to follow bearing-to-station);
  dragging it flips to manual mode via existing `AdjustCourse` behavior.

## Implementation

### 1. `UI/TextureFactory.cs` — procedural knob art (cached sprites, ~256×256)

- `CreateKnobBezel(int size)` — static outer ring:
  - dark metallic radial shading, inset inner shadow
  - tick marks every 10° (longer/brighter every 30°)
  - small N / E / S / W cardinal marks
- `CreateKnobDial(int size)` — rotating knob body:
  - domed radial gradient lit from top-left
  - knurled rim
  - HudGreen indicator notch at 12 o'clock
  - soft drop shadow baked in

### 2. New `UI/CourseKnob.cs` — interaction component

- Implements drag handling via `EventTrigger` (BeginDrag / Drag) or
  `IBeginDragHandler` / `IDragHandler`, plus scroll.
- **Twist math:** pointer position → local point via
  `RectTransformUtility.ScreenPointToLocalPointInRectangle` → angle about
  center; per-frame `Mathf.DeltaAngle(prev, current)` → fire
  `CourseAdjusted(delta)`. Ignore samples too close to the center (angle noise).
- **Scroll wheel** over the knob: ±1° (preserves existing fine-trim behavior).
- `SetCourse(float course)` sets dial `localEulerAngles.z = -course` without
  firing events (no feedback loop; auto-mode updates spin the knob).

### 3. `UI/NavPanel.cs` — `BuildCourseDeck` rework

- Keep: AUTO/MANUAL row, COURSE caption, SET BRG / SET HDG / TO/FR row,
  target label.
- Remove: the four ± buttons row; demote the big input field.
- Add: knob row — bezel `Image` (static) with dial `Image` as a rotating
  child, ~130 px diameter, `CourseKnob` component attached.
- Add: compact readout ("299°") under the knob — still a `TMP_InputField`
  (IntegerNumber, 3-char limit) for click-to-type exact entry.
- `SetCourse(...)` updates both knob rotation and readout text; guard against
  overwriting the readout while it is focused (existing `isFocused` check).

### 4. Cleanup

- Remove the now-unused `OnCourseScroll` EventTrigger wiring on the old input
  (scroll handling moves to the knob).
- Keep `OnCourseTyped` for the compact readout.

## Files touched

| File | Change |
|---|---|
| `UI/TextureFactory.cs` | Add bezel + dial sprite builders |
| `UI/CourseKnob.cs` | **New** — drag/scroll/rotation component |
| `UI/NavPanel.cs` | Rework `BuildCourseDeck`, wire knob, trim scroll wiring |

`Core/NavController.cs`, `Plugin.cs` — untouched.

## Verification

1. `dotnet build` — clean compile.
2. `deploy.ps1` → in-game check:
   - Twist drag: course follows pointer angle, wraps 359° ↔ 0° smoothly.
   - Scroll over knob: ±1° steps.
   - Readout: click, type exact course, Enter applies.
   - AUTO mode: knob spins to follow bearing; dragging flips to MANUAL.
   - `[` / `]` hotkeys still rotate the knob.
   - TO/FR flip rotates knob 180°.
