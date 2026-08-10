# NO VOR — Airport & Course Selection UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the IMGUI airport menu with a SITREP-style uGUI panel for selecting an airport and dialing a manual course (CRS), plus full manual-course CDI math (TO/FROM, deviation against the dialed course).

**Architecture:** The mod already polls `GameManager.GetLocalAircraft` + `Object.FindObjectsOfType<Airbase>()` each frame in `NavController` and feeds a `CdiData` struct to the HUD `CdiInstrument`. We add a manual course mode to that data path, and build a standalone `NavPanel` — a ScreenSpaceOverlay uGUI canvas (CanvasScaler 1920x1080) with a draggable, bordered, scanline-textured panel containing an airport list and course controls. `NavPanel` is decoupled from game types via plain `AirportInfo` structs and C# events.

**Tech Stack:** BepInEx 5, net472, Unity 2022.3.6 uGUI (`UnityEngine.UI`), TextMeshPro (`Unity.TextMeshPro.dll`, `TMPro` namespace), ScriptEngine hot reload (Debug build auto-deploys to `BepInEx/scripts/`).

## Global Constraints

- Repo convention: **no code comments**. All code below is written comment-free.
- Target `net472`, `LangVersion latest`, no `unsafe` blocks (matches `NOVor.csproj`).
- Game path via `$(NuclearOptionRoot)` (already set in `Local.props` → `D:\SteamLibrary\steamapps\common\Nuclear Option`).
- **Never** copy `NOVor.dll` into `BepInEx/plugins/`; Debug builds deploy to `BepInEx/scripts/` only (ScriptEngine is the single loader, `LoadOnStart=true`).
- Hot reload: after `dotnet build -c Debug` the ScriptEngine file watcher reloads in ~3 s, or press the configured reload key (`Insert` in the current `com.bepis.bepinex.scriptengine.cfg`).
- There is **no unit-test harness** in this repo (game-dependent Mono plugin). The test cycle for every task is: `dotnet build -c Debug` (must be 0 errors) → in-game manual verification → commit.
- UI style follows SITREP: dark navy panel `0x0e0e1a` with border `0x2a2a5a`, animated scanline overlay, TMP text, HUD green `#33ff99` highlights, draggable title bar, cursor unlock while the panel is open.
- Existing CDI instrument stays on legacy uGUI `Text`; only the new panel uses TMPro.

---

### Task 1: UI Toolkit + TMPro Reference

Create the SITREP-derived UI primitives the panel will use, and add the TextMeshPro assembly reference so `TMPro` types compile.

**Files:**
- Create: `UI/UiColors.cs`
- Create: `UI/TextureFactory.cs`
- Create: `UI/FontLoader.cs`
- Create: `UI/WindowDragHandler.cs`
- Modify: `NOVor.csproj` (add `Unity.TextMeshPro` reference)
- Create: `.gitignore`, run `git init`

**Interfaces:**
- Consumes: `NOVor.csproj` (existing `<ItemGroup>` of `Reference` items).
- Produces: `NOVor.UI.UiColors` (static palette), `NOVor.UI.TextureFactory.CreatePanelBackground(w,h,bg,border,borderWidth)` and `.CreateScanlineTexture(w,h)`, `NOVor.UI.FontLoader.GetDefaultFont()` → `TMP_FontAsset`, `NOVor.UI.WindowDragHandler` (component with `Init(target, canvasRect)`).

- [ ] **Step 1: Add TMPro reference to `NOVor.csproj`**

Add to the existing `<ItemGroup>` that holds the game references (after the `UnityEngine.InputLegacyModule` block):

```xml
    <Reference Include="Unity.TextMeshPro">
      <HintPath>$(NuclearOptionRoot)\NuclearOption_Data\Managed\Unity.TextMeshPro.dll</HintPath>
      <Private>false</Private>
    </Reference>
```

- [ ] **Step 2: Create `UI/UiColors.cs`**

```csharp
using UnityEngine;

namespace NOVor.UI
{
    internal static class UiColors
    {
        public static readonly Color BgPanel = Hex(0x0e0e1a);
        public static readonly Color BgPanelRaised = Hex(0x161626);
        public static readonly Color BorderSubtle = Hex(0x1e1e3a);
        public static readonly Color BorderPanel = Hex(0x2a2a5a);
        public static readonly Color HudGreen = Hex(0x33ff99);
        public static readonly Color HudGreenDim = Hex(0x1a8a55);
        public static readonly Color HudAmber = Hex(0xffb347);
        public static readonly Color TextPrimary = Hex(0xccffdd);
        public static readonly Color TextSecondary = Hex(0x77aa88);
        public static readonly Color TextMuted = Hex(0x3a6644);

        private static Color Hex(int rgb)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f;
            float g = ((rgb >> 8) & 0xFF) / 255f;
            float b = (rgb & 0xFF) / 255f;
            return new Color(r, g, b);
        }
    }
}
```

- [ ] **Step 3: Create `UI/TextureFactory.cs`**

