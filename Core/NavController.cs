using System.Collections.Generic;
using UnityEngine;
using NOVor.Core;
using NOVor.UI;
using NOVor.Integrations;

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
        private CdiInstrument _instrument;
        private NavPanel _panel;
        private CourseMode _mode = CourseMode.Auto;
        private float _manualCourse;
        private List<AirportInfo> _lastInfos;

        public CdiData Data { get; private set; } = new CdiData();

        public bool HasSelection => _selectedIndex >= 0 && _selectedIndex < _airbases.Count;

        private void Awake()
        {
            _mode = Plugin.CourseModeManual.Value ? CourseMode.Manual : CourseMode.Auto;
            _manualCourse = Mathf.Repeat(Plugin.DefaultManualCourse.Value, 360f);

            _panel = new NavPanel();
            _panel.Create();
            _panel.AirportSelected += i => _selectedIndex = i;
            _panel.ModeChanged += m => _mode = m;
            _panel.CourseAdjusted += AdjustCourse;
            _panel.CourseSet += SetManualCourse;
            _panel.CourseFlipToFrom += () => SetManualCourse(_manualCourse + 180f);
            _panel.NearestRequested += SelectNearest;
            _panel.SetCourseToBearing += () => SetManualCourse(Data.Bearing);
            _panel.SetCourseToHeading += () => SetManualCourse(Data.Heading);
            _panel.SetVisible(false);
            ModBarBridge.Register("no.vor", "VOR", "NO-VOR Nav Panel", () => _panel.IsVisible, () => _panel.Toggle());
        }

        private void Update()
        {
            HandleInput();

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
                _panel?.SetCourse(Data.Mode, Data.Course, Data.ToStation, Data.AirportName,
                    Data.Bearing, Data.DistanceKm);
            }
            else
            {
                SetInstrumentVisible(false);
            }

            UpdatePanel(hasAircraft);
        }

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
                var info = new AirportInfo
                {
                    Name = CleanName(ab.name),
                    HasPosition = pos.HasValue,
                    SourceIndex = i
                };
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

            // Nearest first; entries without a known position go last (in source order).
            infos.Sort((a, b) =>
            {
                if (a.HasPosition != b.HasPosition) return a.HasPosition ? -1 : 1;
                if (a.HasPosition) return a.DistanceKm.CompareTo(b.DistanceKm);
                return a.SourceIndex.CompareTo(b.SourceIndex);
            });

            // Disambiguate repeated names with a numeric suffix in distance order.
            var nameCounts = new Dictionary<string, int>();
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                string baseName = info.Name;
                nameCounts.TryGetValue(baseName, out int count);
                nameCounts[baseName] = count + 1;
                if (count > 0)
                {
                    info.Name = baseName + " " + (count + 1);
                    infos[i] = info;
                }
            }

            _lastInfos = infos;
            _panel.SetAirports(infos, _selectedIndex);
        }

        private void SelectNearest()
        {
            if (_lastInfos == null) return;
            for (int i = 0; i < _lastInfos.Count; i++)
            {
                if (!_lastInfos[i].HasPosition) continue;
                _selectedIndex = _lastInfos[i].SourceIndex;
                return;
            }
        }

        private static string CleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw ?? "";
            return raw.Replace("(Clone)", "").Trim();
        }

        private void HandleInput()
        {
            if (Plugin.NextAirportKey.Value.IsDown()) CycleAirport(1);
            if (Plugin.PrevAirportKey.Value.IsDown()) CycleAirport(-1);
            if (Plugin.ToggleHudKey.Value.IsDown()) _hudVisible = !_hudVisible;
            if (Plugin.ToggleMenuKey.Value.IsDown()) _panel?.Toggle();
            if (Plugin.CourseDecreaseKey.Value.IsDown()) AdjustCourse(-Plugin.CourseStep.Value);
            if (Plugin.CourseIncreaseKey.Value.IsDown()) AdjustCourse(Plugin.CourseStep.Value);

            float step = Plugin.HudNudgeStep.Value;
            if (Plugin.HudNudgeUpKey.Value.IsDown()) NudgeInstrument(0f, step);
            if (Plugin.HudNudgeDownKey.Value.IsDown()) NudgeInstrument(0f, -step);
            if (Plugin.HudNudgeLeftKey.Value.IsDown()) NudgeInstrument(-step, 0f);
            if (Plugin.HudNudgeRightKey.Value.IsDown()) NudgeInstrument(step, 0f);
        }

        private void NudgeInstrument(float dx, float dy)
        {
            Plugin.HudOffsetX.Value = Mathf.Clamp(Plugin.HudOffsetX.Value + dx, -800f, 800f);
            Plugin.HudOffsetY.Value = Mathf.Clamp(Plugin.HudOffsetY.Value + dy, -800f, 800f);
            _instrument?.ApplyOffsets(Plugin.HudOffsetX.Value, Plugin.HudOffsetY.Value);
        }

        private void AdjustCourse(float delta)
        {
            _manualCourse = Mathf.Repeat(_manualCourse + delta, 360f);
            _mode = CourseMode.Manual;
        }

        private void SetManualCourse(float value)
        {
            _manualCourse = Mathf.Repeat(value, 360f);
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
            Data.AirportName = CleanName(target.name);
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

        private void OnDestroy()
        {
            ModBarBridge.Unregister("no.vor");
            if (_instrument != null) Destroy(_instrument.gameObject);
            if (_panel != null) _panel.Destroy();
        }
    }
}
