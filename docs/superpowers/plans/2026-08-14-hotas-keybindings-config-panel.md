# NO VOR HOTAS Keybindings and Controls Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an in-mod Controls screen where players can view, clear, and rebind every NO VOR hotkey, including binding physical HOTAS buttons to actions such as cycling to the next airport.

**Architecture:** Keep the existing BepInEx `ConfigEntry<KeyboardShortcut>` values as the persistence and compatibility contract. Add a focused input adapter that preserves BepInEx's exact keyboard-chord behavior but polls joystick main keys directly, so unrelated held HOTAS switches do not suppress a button press; add a child `KeyBindingPanel` owned by `NavPanel`, and route capture before gameplay hotkeys so the captured press is consumed. The first release supports keyboard keys/chords and Unity legacy-input joystick buttons; analog axes and POV hats that do not appear as `KeyCode` values are explicitly outside this feature.

**Tech Stack:** C# / .NET Framework 4.7.2, BepInEx 5 `ConfigEntry<KeyboardShortcut>`, Unity 2022.3 legacy `Input`, TextMeshPro/uGUI, Nuclear Option Mono, .NET 8 contract harness.

---

## Scope and behavior contract

- The existing `[Hotkeys]` keys and defaults remain unchanged. Existing user configuration files require no migration.
- The Controls screen exposes all eleven current mappings: next/previous airport, HUD visibility, nav panel visibility, course decrease/increase, direct-to, and four HUD nudge directions.
- Clicking a binding enters capture mode. `Escape` cancels without changing it; a dedicated `CLEAR` button writes `KeyboardShortcut.Empty`.
- Keyboard capture accepts one non-modifier main key plus any held Ctrl/Shift/Alt modifiers. Modifier-only actions are not supported by this screen.
- HOTAS capture accepts Unity `JoystickButton0` through `JoystickButton19` and device-specific `Joystick1Button0` through `Joystick8Button19`. If Unity reports generic and device-specific forms on the same frame, store the device-specific form.
- A joystick binding triggers on its main button's down edge and checks configured keyboard modifiers, but ignores unrelated held keyboard/joystick controls. Keyboard-main bindings retain BepInEx `KeyboardShortcut.IsDown()` exact-chord semantics.
- Capture consumes the entire input frame, including the successful press, so rebinding does not also invoke the action.
- Opening/closing the Controls screen does not create another canvas or cursor owner. It is a view inside the existing NO VOR panel.
- The Controls screen and capture state clean up safely during ScriptEngine hot reload.
- The feature does not add a `Rewired_Core` dependency, create game actions, bind analog axes, or interpret POV hats as axes.

## File map

| Path | Change | Responsibility |
|---|---|---|
| `Core/InputBindingPolicy.cs` | Create | Pure classification and preference rules testable without Unity |
| `Core/InputBinding.cs` | Create | Unity/BepInEx runtime triggering and capture |
| `UI/KeyBindingPanel.cs` | Create | Controls view, binding rows, capture state, and labels |
| `UI/NavPanel.cs` | Modify | Own/show the Controls view, route capture, and harden EventSystem cleanup |
| `Core/NavController.cs` | Modify | Consume capture before actions and use HOTAS-aware trigger checks |
| `tests/NavMathHarness/NavMathHarness.csproj` | Modify | Link the pure binding policy into the contract harness |
| `tests/NavMathHarness/Program.cs` | Modify | Verify joystick classification and specific-device preference |
| `AGENTS.md` | Modify | Document the Controls screen, input contract, and acceptance checks |

### Task 1: Add and test the pure binding policy

**Files:**
- Create: `Core/InputBindingPolicy.cs`
- Modify: `tests/NavMathHarness/NavMathHarness.csproj`
- Modify: `tests/NavMathHarness/Program.cs`

- [ ] **Step 1: Link the new policy source into the harness**

Add this item beside the other linked Core files in `tests/NavMathHarness/NavMathHarness.csproj`:

```xml
<Compile Include="..\..\Core\InputBindingPolicy.cs" Link="Core\InputBindingPolicy.cs" />
```

- [ ] **Step 2: Write failing policy checks**

Add these checks before the final `_failures` block in `tests/NavMathHarness/Program.cs`:

```csharp
True(InputBindingPolicy.IsJoystickButtonName("JoystickButton5"),
    "generic joystick button recognized");
True(InputBindingPolicy.IsJoystickButtonName("Joystick3Button12"),
    "device joystick button recognized");
True(!InputBindingPolicy.IsJoystickButtonName("N"),
    "keyboard key is not joystick button");
True(!InputBindingPolicy.IsJoystickButtonName("JoystickAxis1"),
    "joystick axis is not a supported button");
True(InputBindingPolicy.IsDeviceSpecificJoystickButtonName("Joystick3Button12"),
    "device joystick button preferred");
True(!InputBindingPolicy.IsDeviceSpecificJoystickButtonName("JoystickButton5"),
    "generic joystick button is fallback");
Equal(2d, InputBindingPolicy.CapturePreference("Joystick3Button12"),
    "device-specific joystick capture preference");
Equal(1d, InputBindingPolicy.CapturePreference("JoystickButton5"),
    "generic joystick capture preference");
Equal(0d, InputBindingPolicy.CapturePreference("N"),
    "keyboard capture preference");
```

Update the success message from `42 passed` to `51 passed`.

- [ ] **Step 3: Run the harness to verify the new source is missing**

Run:

```powershell
dotnet run --project tests\NavMathHarness -c Release
```

Expected: compilation fails because `Core/InputBindingPolicy.cs` does not exist or `InputBindingPolicy` is undefined.

- [ ] **Step 4: Implement the policy**

Create `Core/InputBindingPolicy.cs` with:

```csharp
using System;

namespace NOVor.Core
{
    public static class InputBindingPolicy
    {
        private const string JoystickPrefix = "Joystick";
        private const string ButtonMarker = "Button";

        public static bool IsJoystickButtonName(string name)
        {
            if (string.IsNullOrEmpty(name) ||
                !name.StartsWith(JoystickPrefix, StringComparison.Ordinal))
                return false;

            int marker = name.IndexOf(ButtonMarker, JoystickPrefix.Length,
                StringComparison.Ordinal);
            if (marker < JoystickPrefix.Length) return false;

            int buttonStart = marker + ButtonMarker.Length;
            if (buttonStart >= name.Length) return false;
            for (int i = buttonStart; i < name.Length; i++)
                if (!char.IsDigit(name[i])) return false;
            return true;
        }

        public static bool IsDeviceSpecificJoystickButtonName(string name)
        {
            if (!IsJoystickButtonName(name) || name.Length <= JoystickPrefix.Length)
                return false;
            return char.IsDigit(name[JoystickPrefix.Length]);
        }

        public static int CapturePreference(string name)
        {
            if (IsDeviceSpecificJoystickButtonName(name)) return 2;
            return IsJoystickButtonName(name) ? 1 : 0;
        }
    }
}
```

- [ ] **Step 5: Run the harness**

Run:

```powershell
dotnet run --project tests\NavMathHarness -c Release
```

Expected: `NavMathHarness: 51 passed`.

- [ ] **Step 6: Commit the policy contract**

```powershell
git add Core\InputBindingPolicy.cs tests\NavMathHarness\NavMathHarness.csproj tests\NavMathHarness\Program.cs
git commit -m "test: define hotas binding policy"
```

### Task 2: Add the HOTAS-aware runtime input adapter

**Files:**
- Create: `Core/InputBinding.cs`

- [ ] **Step 1: Create the runtime adapter**

Create `Core/InputBinding.cs` with:

```csharp
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVor.Core
{
    internal static class InputBinding
    {
        private static readonly KeyCode[] ModifierKeys =
        {
            KeyCode.LeftControl, KeyCode.RightControl,
            KeyCode.LeftShift, KeyCode.RightShift,
            KeyCode.LeftAlt, KeyCode.RightAlt
        };

        private static readonly KeyCode[] AllKeys =
            (KeyCode[])Enum.GetValues(typeof(KeyCode));

        public static bool IsDown(KeyboardShortcut shortcut)
        {
            KeyCode mainKey = shortcut.MainKey;
            if (mainKey == KeyCode.None) return false;
            if (!InputBindingPolicy.IsJoystickButtonName(mainKey.ToString()))
                return shortcut.IsDown();
            if (!Input.GetKeyDown(mainKey)) return false;
            foreach (KeyCode modifier in shortcut.Modifiers)
                if (!Input.GetKey(modifier)) return false;
            return true;
        }

        public static bool TryCapture(out KeyboardShortcut shortcut)
        {
            KeyCode fallback = KeyCode.None;
            int fallbackPreference = -1;

            for (int i = 0; i < AllKeys.Length; i++)
            {
                KeyCode key = AllKeys[i];
                if (!IsCapturableMainKey(key) || !Input.GetKeyDown(key)) continue;

                int preference = InputBindingPolicy.CapturePreference(key.ToString());
                if (preference == 2)
                {
                    shortcut = BuildShortcut(key);
                    return true;
                }
                if (preference > fallbackPreference)
                {
                    fallback = key;
                    fallbackPreference = preference;
                }
            }

            if (fallback != KeyCode.None)
            {
                shortcut = BuildShortcut(fallback);
                return true;
            }

            shortcut = KeyboardShortcut.Empty;
            return false;
        }

        private static bool IsCapturableMainKey(KeyCode key)
        {
            if (key == KeyCode.None || key == KeyCode.Escape) return false;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) return false;
            for (int i = 0; i < ModifierKeys.Length; i++)
                if (key == ModifierKeys[i]) return false;
            return true;
        }

        private static KeyboardShortcut BuildShortcut(KeyCode mainKey)
        {
            var modifiers = new List<KeyCode>();
            for (int i = 0; i < ModifierKeys.Length; i++)
                if (Input.GetKey(ModifierKeys[i])) modifiers.Add(ModifierKeys[i]);
            return modifiers.Count == 0
                ? new KeyboardShortcut(mainKey)
                : new KeyboardShortcut(mainKey, modifiers.ToArray());
        }
    }
}
```

This intentionally uses `KeyboardShortcut.IsDown()` for keyboard-main bindings and direct polling only for joystick-main bindings. Do not replace all shortcuts with relaxed keyboard matching; doing so would make `Ctrl+N` also fire an action bound to plain `N`.

- [ ] **Step 2: Compile the plugin**

Run:

```powershell
dotnet build NOVor.csproj -c Release
```

Expected: build succeeds with 0 errors and creates `bin\Release\novor-<version>.zip`.

- [ ] **Step 3: Commit the runtime adapter**

```powershell
git add Core\InputBinding.cs
git commit -m "feat: add hotas-aware input bindings"
```

### Task 3: Build the in-panel Controls screen

**Files:**
- Create: `UI/KeyBindingPanel.cs`

- [ ] **Step 1: Define the panel's ownership and public contract**

Create `UI/KeyBindingPanel.cs`. The class must expose exactly this lifecycle to `NavPanel`:

```csharp
internal sealed class KeyBindingPanel
{
    public bool IsVisible { get; private set; }
    public bool IsCapturing => _capturing != null;

    public void Create(Transform parent, Action closeRequested);
    public void SetVisible(bool visible);
    public bool TickCapture();
    public void CancelCapture();
}
```

`TickCapture()` returns `true` whenever capture was active at the beginning of the frame, including cancellation and a successful capture. This return value is the gameplay-input suppression contract used in Task 5.

- [ ] **Step 2: Define binding rows from existing config entries**

Inside `KeyBindingPanel`, add:

```csharp
private sealed class BindingRow
{
    public string Name;
    public ConfigEntry<KeyboardShortcut> Entry;
    public Button CaptureButton;
    public TextMeshProUGUI ValueLabel;
}

private readonly List<BindingRow> _rows = new List<BindingRow>();
private BindingRow _capturing;
private GameObject _root;
private Action _closeRequested;
```

In `Create`, build two equal-width columns and add the mappings in this order:

```csharp
AddBinding(left, "NEXT AIRPORT", Plugin.NextAirportKey);
AddBinding(left, "PREVIOUS AIRPORT", Plugin.PrevAirportKey);
AddBinding(left, "SHOW / HIDE HUD", Plugin.ToggleHudKey);
AddBinding(left, "OPEN / CLOSE PANEL", Plugin.ToggleMenuKey);
AddBinding(left, "COURSE DECREASE", Plugin.CourseDecreaseKey);
AddBinding(left, "COURSE INCREASE", Plugin.CourseIncreaseKey);

AddBinding(right, "DIRECT TO FIELD", Plugin.DirectToKey);
AddBinding(right, "HUD NUDGE UP", Plugin.HudNudgeUpKey);
AddBinding(right, "HUD NUDGE DOWN", Plugin.HudNudgeDownKey);
AddBinding(right, "HUD NUDGE LEFT", Plugin.HudNudgeLeftKey);
AddBinding(right, "HUD NUDGE RIGHT", Plugin.HudNudgeRightKey);
```

The screen structure is:

```text
CONTROLS                                             CLOSE
Keyboard key/chord or HOTAS button; Esc cancels capture.

NEXT AIRPORT       [ N                 ] [CLEAR] | DIRECT TO FIELD    [ \                 ] [CLEAR]
PREVIOUS AIRPORT   [ B                 ] [CLEAR] | HUD NUDGE UP       [ LeftControl + Up ] [CLEAR]
...

HOTAS BUTTONS ONLY — ANALOG AXES AND POV HATS ARE NOT AVAILABLE HERE
```

Use the existing `UiColors.Chrome`, `ChromeRaised`, `InstrumentWell`, `Rule`, `Amber`, `PanelText`, `PanelSecondaryText`, and `PanelDisabledText`, plus `FontLoader.GetDefaultFont()`. Keep the root at `PanelHeight - HeaderHeight - 22f` so switching views does not resize the nav window.

- [ ] **Step 3: Implement row actions and visual refresh**

The capture and clear callbacks must be:

```csharp
private void BeginCapture(BindingRow row)
{
    _capturing = row;
    ClearOwnedSelection();
    RefreshRows();
}

private void ClearBinding(BindingRow row)
{
    if (_capturing == row) _capturing = null;
    row.Entry.Value = KeyboardShortcut.Empty;
    RefreshRows();
}

private void RefreshRows()
{
    for (int i = 0; i < _rows.Count; i++)
    {
        BindingRow row = _rows[i];
        bool capturing = row == _capturing;
        row.ValueLabel.text = capturing
            ? "PRESS KEY / HOTAS…"
            : Format(row.Entry.Value);
        row.ValueLabel.color = capturing ? UiColors.Amber
            : row.Entry.Value.MainKey == KeyCode.None
                ? UiColors.PanelDisabledText
                : UiColors.PanelText;
        row.CaptureButton.GetComponent<Image>().color =
            capturing ? UiColors.SelectionSurface : UiColors.ChromeRaised;
    }
}

private static string Format(KeyboardShortcut shortcut)
{
    return shortcut.MainKey == KeyCode.None ? "<NOT BOUND>" : shortcut.ToString();
}
```

`ClearOwnedSelection()` must only clear a selected object belonging to this Controls root:

```csharp
private void ClearOwnedSelection()
{
    EventSystem eventSystem = EventSystem.current;
    GameObject selected = eventSystem != null
        ? eventSystem.currentSelectedGameObject
        : null;
    if (selected != null && _root != null &&
        selected.transform.IsChildOf(_root.transform))
        eventSystem.SetSelectedGameObject(null);
}
```

- [ ] **Step 4: Implement capture lifecycle**

Use this exact state logic:

```csharp
public bool TickCapture()
{
    if (_capturing == null) return false;

    if (Input.GetKeyDown(KeyCode.Escape))
    {
        CancelCapture();
        return true;
    }

    if (InputBinding.TryCapture(out KeyboardShortcut shortcut))
    {
        BindingRow row = _capturing;
        _capturing = null;
        row.Entry.Value = shortcut;
        Plugin.Log?.LogInfo("NO VOR: " + row.Name + " bound to " + shortcut);
        RefreshRows();
    }
    return true;
}

public void CancelCapture()
{
    if (_capturing == null) return;
    _capturing = null;
    RefreshRows();
}

public void SetVisible(bool visible)
{
    IsVisible = visible;
    if (!visible) CancelCapture();
    if (_root != null) _root.SetActive(visible);
    if (!visible) ClearOwnedSelection();
    else RefreshRows();
}
```