```csharp
using UnityEngine;

namespace NOVor.UI
{
    internal static class TextureFactory
    {
        private static Texture2D _cachedPanelBg;
        private static Texture2D _cachedScanline;

        public static Texture2D CreatePanelBackground(int width, int height, Color bgColor, Color borderColor, float borderWidth = 1f)
        {
            if (_cachedPanelBg == null)
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        bool onBorder = x < borderWidth || x >= width - borderWidth ||
                                        y < borderWidth || y >= height - borderWidth;
                        pixels[y * width + x] = onBorder ? borderColor : bgColor;
                    }
                tex.SetPixels(pixels);
                tex.Apply();
                _cachedPanelBg = tex;
            }
            return _cachedPanelBg;
        }

        public static Texture2D CreateScanlineTexture(int width, int height, float lineThickness = 2f, float lineSpacing = 4f)
        {
            if (_cachedScanline == null)
            {
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Repeat;
                var pixels = new Color[width * height];
                var dark = new Color(0, 0, 0, 0.06f);
                var clear = new Color(0, 0, 0, 0);
                for (int y = 0; y < height; y++)
                {
                    float rowInPattern = y % (lineThickness + lineSpacing);
                    Color c = rowInPattern < lineThickness ? dark : clear;
                    for (int x = 0; x < width; x++)
                        pixels[y * width + x] = c;
                }
                tex.SetPixels(pixels);
                tex.Apply();
                _cachedScanline = tex;
            }
            return _cachedScanline;
        }
    }
}
```

- [ ] **Step 4: Create `UI/FontLoader.cs`**

```csharp
using TMPro;
using UnityEngine;

namespace NOVor.UI
{
    internal static class FontLoader
    {
        private static TMP_FontAsset _cached;

        public static TMP_FontAsset GetDefaultFont()
        {
            if (_cached != null) return _cached;
            if (TMP_Settings.defaultFontAsset != null)
            {
                _cached = TMP_Settings.defaultFontAsset;
                return _cached;
            }
            var fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (fallback != null) _cached = fallback;
            return _cached;
        }
    }
}
```

- [ ] **Step 5: Create `UI/WindowDragHandler.cs`**

```csharp
using UnityEngine;
using UnityEngine.EventSystems;

namespace NOVor.UI
{
    public class WindowDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform _target;
        private RectTransform _canvasRect;
        private Vector2 _startLocal;
        private Vector2 _startAnchored;
        private bool _dragging;

        public void Init(RectTransform target, RectTransform canvasRect)
        {
            _target = target;
            _canvasRect = canvasRect;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_target == null || _canvasRect == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, eventData.position, null, out _startLocal)) return;
            _startAnchored = _target.anchoredPosition;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _target == null || _canvasRect == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, eventData.position, null, out var local)) return;
            _target.anchoredPosition = ClampPosition(_startAnchored + (local - _startLocal));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }

        private Vector2 ClampPosition(Vector2 pos)
        {
            if (_target == null || _canvasRect == null) return pos;
            var half = _target.sizeDelta * 0.5f;
            var size = _canvasRect.rect.size;
            pos.x = Mathf.Clamp(pos.x, half.x - size.x * 0.5f, size.x * 0.5f - half.x);
            pos.y = Mathf.Clamp(pos.y, half.y - size.y * 0.5f, size.y * 0.5f - half.y);
            return pos;
        }
    }
}
```

- [ ] **Step 6: Create `.gitignore` and init the repo**

`.gitignore`:

```
bin/
obj/
*.user
.vs/
```

Run: `git init` then commit:

```bash
git add .gitignore NOVor.csproj Local.props Directory.Build.props MyPluginInfo.cs Plugin.cs Core/ UI/ deploy.ps1
git commit -m "chore: init repo, add UI toolkit primitives and TMPro reference"
```

- [ ] **Step 7: Verify build**

Run: `dotnet build -c Debug`
Expected: `Build succeeded. 0 Error(s)` and `Deployed NOVor.dll to ...\BepInEx\scripts`.

---

### Task 2: Manual Course Model, Config, and CDI Math

Add the manual course mode to the data path so a dialed course drives the CDI needle (deviation vs. dialed CRS) with TO/FROM logic. This is fully testable with the `[` / `]` keys before any panel exists.

**Files:**
- Modify: `Core/CdiData.cs` (add `Mode`, `ToStation`)
- Create: `Core/NavModels.cs` (`CourseMode`, `AirportInfo`)
- Modify: `Plugin.cs` (config entries: course hotkeys, manual-course defaults, step)
- Modify: `Core/NavController.cs` (course keys, `AdjustCourse`, mode-aware `UpdateData`)
- Modify: `UI/CdiInstrument.cs` (show mode tag + TO/FROM)

**Interfaces:**
- Consumes: `CdiData` (existing fields `Heading`, `Bearing`, `Course`, `Deviation`, `Deflection`, `DistanceKm`, `AirportName`).
- Produces: `NOVor.Core.CourseMode { Auto, Manual }`; `NOVor.Core.AirportInfo { string Name; float Bearing; float DistanceKm; bool HasPosition; }`; `CdiData.Mode`, `CdiData.ToStation`; `Plugin.CourseDecreaseKey`, `Plugin.CourseIncreaseKey`, `Plugin.CourseModeManual`, `Plugin.DefaultManualCourse`, `Plugin.CourseStep`; `NavController.AdjustCourse(float)`.

- [ ] **Step 1: Create `Core/NavModels.cs`**

```csharp
namespace NOVor.Core
{
    public enum CourseMode
    {
        Auto,
        Manual
    }

    public struct AirportInfo
    {
        public string Name;
        public float Bearing;
        public float DistanceKm;
        public bool HasPosition;
    }
}
```

- [ ] **Step 2: Modify `Core/CdiData.cs`**

Replace the whole file with:

```csharp
namespace NOVor.Core
{
    public class CdiData
    {
        public float Heading;
        public float Bearing;
        public float Course;
        public float Deviation;
        public float Deflection;
        public float DistanceKm;
        public string AirportName;
        public CourseMode Mode;
        public bool ToStation;
    }
}
```

