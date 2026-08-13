# Navigation Panel Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the navigation panel read as a compact, aligned avionics console with fixed-height controls, opaque instrument surfaces, restrained selection states, and non-duplicative navigation data.

**Architecture:** `NavPanel` remains the code-built uGUI owner, but its layout helpers will explicitly separate fixed-height tool rows from the one expanding body row. Procedural framed sprites will be removed from broad surfaces where they currently create gray/translucent artifacts; simple opaque colors and small state rails will carry hierarchy instead. `PanelHsi` will switch its top label by navigation mode.

**Tech Stack:** C# net472, Unity 2022 uGUI, TextMeshPro, BepInEx 5.

---

### Task 1: Lock toolbar geometry and bottom readout space

**Files:**
- Modify: `UI/NavPanel.cs`

- [x] **Step 1: Give horizontal rows explicit flexibility semantics**

Change `MakeHorizontal` to accept `bool expandChildrenHeight = false`, set its `LayoutElement.minHeight` and `preferredHeight` to the requested height, and set `flexibleHeight = 0f`. Assign `layout.childForceExpandHeight = expandChildrenHeight`.

- [x] **Step 2: Allow only the main body to fill vertically**

Call `MakeHorizontal(parent, "Body", PanelHeight - HeaderHeight - 22f, 8f, true)` for the two main panes. Leave header, search, mode, and action rows on the fixed-height default.

- [x] **Step 3: Prevent telemetry clipping**

Keep the HSI slot as the only flexible-height item in the navigation pane. Give `RunwaySection` and `Readout` explicit non-flexible layout elements so `STEER`, `GS`, `ETA`, and `ELEV` retain their 42 px readout allocation.

### Task 2: Remove transparency and sliced-surface artifacts

**Files:**
- Modify: `UI/NavPanel.cs`
- Modify: `UI/UiColors.cs`

- [x] **Step 1: Make panel chrome opaque**

Set `Chrome`, `ChromeRaised`, `InstrumentWell`, and `RowSurface` to alpha `1f` and use a subtle opaque selected surface rather than the existing brown `SelectionFill`.

- [x] **Step 2: Use direct image colors for large surfaces**

Remove framed sprites from the panel root, HSI slot, search input, and action buttons. Assign `UiColors.Chrome`, `UiColors.InstrumentWell`, or `UiColors.ChromeRaised` directly so no game scene or stretched border color appears between panes.

- [x] **Step 3: Make the body a continuous aligned surface**

Set the body image to `UiColors.Chrome`, retain the two dark instrument wells, and keep the divider as the only full-height separator.

### Task 3: Restrain state styling and repair badges

**Files:**
- Modify: `UI/NavPanel.cs`

- [x] **Step 1: Replace filled active tabs with a state rail**

Keep every toggle background `UiColors.ChromeRaised`. Add or update a two-pixel `ActiveRail` child at the bottom of each toggle; show it in `UiColors.Amber` only when active and use amber text for the active label.

- [x] **Step 2: Reduce selected-row acreage**

Keep the existing amber three-pixel selection rail, set selected row fill to the subtle selected surface, and render the selected field name in `PanelText` rather than amber.

- [x] **Step 3: Make status badges readable**

Use a solid accent fill for `MOV`, `PINNED`, and `HERE`, with `UiColors.OnAmber` dark text and fixed 15 px height. This guarantees contrast even at small sizes.

### Task 4: Remove duplicated navigation copy

**Files:**
- Modify: `UI/NavPanel.cs`
- Modify: `UI/PanelHsi.cs`

- [x] **Step 1: Compact the header readout**

Uppercase the selected name, remove the word `CLASS`, and separate name, bearing, and range with centered dots: `ARGUS FRIGATE · 001° · 30.1 NM`.

- [x] **Step 2: Make the HSI label mode-correct**

Render `BRG nnn°` in AUTO and `CRS nnn°` in MANUAL. Preserve `DIR` for AUTO and TO/FROM for MANUAL.

- [x] **Step 3: Suppress redundant steering text only when exact**

Keep the lower `STEER` readout because drift can make it differ from bearing; the HSI label supplies the geometric target while `STEER` remains the commanded heading.

### Task 5: Verify and deploy

**Files:**
- Modify: `AGENTS.md`

- [x] **Step 1: Document panel layout rules**

Record fixed 28 px tool rows, opaque surfaces, amber state rails, and mode-specific HSI labels.

- [x] **Step 2: Run verification**

Run `dotnet run --project tests\NavMathHarness -c Release`, `dotnet build NOVor.csproj -c Release`, and `dotnet build NOVor.csproj -c Debug`. Expect 18 harness checks and zero build warnings/errors; Debug deploys to `BepInEx\scripts`.