The `CLOSE` button calls `_closeRequested`; it must not directly manipulate `NavPanel` internals.

- [ ] **Step 5: Compile the standalone UI file**

Run:

```powershell
dotnet build NOVor.csproj -c Release
```

Expected at this stage: the build succeeds. `KeyBindingPanel` is compiled but not yet instantiated.

- [ ] **Step 6: Commit the Controls view**

```powershell
git add UI\KeyBindingPanel.cs
git commit -m "feat: add controls binding panel"
```

### Task 4: Integrate Controls into the navigation panel safely

**Files:**
- Modify: `UI/NavPanel.cs`

- [ ] **Step 1: Add Controls ownership state**

Add these fields beside `_body` and the header controls:

```csharp
private KeyBindingPanel _keyBindingPanel;
private Button _controlsButton;
private TextMeshProUGUI _controlsLabel;
private bool _showingControls;
```

- [ ] **Step 2: Harden the fallback EventSystem against HOTAS Submit**

Replace the owned EventSystem creation block in `Create()` with:

```csharp
if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
{
    _ownedEventSystem = new GameObject("NOVorEventSystem",
        typeof(EventSystem), typeof(StandaloneInputModule));
    var input = _ownedEventSystem.GetComponent<StandaloneInputModule>();
    input.submitButton = string.Empty;
    input.cancelButton = string.Empty;
    UnityEngine.Object.DontDestroyOnLoad(_ownedEventSystem);
}
```

This only changes the fallback module NO VOR owns. Never blank Submit/Cancel on the game's or another mod's EventSystem.

- [ ] **Step 3: Add the header control and build the Controls view**

In `BuildHeader`, insert this before the minimize button:

```csharp
_controlsButton = MakeButton(header.transform, "CFG", 44f, 30f, ToggleControls, 9);
_controlsLabel = _controlsButton.GetComponentInChildren<TextMeshProUGUI>();
StyleHeaderControl(_controlsButton, _controlsLabel);
```

In `Create()`, immediately after `BuildBody(panel.transform)`, add:

```csharp
_keyBindingPanel = new KeyBindingPanel();
_keyBindingPanel.Create(panel.transform, () => ShowControls(false));
_keyBindingPanel.SetVisible(false);
```

- [ ] **Step 4: Add view switching and capture routing**

Add:

```csharp
private void ToggleControls()
{
    ShowControls(!_showingControls);
}

private void ShowControls(bool visible)
{
    _showingControls = visible;
    if (visible && _minimized)
    {
        _minimized = false;
        _minimizeLabel.text = "−";
        _panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
    }
    if (_body != null) _body.SetActive(!visible && !_minimized);
    _keyBindingPanel?.SetVisible(visible);
    StyleToggle(_controlsButton, _controlsLabel, visible);
}

public bool TickBindingCapture()
{
    return _keyBindingPanel != null && _keyBindingPanel.TickCapture();
}
```

Update `ToggleMinimized()` so minimizing always leaves the Controls view and applies body state through the same view logic:

```csharp
private void ToggleMinimized()
{
    if (_showingControls) ShowControls(false);
    _minimized = !_minimized;
    _body.SetActive(!_minimized);
    _minimizeLabel.text = _minimized ? "+" : "−";
    _panelRt.sizeDelta = new Vector2(PanelWidth,
        _minimized ? HeaderHeight + 16f : PanelHeight);
}
```

- [ ] **Step 5: Stop capture when the whole panel closes**

At the start of the `visible == false` branch in `SetVisible`, add:

```csharp
_keyBindingPanel?.CancelCapture();
```

Do not force the Controls view closed here; reopening the nav panel may return to Controls, but there must never be an invisible active capture.

- [ ] **Step 6: Clear stale uGUI selection during teardown**

At the start of `Destroy()`, before destroying `_root`, add:

```csharp
var eventSystem = EventSystem.current;
var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
if (selected != null && _root != null &&
    selected.transform.IsChildOf(_root.transform))
    eventSystem.SetSelectedGameObject(null);
_keyBindingPanel?.CancelCapture();
```

- [ ] **Step 7: Build the integrated panel**

Run:

```powershell
dotnet build NOVor.csproj -c Release
```