- [ ] **Step 3: Add config entries to `Plugin.cs`**

Add these static fields next to the existing ones (after `HudOffsetY`):

```csharp
        internal static ConfigEntry<KeyboardShortcut> CourseDecreaseKey;
        internal static ConfigEntry<KeyboardShortcut> CourseIncreaseKey;
        internal static ConfigEntry<bool> CourseModeManual;
        internal static ConfigEntry<float> DefaultManualCourse;
        internal static ConfigEntry<float> CourseStep;
```

Add these binds inside `Awake()` after the existing `ToggleMenuKey` bind:

```csharp
            CourseDecreaseKey = Config.Bind("Hotkeys", "CourseDecrease", new KeyboardShortcut(KeyCode.LeftBracket),
                "Decrease the manual course (switches to manual course mode).");
            CourseIncreaseKey = Config.Bind("Hotkeys", "CourseIncrease", new KeyboardShortcut(KeyCode.RightBracket),
                "Increase the manual course (switches to manual course mode).");

            CourseModeManual = Config.Bind("Navigation", "ManualCourseByDefault", false,
                "Start in manual course mode instead of automatic direct-to-airport.");
            DefaultManualCourse = Config.Bind("Navigation", "DefaultManualCourse", 90f,
                new ConfigDescription("Initial manual course in degrees (0-359).",
                    new AcceptableValueRange<float>(0f, 359f)));
            CourseStep = Config.Bind("Navigation", "CourseStep", 1f,
                new ConfigDescription("Course change per key press, in degrees.",
                    new AcceptableValueRange<float>(1f, 30f)));
```

- [ ] **Step 4: Modify `Core/NavController.cs`**

Replace the whole file with:

```csharp
using System.Collections.Generic;
using UnityEngine;
using NOVor.Core;
using NOVor.UI;

namespace NOVor
{
    public class NavController : MonoBehaviour
    {
        private const float AirbaseRefreshInterval = 1f;

        private readonly List<Airbase> _airbases = new List<Airbase>();
        private float _refreshTimer;
        private int _selectedIndex = -1;
        private Aircraft _aircraft;
        private bool _hudVisible = true;
        private bool _menuVisible;
        private CdiInstrument _instrument;
        private CourseMode _mode = CourseMode.Auto;
        private float _manualCourse;

        public CdiData Data { get; private set; } = new CdiData();

        public bool HasSelection => _selectedIndex >= 0 && _selectedIndex < _airbases.Count;

        private void Awake()
        {
            _mode = Plugin.CourseModeManual.Value ? CourseMode.Manual : CourseMode.Auto;
            _manualCourse = Mathf.Repeat(Plugin.DefaultManualCourse.Value, 360f);
        }

        private void Update()
        {
            HandleInput();

            if (!GameManager.GetLocalAircraft(out _aircraft))
            {
                SetInstrumentVisible(false);
                return;
            }

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = AirbaseRefreshInterval;
                RefreshAirbases();
            }

            if (!HasSelection)
            {
                SetInstrumentVisible(false);
                return;
            }

            UpdateData();
            EnsureInstrument();
            SetInstrumentVisible(_hudVisible);
            _instrument?.SetData(Data, _selectedIndex, _airbases.Count);
        }

        private void HandleInput()
        {
            if (Plugin.NextAirportKey.Value.IsDown()) CycleAirport(1);
            if (Plugin.PrevAirportKey.Value.IsDown()) CycleAirport(-1);
            if (Plugin.ToggleHudKey.Value.IsDown()) _hudVisible = !_hudVisible;
            if (Plugin.ToggleMenuKey.Value.IsDown()) _menuVisible = !_menuVisible;
            if (Plugin.CourseDecreaseKey.Value.IsDown()) AdjustCourse(-Plugin.CourseStep.Value);
            if (Plugin.CourseIncreaseKey.Value.IsDown()) AdjustCourse(Plugin.CourseStep.Value);
        }

        private void AdjustCourse(float delta)
        {
            _manualCourse = Mathf.Repeat(_manualCourse + delta, 360f);
            _mode = CourseMode.Manual;
        }

        private void CycleAirport(int direction)
        {
            if (_airbases.Count == 0) return;
            _selectedIndex = (_selectedIndex + direction) % _airbases.Count;
            if (_selectedIndex < 0) _selectedIndex += _airbases.Count;
        }

        private void RefreshAirbases()
        {
            var all = Object.FindObjectsOfType<Airbase>();
            _airbases.Clear();
            foreach (var ab in all)
            {
                if (ab == null || ab.disabled || ab.center == null) continue;
                _airbases.Add(ab);
            }
            if (_airbases.Count == 0)
            {
                _selectedIndex = -1;
                return;
            }
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _airbases.Count - 1);
        }

        private void UpdateData()
        {
            var rb = _aircraft.rb;
            if (rb == null) return;

            var pos = rb.transform.position;
            float heading = Mathf.Repeat(rb.transform.eulerAngles.y, 360f);

            var target = _airbases[_selectedIndex];
            var tpos = target.center.position;
            var to = tpos - pos;
            float bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            if (bearing < 0f) bearing += 360f;
            float distance = new Vector2(to.x, to.z).magnitude;

            Data.Heading = heading;
            Data.Bearing = bearing;
            Data.DistanceKm = distance / 1000f;
            Data.AirportName = target.name;
            Data.Mode = _mode;

            if (_mode == CourseMode.Manual)
            {
                Data.Course = _manualCourse;
                float diff = Mathf.DeltaAngle(_manualCourse, bearing);
                Data.ToStation = Mathf.Abs(diff) <= 90f;
            }
            else
            {
                Data.Course = bearing;
                Data.ToStation = true;
            }

            Data.Deviation = Mathf.DeltaAngle(Data.Course, heading);
            Data.Deflection = Mathf.Clamp(Data.Deviation / Plugin.FullDeflectionDeg.Value, -1f, 1f);
        }

        private void EnsureInstrument()
        {
            if (_instrument != null) return;

            Transform hudCenter = null;
            try
            {
                var hud = SceneSingleton<FlightHud>.i;
                if (hud != null) hudCenter = hud.GetHUDCenter();
            }
            catch
            {
                hudCenter = null;
            }

            if (hudCenter == null) return;

            var host = new GameObject("NOVorCdiInstrument", typeof(RectTransform));
            host.transform.SetParent(hudCenter, false);
            _instrument = host.AddComponent<CdiInstrument>();
            _instrument.ApplyOffsets(Plugin.HudOffsetX.Value, Plugin.HudOffsetY.Value);
        }

        private void SetInstrumentVisible(bool visible)
        {
            if (_instrument != null) _instrument.SetVisible(visible);
        }

        private void OnGUI()
        {
            if (!_menuVisible) return;

            var rect = new Rect(Screen.width / 2f - 220f, 40f, 440f, 460f);
            rect.y = Mathf.Min(rect.y, Screen.height - 500f);
            GUI.Window(4711, rect, DrawMenu, "NO VOR - Airports");
        }

        private void DrawMenu(int id)
        {
            if (_airbases.Count == 0)
            {
                GUILayout.Label("No airports detected. Fly a mission and try again.");
                GUI.DragWindow();
                return;
            }

            GUILayout.Label($"Select an airport ({_airbases.Count} detected)");
            GUILayout.Space(4f);
            for (int i = 0; i < _airbases.Count; i++)
            {
                var ab = _airbases[i];
                var prefix = i == _selectedIndex ? "> " : "   ";
                if (GUILayout.Button(prefix + ab.name))
                {
                    _selectedIndex = i;
                }
            }
            GUI.DragWindow();
        }

        private void OnDestroy()
        {
            if (_instrument != null) Destroy(_instrument.gameObject);
        }
    }
}
```

