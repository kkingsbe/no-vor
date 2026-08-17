---
date: 2026-08-14 20:12:21 EDT
researcher: Codex
git_commit: 5b21d161bb995f53a72acc0c97f1844e36c1be8f
branch: master
repository: no-vor
topic: "Current product surface and owner discovery for the next no-vor phase"
tags: [research, codebase, navigation, cockpit-hud, ui, roadmap]
status: complete
last_updated: 2026-08-14
last_updated_by: Codex
last_updated_note: "Added owner answers, screenshot evidence, and intercept-guidance follow-up research"
---

# Research

## Research Question

What user-visible capabilities, interaction surfaces, and implementation seams does `no-vor` currently have, so an owner interview and screenshot review can expose the highest-value next phase?

## Summary

The live working tree describes a substantially broader product than a simple VOR needle. It combines airport and diversion discovery, direct-to guidance, manual course interception, a heading-up panel HSI, runway selection, mobile-field telemetry, a compact cockpit block, native heading-tape cues, and in-panel keyboard/HOTAS rebinding. The newest guidance, presentation, unit, and input work is uncommitted, so the working tree—not `HEAD`—is the current source of truth.

The next discovery step should establish the product's intended center of gravity and evaluate the live in-game result. Existing plans already cover extensive cockpit and panel polish; screenshots are needed to distinguish implemented behavior from accepted behavior.

## Detailed Findings

### Product and navigation model

- `NavController` polls the local aircraft and airbases, builds the current navigation state, and coordinates the panel and cockpit surfaces (`Core/NavController.cs:14`, `Core/NavController.cs:59`, `Core/NavController.cs:118`).
- Direct mode guides to the selected field bearing. Manual mode tracks a selected course using cross-track deviation and a drift-corrected, capped intercept command (`Core/NavController.cs:336`, `Core/GuidanceMath.cs:5`, `Core/CdiScale.cs:34`).
- Airport models include range, bearing, ETA, ownership, mobility, elevation, and runway directions; mobile-field ETA accounts for target velocity (`Core/NavModels.cs:17`, `Core/NavController.cs:187`, `Core/NavController.cs:220`).
- Dependency-free navigation, guidance, presentation, and input policy code is linked into a .NET 8 harness with 65 checks (`tests/NavMathHarness/NavMathHarness.csproj:3`, `tests/NavMathHarness/Program.cs:10`).

### User-visible surfaces

- The 820x430 navigation panel provides search, nearest/name sorting, friendly filtering, pinned selection, airport facts, a heading-up HSI, direct/manual modes, course actions, up to six runway directions, and live steer/groundspeed/ETA/elevation telemetry (`UI/NavPanel.cs:15`, `UI/NavPanel.cs:215`, `UI/NavPanel.cs:370`, `UI/NavPanel.cs:652`, `UI/NavPanel.cs:754`).
- The cockpit HUD is owned by one component and combines a compact navigation block with a green course/bearing triangle and amber command diamond on the native heading tape (`UI/CockpitHud.cs:12`, `UI/HudCueIcon.cs:14`, `UI/HeadingTapeCues.cs:29`).
- Direct mode collapses the block and hides CDI deviation. Manual mode adds the CDI rail, TO/FROM course context, and an off-scale intercept state (`Core/CockpitPresentation.cs:36`, `UI/CdiInstrument.cs:70`).
- The CFG view exposes eleven keyboard/HOTAS bindings with capture, clear, and cancel states; analog axes and POV hats are outside the current model (`UI/KeyBindingPanel.cs:51`, `UI/KeyBindingPanel.cs:77`, `Core/InputBindingPolicy.cs:27`).
- A currently observable presentation mismatch remains: metric display formatting is used in panel header/runway/telemetry, while airport-list distance and its header remain nautical miles (`UI/NavPanel.cs:271`, `UI/NavPanel.cs:607`, `UI/NavPanel.cs:639`, `UI/NavPanel.cs:692`, `UI/NavPanel.cs:768`).

### Architecture and operational seams

- The plugin uses polling/runtime discovery rather than Harmony. `NavController` is the game-assembly boundary; plain models carry data into UI (`Core/NavController.cs:41`, `Core/NavModels.cs:24`).
- The panel emits ordinary C# events, while `NavController` owns navigation state (`UI/NavPanel.cs:91`, `Core/NavController.cs:46`).
- Native compass access and camera/zoom suppression use reflection; panel close/teardown restores cursor and camera state (`Core/NavController.cs:396`, `Core/NavController.cs:407`, `Core/NavController.cs:474`, `UI/NavPanel.cs:913`).
- Debug builds deploy only to ScriptEngine's scripts folder for hot reload; Release creates a flat zip, and CI runs the harness/build/release flow (`NOVor.csproj:86`, `NOVor.csproj:93`, `.github/workflows/release-build.yml:92`).

## Historical Context

- The project evolved from an airport/course panel into a landscape navigation page with HSI, ownership, mobile fields, runways, and diversion telemetry (`docs/superpowers/plans/2026-08-09-nav-airport-course-ui.md:5`, `docs/superpowers/plans/2026-08-10-native-nav-panel-hsi-redesign.md:5`).
- Subsequent phases separated direct and manual semantics, consolidated cockpit ownership, introduced automatic CDI scaling/intercept guidance, polished the panel/cockpit presentation, unified the active command across surfaces, and added HOTAS rebinding (`docs/superpowers/plans/2026-08-11-hud-navigation-cues.md:5`, `docs/superpowers/plans/2026-08-12-cockpit-hud-consolidation.md:5`, `docs/superpowers/plans/2026-08-14-cockpit-guidance-coherence.md:5`, `docs/superpowers/plans/2026-08-14-hotas-keybindings-config-panel.md:5`).
- Plan checkbox state is not reliable completion evidence: several implemented phases retain unchecked boxes. The live files and in-game review should decide acceptance.
- `master` is seven commits ahead of `origin/master`, with 13 modified tracked files and 11 untracked files at research time. No files were staged.