Expected: build succeeds with 0 errors.

- [ ] **Step 8: Commit the panel integration**

```powershell
git add UI\NavPanel.cs
git commit -m "feat: expose controls in nav panel"
```

### Task 5: Route gameplay input through capture and HOTAS handling

**Files:**
- Modify: `Core/NavController.cs`

- [ ] **Step 1: Consume binding capture before any action**

Make the first line of `HandleInput()`:

```csharp
if (_panel != null && _panel.TickBindingCapture()) return;
```

This line must precede next-airport, panel-toggle, course, and HUD-nudge checks. Otherwise the press selected as a new binding can fire an action in the same frame.

- [ ] **Step 2: Use the HOTAS-aware adapter for every exposed mapping**

Replace `HandleInput()` with:

```csharp
private void HandleInput()
{
    if (_panel != null && _panel.TickBindingCapture()) return;

    if (InputBinding.IsDown(Plugin.NextAirportKey.Value)) CycleAirport(1);
    if (InputBinding.IsDown(Plugin.PrevAirportKey.Value)) CycleAirport(-1);
    if (InputBinding.IsDown(Plugin.ToggleHudKey.Value)) _hudVisible = !_hudVisible;
    if (InputBinding.IsDown(Plugin.ToggleMenuKey.Value)) _panel?.Toggle();
    if (InputBinding.IsDown(Plugin.CourseDecreaseKey.Value))
        AdjustCourse(-Plugin.CourseStep.Value);
    if (InputBinding.IsDown(Plugin.CourseIncreaseKey.Value))
        AdjustCourse(Plugin.CourseStep.Value);
    if (InputBinding.IsDown(Plugin.DirectToKey.Value)) SetManualCourse(Data.Bearing);

    float step = Plugin.HudNudgeStep.Value;
    if (InputBinding.IsDown(Plugin.HudNudgeUpKey.Value)) NudgeInstrument(0f, step);
    if (InputBinding.IsDown(Plugin.HudNudgeDownKey.Value)) NudgeInstrument(0f, -step);
    if (InputBinding.IsDown(Plugin.HudNudgeLeftKey.Value)) NudgeInstrument(-step, 0f);
    if (InputBinding.IsDown(Plugin.HudNudgeRightKey.Value)) NudgeInstrument(step, 0f);
}
```

Applying the adapter consistently is necessary because the Controls screen permits a HOTAS button on every row, not only Next Airport.

- [ ] **Step 3: Run automated and compile verification**