- [ ] **Step 5: Modify `UI/CdiInstrument.cs`**

In `SetData`, update the title and data text to show the mode and TO/FROM. Replace the existing `SetData` body with:

```csharp
        public void SetData(CdiData data, int index, int count)
        {
            if (!isActiveAndEnabled) return;

            string modeTag = data.Mode == CourseMode.Manual ? "MAN" : "AUTO";
            _titleText.text = $"{data.AirportName}   [{index + 1}/{count}]   {modeTag}";
            _dataText.text =
                $"HDG {Mathf.RoundToInt(data.Heading):000}\u00b0   " +
                $"CRS {Mathf.RoundToInt(data.Course):000}\u00b0   " +
                $"BRG {Mathf.RoundToInt(data.Bearing):000}\u00b0   " +
                $"DEV {Mathf.RoundToInt(data.Deviation):+#;-#;0}\u00b0   " +
                $"D {data.DistanceKm:F1} km   {(data.ToStation ? "TO" : "FROM")}";

            if (_needle != null)
                _needle.anchoredPosition = new Vector2(-data.Deflection * NeedleTravelPx, 0f);
        }
```

- [ ] **Step 6: Verify build + in-game**

Run: `dotnet build -c Debug`
Expected: `Build succeeded. 0 Error(s)`, deployed to scripts. In-game (hot reload or restart):
1. Fly, confirm the HUD instrument shows `AUTO` and CRS = BRG.
2. Press `[` → instrument flips to `MAN`, CRS decreases by `CourseStep`.
3. Turn the aircraft; the needle deviates against the dialed CRS, `TO`/`FROM` flips when the dialed course is >90° from the bearing to the airport.

- [ ] **Step 7: Commit**

```bash
git add Core/ Plugin.cs UI/CdiInstrument.cs
git commit -m "feat: manual course mode with TO/FROM CDI math"
```

---

### Task 3: NavPanel Shell + Course Controls

Build the SITREP-style panel (canvas, draggable bordered panel, scanlines, course section) and wire it into `NavController`, replacing the IMGUI menu.

**Files:**
- Create: `UI/UiAnimator.cs`
- Create: `UI/NavPanel.cs`
- Modify: `Core/NavController.cs` (create/toggle/destroy panel, course callbacks, remove IMGUI menu)

**Interfaces:**
- Consumes: `UiColors`, `TextureFactory`, `FontLoader`, `WindowDragHandler` (Task 1); `CourseMode`, `AirportInfo`, `Plugin` config (Task 2).
- Produces: `NOVor.UI.NavPanel` with `Create()`, `SetVisible(bool)`, `Toggle()`, `SetCourse(CourseMode, float course, bool toStation)`, `Destroy()`, and events `AirportSelected(int)`, `ModeChanged(CourseMode)`, `CourseAdjusted(float)`, `SetCourseToBearing`, `SetCourseToHeading`.

- [ ] **Step 1: Create `UI/UiAnimator.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace NOVor.UI
{
    public class UiAnimator : MonoBehaviour
    {
        private RawImage _scanlineImage;
        private float _scrollSpeed = 0.5f;

        public void Init(RawImage scanlineImage)
        {
            _scanlineImage = scanlineImage;
            if (_scanlineImage != null)
                StartCoroutine(ScrollScanlines());
        }

        private IEnumerator ScrollScanlines()
        {
            var uvRect = _scanlineImage.uvRect;
            while (true)
            {
                uvRect.y += Time.deltaTime * _scrollSpeed;
                _scanlineImage.uvRect = uvRect;
                yield return null;
            }
        }
    }
}
```