## Screenshot Review Set

1. Normal expanded panel: selected friendly fixed field, multiple runway buttons, populated airport list.
2. Mobile field/carrier selected: mobility state, runway choices, and live telemetry visible.
3. Cockpit direct-to: compact block plus separated green course and amber command tape cues.
4. Cockpit manual/on-scale: CDI needle visible and tracking toward the selected course.
5. Cockpit manual/off-scale: suppressed needle, one edge flag, amber `INTCP`, and matching tape command.
6. Heading-tape edge case: course and command clamped on opposite edges or the same edge.
7. CFG/Controls: normal bindings plus one active HOTAS capture or cleared binding.
8. The most crowded, confusing, or visually weak state at the owner's normal resolution and UI scale.

## Open Questions

- Is the product primarily an authentic VOR/CDI instrument, a tactical navigation aid, or an airport/diversion browser?
- Which surface should carry the in-flight experience: cockpit block, heading-tape cues, panel HSI, or airport list?
- Which flight phase should the next phase transform: field discovery, enroute intercept, terminal/runway alignment, diversion decisions, or cockpit readability?
- Should runway guidance and mobile fields be central workflows or supporting compatibility?
- Is manual mode intended to reward realistic course interception or provide accessible command-heading guidance?
- Is the current telemetry density appropriate under flight workload?
- Are metric parity, HOTAS rebinding, NoModBar integration, and public release readiness core product requirements or supporting infrastructure?

## Related Research

- No earlier `research/` artifacts existed at research time.
- Origin: `https://github.com/kkingsbe/no-vor.git`. Local references are used because the current product state includes uncommitted files that cannot be represented by commit permalinks.

## Follow-up Research 2026-08-14 20:31:33 EDT

### Owner intent

- The intended job is navigation to a chosen airport while arriving aligned with a chosen runway; vanilla runway/glidepath visualization appears too late because it waits for gear deployment.
- The heading-tape cues are the primary in-flight surface. The airport list is the selection surface, and the cockpit block is used to confirm the chosen runway.
- Course interception is the desired focus for the next phase. Mobile fields are low priority, and existing behavior is open to change.

### Screenshot evidence

- The screenshots show Sandrift selected at 21.7 NM, manual course 186 degrees, an intercept command of 141 degrees, and the same course/command propagated through panel, cockpit block, and tape.
- The tape separates course context (outlined green triangle) from commanded heading (amber diamond), although both cues are small relative to the native tape and native heading marker.
- The cockpit block strongly emphasizes `INTCP 141` and off-scale direction, but it names the airport and course rather than the selected runway. This does not directly satisfy the owner's stated runway-confirmation use.
- The panel offers runway 36 at 004 degrees and runway 18 at 184 degrees, while the active course is 186 degrees. Neither runway button remains selected because current presentation clears runway highlighting beyond one degree (`UI/NavPanel.cs:703`).
- All captures are stationary or near-stationary. They validate state propagation and visual hierarchy, but do not establish how guidance behaves during capture, rollout, overshoot, or wind correction.

### Current intercept behavior

- Selecting a runway retains only its heading in navigation state; runway identity and threshold/endpoints are not retained by `NavController` (`UI/NavPanel.cs:739`, `Core/NavController.cs:54`).
- Lateral guidance uses an infinite course line through the airbase center, not an inbound approach leg through an actual runway threshold (`Core/NavController.cs:320`, `Core/NavController.cs:344`, `Core/NavMath.cs:24`).
- At one NM or more cross-track, the command uses the configured maximum intercept angle (45 degrees by default). Inside one NM, that angle shrinks linearly to zero (`Core/CdiScale.cs:34`, `Plugin.cs:85`).
- The schedule does not consider distance remaining, along-track position, speed, closure, current convergence, turn performance, runway length, or capture history.
- Rollout is stateless. There is no capture state, rollout lead, hysteresis, damping, or overshoot history (`Core/CdiScale.cs:34`, `Core/GuidanceMath.cs:5`).
- Observed drift is subtracted from the desired track so the tape diamond represents a commanded aircraft heading (`Core/GuidanceMath.cs:8`, `Core/NavMath.cs:45`).
- Vertical guidance is not present in the live model (`Core/CdiData.cs:3`).

### Remaining discriminating questions

- Should runway navigation retain a specific runway and its actual endpoints, or remain a generic numeric course?
- Should the path be an infinite bidirectional line or an inbound runway approach extension with a defined capture point?
- What observable behavior is currently unsatisfactory during a real intercept: slow capture, overshoot, oscillation, abrupt cue motion, incorrect side, or unclear presentation?
- Should capture and rollout anticipate speed/turn rate, or remain a simple cross-track-proportional command?
- When established inbound, should the command diamond merge with course or remain separated to show wind correction?
- Is the next phase strictly lateral, or should early vertical/glidepath guidance enter scope?

### Owner decision and selected phase

- Runway identity should remain explicit; the screenshot's course discrepancy was not caused by an intentional manual nudge.
- Use an inbound runway approach extension through the actual threshold rather than the current bidirectional line through the airbase center.
- The aircraft should be established by 3 NM from the threshold.
- The green course cue and amber command diamond remain separate. The pilot flies the amber diamond by aligning the native current-heading marker with it; the green outlined triangle remains runway-course context and may be offset under wind correction.
- Scope is lateral only. Vertical/glidepath guidance remains a later phase.
- The resulting implementation plan is `docs/superpowers/plans/2026-08-14-runway-aware-course-capture.md`.
