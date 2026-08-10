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