- [ ] **Step 2: Create `UI/NavPanel.cs`**

```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    public class NavPanel
    {
        private const float PanelWidth = 440f;
        private const float PanelHeight = 640f;

        private GameObject _root;
        private RectTransform _panelRt;
        private readonly List<GameObject> _airportRows = new List<GameObject>();
        private readonly List<Image> _rowBg = new List<Image>();
        private readonly List<TextMeshProUGUI> _rowName = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _rowMeta = new List<TextMeshProUGUI>();

        private TextMeshProUGUI _crsReadout;
        private TextMeshProUGUI _toFrom;
        private Image _autoBg;
        private Image _manualBg;

        private CursorLockMode _prevLockState;
        private bool _prevCursorVisible;
        private bool _visible = true;

        public event Action<int> AirportSelected;
        public event Action<CourseMode> ModeChanged;
        public event Action<float> CourseAdjusted;
        public event Action SetCourseToBearing;
        public event Action SetCourseToHeading;

        public void Create()
        {
            _prevLockState = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;

            _root = new GameObject("NOVorNavCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            UnityEngine.Object.DontDestroyOnLoad(_root);

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                var esGo = new GameObject("NOVorEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                UnityEngine.Object.DontDestroyOnLoad(esGo);
            }

            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(RawImage));
            panelGo.transform.SetParent(_root.transform, false);
            _panelRt = panelGo.GetComponent<RectTransform>();
            _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRt.pivot = new Vector2(0.5f, 0.5f);
            _panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panelRt.anchoredPosition = new Vector2(-620f, 0f);
            var panelImage = panelGo.GetComponent<RawImage>();
            panelImage.texture = TextureFactory.CreatePanelBackground(64, 64, UiColors.BgPanel, UiColors.BorderPanel, 2f);
            panelImage.color = Color.white;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.sizeDelta = new Vector2(-20, 30);
            titleRt.anchoredPosition = new Vector2(0, -8);
            var titleText = titleGo.GetComponent<TextMeshProUGUI>();
            titleText.font = FontLoader.GetDefaultFont();
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = UiColors.HudGreen;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.text = ">> NO VOR NAV <<";
            var drag = titleGo.AddComponent<WindowDragHandler>();
            drag.Init(_panelRt, _root.GetComponent<RectTransform>());

            var slGo = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
            slGo.transform.SetParent(panelGo.transform, false);
            var slRt = slGo.GetComponent<RectTransform>();
            slRt.anchorMin = Vector2.zero;
            slRt.anchorMax = Vector2.one;
            slRt.offsetMin = Vector2.zero;
            slRt.offsetMax = Vector2.zero;
            var slImage = slGo.GetComponent<RawImage>();
            slImage.texture = TextureFactory.CreateScanlineTexture(4, 6);
            slImage.color = Color.white;
            slImage.raycastTarget = false;

            var animator = panelGo.AddComponent<UiAnimator>();
            animator.Init(slImage);

            BuildCourseSection(panelGo.transform);

            SetVisible(false);
        }

        private void BuildCourseSection(Transform parent)
        {
            AddLabel(parent, "COURSE", 0f, -388f, UiColors.TextSecondary, 12);

            _autoBg = AddModeButton(parent, "AUTO", -210f, -410f, 115f, 24f, CourseMode.Auto);
            _manualBg = AddModeButton(parent, "MANUAL", -80f, -410f, 115f, 24f, CourseMode.Manual);

            _crsReadout = AddBigReadout(parent, -448f);

            _toFrom = AddLabel(parent, "TO", 0f, -484f, UiColors.HudAmber, 16);

            AddButton(parent, "-5", -150f, -514f, 70f, 26f, () => CourseAdjusted?.Invoke(-5f));
            AddButton(parent, "-1", -75f, -514f, 70f, 26f, () => CourseAdjusted?.Invoke(-1f));
            AddButton(parent, "+1", 0f, -514f, 70f, 26f, () => CourseAdjusted?.Invoke(1f));
            AddButton(parent, "+5", 75f, -514f, 70f, 26f, () => CourseAdjusted?.Invoke(5f));

            AddButton(parent, "SET BRG", -82f, -548f, 75f, 26f, () => SetCourseToBearing?.Invoke());
            AddButton(parent, "SET HDG", 7f, -548f, 75f, 26f, () => SetCourseToHeading?.Invoke());
        }

        public void SetCourse(CourseMode mode, float course, bool toStation)
        {
            if (_crsReadout != null)
                _crsReadout.text = $"CRS {Mathf.RoundToInt(course):000}\u00b0";
            if (_toFrom != null)
                _toFrom.text = mode == CourseMode.Manual ? (toStation ? "TO" : "FROM") : "TO";
            if (_autoBg != null)
                _autoBg.color = mode == CourseMode.Auto ? UiColors.HudGreenDim : UiColors.BgPanelRaised;
            if (_manualBg != null)
                _manualBg.color = mode == CourseMode.Manual ? UiColors.HudGreenDim : UiColors.BgPanelRaised;
        }

        public void Toggle()
        {
            SetVisible(!_visible);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_root != null) _root.SetActive(visible);

            if (visible)
            {
                _prevLockState = Cursor.lockState;
                _prevCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = _prevLockState;
                Cursor.visible = _prevCursorVisible;
            }
        }

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
        }

        private Image AddModeButton(Transform parent, string text, float x, float y, float w, float h, CourseMode mode)
        {
            var go = new GameObject("Mode_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var img = go.GetComponent<Image>();
            img.color = UiColors.BgPanelRaised;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = UiColors.BorderPanel;
            colors.pressedColor = UiColors.HudGreenDim;
            btn.colors = colors;
            btn.onClick.AddListener(new UnityAction(() => ModeChanged?.Invoke(mode)));

            var tmpGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            tmpGo.transform.SetParent(go.transform, false);
            var tmpRt = tmpGo.GetComponent<RectTransform>();
            tmpRt.anchorMin = Vector2.zero;
            tmpRt.anchorMax = Vector2.one;
            tmpRt.offsetMin = Vector2.zero;
            tmpRt.offsetMax = Vector2.zero;
            var tmp = tmpGo.GetComponent<TextMeshProUGUI>();
            tmp.font = FontLoader.GetDefaultFont();
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UiColors.TextPrimary;
            tmp.text = text;
            return img;
        }

        private void AddButton(Transform parent, string text, float x, float y, float w, float h, UnityAction onClick)
        {
            var go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            go.GetComponent<Image>().color = UiColors.BgPanelRaised;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = UiColors.BorderPanel;
            colors.pressedColor = UiColors.HudGreenDim;
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var tmpGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            tmpGo.transform.SetParent(go.transform, false);
            var tmpRt = tmpGo.GetComponent<RectTransform>();
            tmpRt.anchorMin = Vector2.zero;
            tmpRt.anchorMax = Vector2.one;
            tmpRt.offsetMin = Vector2.zero;
            tmpRt.offsetMax = Vector2.zero;
            var tmp = tmpGo.GetComponent<TextMeshProUGUI>();
            tmp.font = FontLoader.GetDefaultFont();
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = UiColors.TextPrimary;
            tmp.text = text;
        }

        private TextMeshProUGUI AddLabel(Transform parent, string text, float x, float y, Color color, int fontSize = 11)
        {
            var go = new GameObject("Label_" + text, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(300, 20);
            rt.anchoredPosition = new Vector2(x, y);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = FontLoader.GetDefaultFont();
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }

        private TextMeshProUGUI AddBigReadout(Transform parent, float y)
        {
            var go = new GameObject("Readout", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1);
            rt.anchorMax = new Vector2(0.5f, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.sizeDelta = new Vector2(300, 32);
            rt.anchoredPosition = new Vector2(0f, y);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = FontLoader.GetDefaultFont();
            tmp.fontSize = 26;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = UiColors.HudGreen;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = "CRS 000\u00b0";
            return tmp;
        }
    }
}
```