Run in this order:

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
dotnet build NOVor.csproj -c Debug
```

Expected:

- harness prints `NavMathHarness: 51 passed`;
- Release build succeeds with 0 errors and produces the distribution zip;
- Debug build succeeds with 0 errors and deploys only to `BepInEx\scripts`.

- [ ] **Step 4: Commit input routing**

```powershell
git add Core\NavController.cs
git commit -m "feat: route nav actions from hotas buttons"
```

### Task 6: Document and manually verify the feature in Nuclear Option

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Document the new file and input behavior**

Add this row to the Code Layout table in `AGENTS.md`:

```markdown
| `UI/KeyBindingPanel.cs` | in-panel keyboard/HOTAS binding capture and Controls UI |
```

Add these points to Game API Patterns:

```markdown
- The nav panel's `CFG` control opens the in-panel Controls view. Binding changes write directly to the existing BepInEx `[Hotkeys]` `KeyboardShortcut` entries.
- Keyboard-main bindings retain BepInEx exact-chord behavior. Joystick-main bindings use a down-edge check that ignores unrelated held HOTAS controls while still requiring configured keyboard modifiers.
- Binding capture prefers device-specific `JoystickNButtonM` values when Unity reports both device-specific and generic joystick buttons. Analog axes and POV hats are outside the Controls screen contract.
- Binding capture runs before navigation actions and consumes its entire frame; closing or hot-reloading cancels capture and clears owned uGUI selection.
```

Under Default Hotkeys, add:

```markdown
- Open the nav panel and select `CFG` to view, clear, or rebind controls to keyboard keys/chords or HOTAS buttons.
```

- [ ] **Step 2: Verify keyboard capture and persistence**

In a mission with at least two detected airfields:

1. Open the nav panel and click `CFG`.
2. Click `NEXT AIRPORT`, press `M`, and confirm the row changes to `M` without changing airport on the capture frame.
3. Close the panel, press `M` once, and confirm the selected airport advances exactly once.
4. Press the old `N` binding and confirm it no longer advances.
5. Hot reload with Insert, reopen `CFG`, and confirm `M` remains displayed and still works.

- [ ] **Step 3: Verify keyboard chord, cancel, and clear behavior**

1. Capture `LeftControl + N` for Next Airport; confirm plain `N` does nothing and Ctrl+N advances once.
2. Start capture again, press Escape, and confirm Ctrl+N remains assigned.
3. Click `CLEAR`, confirm `<NOT BOUND>`, and verify neither plain N nor Ctrl+N advances.
4. Rebind Next Airport to the desired final keyboard or HOTAS control before continuing.

- [ ] **Step 4: Verify HOTAS capture and runtime semantics**

1. Start Next Airport capture and press the intended HOTAS button.
2. Confirm the row shows `JoystickNButtonM` when Unity exposes a device-specific value; accept `JoystickButtonM` only when the device-specific value is unavailable.
3. Close the panel and press the HOTAS button once; confirm the airport advances exactly once.
4. Hold another HOTAS button or latching switch, then press the bound button; confirm it still advances exactly once.
5. Hold the bound button; confirm it does not repeat every frame.
6. Verify the saved `BepInEx/config/com.novor.cdi.cfg` `NextAirport` value matches the row.

- [ ] **Step 5: Verify UI input isolation and hot reload**

1. Open `CFG`, click a binding, and press a HOTAS button also mapped by Unity as Submit; confirm it is captured and does not click another selected control.
2. Begin capture and hot reload with Insert; confirm there is one NO VOR panel, no disposed-object exception, and no invisible capture after reload.
3. Close the panel and confirm camera movement, zoom, cursor lock, and the game's own menu Submit/Cancel behavior are restored.
4. Temporarily run without NO Mod Bar and confirm NO VOR's owned EventSystem accepts mouse clicks but ignores HOTAS Submit.

- [ ] **Step 6: Run the final verification cycle**

```powershell
dotnet run --project tests\NavMathHarness -c Release
dotnet build NOVor.csproj -c Release
dotnet build NOVor.csproj -c Debug
```

Expected: 51 harness checks pass; all builds succeed with 0 errors; Debug deploys to the ScriptEngine directory.

- [ ] **Step 7: Commit documentation**

```powershell
git add AGENTS.md
git commit -m "docs: describe controls and hotas bindings"
```

## Final acceptance criteria

- `CFG` is visible in the NO VOR nav-panel header and opens a Controls view inside the same panel.
- All existing hotkeys appear with current persisted values; each can be captured, cancelled, or cleared.
- Next Airport accepts an ordinary key, a keyboard chord, a generic joystick button, or a device-specific HOTAS button.
- A captured press never invokes its action in the capture frame.
- HOTAS bindings fire once per press even when unrelated HOTAS controls remain held.
- Existing keyboard config values and default controls remain compatible.
- No game/global EventSystem is modified; only the fallback EventSystem owned by NO VOR has Submit/Cancel disabled.
- Closing and hot reload restore cursor/camera state and leave no stale selection or capture.
- Axes and POV hats are described as unsupported instead of silently pretending to bind them.
- The harness and both Release/Debug builds pass.

## Self-review

- **Spec coverage:** The Controls screen is implemented in Tasks 3-4; Next Airport and all current mappings are exposed there; HOTAS capture and held-switch-safe triggering are implemented in Tasks 1-2 and 5; persistence, UI isolation, and reload behavior are covered in Tasks 4 and 6.
- **Placeholder scan:** Every code-changing step names the file, exact contract, code, command, and expected result. There are no deferred implementation instructions.
- **Type consistency:** `InputBindingPolicy` is public for the linked harness; `InputBinding` is internal runtime code; `KeyBindingPanel.TickCapture()` and `NavPanel.TickBindingCapture()` both return the consumed-frame boolean used by `NavController`; all rows use the existing `ConfigEntry<KeyboardShortcut>` fields from `Plugin`.