- [ ] **Step 3: Wire the panel into `Core/NavController.cs`**

3a. Add a field and create/destroy the panel. Replace the `Awake()` method and the `OnDestroy()` method with:

```csharp
        private void Awake()
        {
            _mode = Plugin.CourseModeManual.Value ? CourseMode.Manual : CourseMode.Auto;
            _manualCourse = Mathf.Repeat(Plugin.DefaultManualCourse.Value, 360f);

            _panel = new NavPanel();
            _panel.Create();
            _panel.AirportSelected += i => _selectedIndex = i;
            _panel.ModeChanged += m => _mode = m;
            _panel.CourseAdjusted += AdjustCourse;
            _panel.SetCourseToBearing += () => SetManualCourse(Data.Bearing);
            _panel.SetCourseToHeading += () => SetManualCourse(Data.Heading);
            _panel.SetVisible(false);
        }
```

Add the field alongside `_instrument`:

```csharp
        private NavPanel _panel;
```

And add a helper plus updated `OnDestroy`:

```csharp
        private void SetManualCourse(float value)
        {
            _manualCourse = Mathf.Repeat(value, 360f);
            _mode = CourseMode.Manual;
        }
```

```csharp
        private void OnDestroy()
        {
            if (_instrument != null) Destroy(_instrument.gameObject);
            if (_panel != null) _panel.Destroy();
        }
```

3b. Replace the `HandleInput()` menu line and remove the IMGUI menu. Change:

```csharp
            if (Plugin.ToggleMenuKey.Value.IsDown()) _menuVisible = !_menuVisible;
```

to:

```csharp
            if (Plugin.ToggleMenuKey.Value.IsDown()) _panel?.Toggle();
```

Delete the `_menuVisible` field, the `OnGUI()` method, and the `DrawMenu(int id)` method entirely.

3c. Feed course data to the panel every frame. In `Update()`, after the `_instrument?.SetData(...)` call add:

```csharp
            _panel?.SetCourse(Data.Mode, Data.Course, Data.ToStation);
```

- [ ] **Step 4: Verify build + in-game**

Run: `dotnet build -c Debug`
Expected: `Build succeeded. 0 Error(s)`. In-game:
1. Press `F9` → panel opens on the left side of the screen; cursor unlocks; drag by the title bar.
2. `AUTO` mode button is highlighted; click `MANUAL` → manual highlight.
3. `+1`/`+5`/`-1`/`-5` change the big CRS readout and the HUD instrument's CRS/DEV; `[`/`]` still work.
4. `SET BRG` / `SET HDG` snap the course to the bearing/heading.
5. Press `F9` again → panel closes, cursor re-locks.

- [ ] **Step 5: Commit**

```bash
git add UI/ Core/NavController.cs
git commit -m "feat: SITREP-style nav panel with course controls, replaces IMGUI menu"
```

---

### Task 4: Airport List in the Panel

Add a scrollable airport list to the panel with live bearing/distance, click-to-select, and selection highlighting. Wire it to `NavController`.

**Files:**
- Modify: `UI/NavPanel.cs` (airport section: scroll area + rows, `SetAirports`, `RefreshSelection`, `RefreshMeta`)
- Modify: `Core/NavController.cs` (`UpdatePanel`, call `SetAirports` per frame)

**Interfaces:**
- Consumes: `AirportInfo` (Task 2), `NavPanel` events (Task 3).
- Produces: `NavPanel.SetAirports(IReadOnlyList<AirportInfo> airports, int selectedIndex)` — rebuilds rows only when the row count changes, recolors selection, and refreshes meta text each call.

- [ ] **Step 1: Add the airport list to `UI/NavPanel.cs`**

1a. Add a `RectTransform _contentRt;` field and a `BuildAirportSection` call. In `Create()`, replace:

```csharp
            BuildCourseSection(panelGo.transform);
```

with:

```csharp
            BuildAirportSection(panelGo.transform);
            BuildCourseSection(panelGo.transform);
```

1b. Add this method (scroll area + header) to the class:

```csharp
        private void BuildAirportSection(Transform parent)
        {
            AddLabel(parent, "AIRPORTS", 0f, -46f, UiColors.TextSecondary, 12);

            var scrollGo = new GameObject("AirportScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 1);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.pivot = new Vector2(0, 1);
            scrollRt.sizeDelta = new Vector2(PanelWidth - 16, 300);
            scrollRt.anchoredPosition = new Vector2(8, -64);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewportGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _contentRt = contentGo.GetComponent<RectTransform>();
            _contentRt.anchorMin = new Vector2(0, 1);
            _contentRt.anchorMax = new Vector2(1, 1);
            _contentRt.pivot = new Vector2(0, 1);
            _contentRt.sizeDelta = new Vector2(0, 10);

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.content = _contentRt;
            scrollRect.viewport = vpRt;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
        }
```

1c. Add the public `SetAirports` method and the row builders. Insert these after `SetCourse`:

```csharp
        public void SetAirports(IReadOnlyList<AirportInfo> airports, int selectedIndex)
        {
            if (airports.Count != _airportRows.Count)
                RebuildRows(airports, selectedIndex);
            else
                RefreshSelection(selectedIndex);
            RefreshMeta(airports);
        }

        private void RebuildRows(IReadOnlyList<AirportInfo> airports, int selectedIndex)
        {
            foreach (var row in _airportRows)
                UnityEngine.Object.Destroy(row);
            _airportRows.Clear();
            _rowBg.Clear();
            _rowName.Clear();
            _rowMeta.Clear();

            _contentRt.sizeDelta = new Vector2(0, Mathf.Max(airports.Count * 24 + 8, 10));
            for (int i = 0; i < airports.Count; i++)
                AddAirportRow(i, airports[i], selectedIndex);
        }

        private void RefreshSelection(int selectedIndex)
        {
            for (int i = 0; i < _rowBg.Count; i++)
            {
                bool sel = i == selectedIndex;
                _rowBg[i].color = sel ? UiColors.HudGreenDim : UiColors.BgPanelRaised;
                _rowName[i].color = sel ? UiColors.TextPrimary : UiColors.TextSecondary;
            }
        }

        private void RefreshMeta(IReadOnlyList<AirportInfo> airports)
        {
            for (int i = 0; i < _rowMeta.Count && i < airports.Count; i++)
            {
                var info = airports[i];
                _rowMeta[i].text = info.HasPosition
                    ? $"BRG {Mathf.RoundToInt(info.Bearing):000}\u00b0  {info.DistanceKm:F1}km"
                    : "";
            }
        }

        private void AddAirportRow(int index, AirportInfo info, int selectedIndex)
        {
            var go = new GameObject("Row" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_contentRt.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(PanelWidth - 32, 22);
            rt.anchoredPosition = new Vector2(4, -index * 24 - 4);

            var img = go.GetComponent<Image>();
            img.color = index == selectedIndex ? UiColors.HudGreenDim : UiColors.BgPanelRaised;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = UiColors.BorderPanel;
            colors.pressedColor = UiColors.HudGreenDim;
            btn.colors = colors;
            int idx = index;
            btn.onClick.AddListener(new UnityAction(() => AirportSelected?.Invoke(idx)));

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(go.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = Vector2.zero;
            nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = new Vector2(8, 0);
            nameRt.offsetMax = new Vector2(-120, 0);
            var nameTmp = nameGo.GetComponent<TextMeshProUGUI>();
            nameTmp.font = FontLoader.GetDefaultFont();
            nameTmp.fontSize = 12;
            nameTmp.color = index == selectedIndex ? UiColors.TextPrimary : UiColors.TextSecondary;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.text = info.Name;

            var metaGo = new GameObject("Meta", typeof(RectTransform), typeof(TextMeshProUGUI));
            metaGo.transform.SetParent(go.transform, false);
            var metaRt = metaGo.GetComponent<RectTransform>();
            metaRt.anchorMin = Vector2.zero;
            metaRt.anchorMax = Vector2.one;
            metaRt.offsetMin = new Vector2(-116, 0);
            metaRt.offsetMax = new Vector2(-8, 0);
            var metaTmp = metaGo.GetComponent<TextMeshProUGUI>();
            metaTmp.font = FontLoader.GetDefaultFont();
            metaTmp.fontSize = 11;
            metaTmp.color = UiColors.TextSecondary;
            metaTmp.alignment = TextAlignmentOptions.Right;
            metaTmp.text = "";

            _airportRows.Add(go);
            _rowBg.Add(img);
            _rowName.Add(nameTmp);
            _rowMeta.Add(metaTmp);
        }
```

- [ ] **Step 2: Feed the airport list from `Core/NavController.cs`**

2a. Add this method to `NavController`:

```csharp
        private void UpdatePanel(bool hasAircraft)
        {
            if (_panel == null) return;

            var infos = new List<AirportInfo>(_airbases.Count);
            Vector3? pos = hasAircraft && _aircraft != null && _aircraft.rb != null
                ? (Vector3?)_aircraft.rb.transform.position
                : null;

            for (int i = 0; i < _airbases.Count; i++)
            {
                var ab = _airbases[i];
                var info = new AirportInfo { Name = ab.name, HasPosition = pos.HasValue };
                if (pos.HasValue && ab.center != null)
                {
                    var to = ab.center.position - pos.Value;
                    float brg = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    if (brg < 0f) brg += 360f;
                    info.Bearing = brg;
                    info.DistanceKm = new Vector2(to.x, to.z).magnitude / 1000f;
                }
                infos.Add(info);
            }

            _panel.SetAirports(infos, _selectedIndex);
        }
```

2b. In `Update()`, replace the early-return block so the panel still updates when not in an aircraft. Replace:

```csharp
            if (!GameManager.GetLocalAircraft(out _aircraft))
            {
                SetInstrumentVisible(false);
                return;
            }

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = AirbaseRefreshInterval;
                RefreshAirbases();
            }

            if (!HasSelection)
            {
                SetInstrumentVisible(false);
                return;
            }
```

with:

```csharp
            bool hasAircraft = GameManager.GetLocalAircraft(out _aircraft);

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = AirbaseRefreshInterval;
                RefreshAirbases();
            }

            if (hasAircraft && HasSelection)
            {
                UpdateData();
                EnsureInstrument();
                SetInstrumentVisible(_hudVisible);
                _instrument?.SetData(Data, _selectedIndex, _airbases.Count);
                _panel?.SetCourse(Data.Mode, Data.Course, Data.ToStation);
            }
            else
            {
                SetInstrumentVisible(false);
            }

            UpdatePanel(hasAircraft);
```

Also remove the now-duplicated `UpdateData(); EnsureInstrument(); ...` lines and the `_panel?.SetCourse(...)` line added in Task 3 Step 3c, since they are folded into the new block above.

- [ ] **Step 3: Verify build + in-game**

Run: `dotnet build -c Debug`
Expected: `Build succeeded. 0 Error(s)`. In-game:
1. `F9` opens the panel; the AIRPORTS list shows every airport with live `BRG`/distance, and the current selection is highlighted green.
2. Clicking a row selects it → HUD instrument switches to that airport; highlight follows.
3. `N`/`B` cycling still works and the panel highlight follows.
4. Open the panel on the main menu (not flying) → list still shows airports, no `BRG` column, no crash.

- [ ] **Step 4: Commit**

```bash
git add UI/NavPanel.cs Core/NavController.cs
git commit -m "feat: scrollable airport list in nav panel with live bearing/distance"
```

---

### Task 5: Final Verification Pass

Confirm hot reload, cleanup, and config regeneration all behave after the new UI.

**Files:**
- Modify: none (verification only), unless fixes are found.

**Interfaces:**
- Consumes: everything from Tasks 1–4.

- [ ] **Step 1: Double hot reload**

Run: `dotnet build -c Debug` twice without touching the game.
Expected: each run prints `Deployed NOVor.dll to ...\BepInEx\scripts`; the game reloads via the file watcher (~3 s). After the second reload the panel still opens (F9) and the HUD still works — no duplicate panels, no stale HUD instruments. Confirm the log has one `NOVor v1.0.0 loaded.` per reload and no `NOVor duplicate instance detected` warnings.

- [ ] **Step 2: Cleanup on scene change**

In-game: close the panel, eject/land so the HUD scene changes, re-enter an aircraft.
Expected: instrument re-parents and re-appears; panel persists (DontDestroyOnLoad) and still opens.

- [ ] **Step 3: Config regeneration**

Delete `BepInEx/config/com.novor.cdi.cfg`, restart the game.
Expected: the cfg regenerates with the new keys `CourseDecrease`, `CourseIncrease`, `ManualCourseByDefault`, `DefaultManualCourse`, `CourseStep`, and the mod works with defaults.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: final verification for nav panel UI"
```

---

## Self-Review

**Spec coverage:** airport selection UI (Task 4), course selection/CRS UI (Task 3), manual course math + TO/FROM (Task 2), SITREP visual style — canvas, draggable bordered panel, scanlines, TMP, HUD-green palette (Tasks 1, 3). All covered.

**Placeholder scan:** every step has full code or an exact edit with code; no "TBD"/"later" steps.

**Type consistency:** `CourseMode` defined in Task 2 and used in `CdiData`, `NavPanel` events, and `NavController`; `AirportInfo` defined in Task 2 and consumed by `NavPanel.SetAirports`; `NavPanel` events (`AirportSelected(int)`, `ModeChanged(CourseMode)`, `CourseAdjusted(float)`, `SetCourseToBearing`, `SetCourseToHeading`) match the wiring in `NavController.Awake` (Task 3) exactly. `SetAirports(IReadOnlyList<AirportInfo>, int)` matches Task 4 usage.
