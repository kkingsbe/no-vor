using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using NOVor.Core;
using NOVor.UI;
using NOVor.Integrations;

namespace NOVor
{
    [DefaultExecutionOrder(1000)]
    public class NavController : MonoBehaviour
    {
        private const float AirbaseRefreshInterval = 1f;
        private static readonly Color FallbackFactionColor = new Color(0.35f, 0.37f, 0.36f, 1f);

        private readonly List<Airbase> _airbases = new List<Airbase>();
        private float _refreshTimer;
        private int _selectedIndex = -1;
        private Aircraft _aircraft;
        private bool _hudVisible = true;
        private CdiInstrument _instrument;
        private NavPanel _panel;
        private CourseMode _mode = CourseMode.Auto;
        private float _manualCourse;

        private CameraStateManager _camManager;
        private bool _camInputsOverridden;
        private bool _cachedAllowInputs;
        private float _cachedFovChangeSpeed;
        private float _cachedFovChangeInertia;
        private float _lockedFov;
        private bool _fovLocked;
        private readonly Dictionary<FieldInfo, object> _cachedCameraStateValues =
            new Dictionary<FieldInfo, object>();

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
            _panel.SetReciprocalCourse += () => SetManualCourse(_manualCourse + 180f);
            _panel.SetCourseToBearing += () => SetManualCourse(Data.Bearing);
            _panel.SetCourseToHeading += () => SetManualCourse(Data.Heading);
            _panel.RunwaySelected += (index, heading) => SetManualCourse(heading);
            _panel.SetVisible(false);
            ModBarBridge.Register("no.vor", "NAV", "Navigation CDI", () => _panel.IsVisible, () => _panel.Toggle());
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
                _panel?.SetNavigation(Data);
            }
            else
            {
                SetInstrumentVisible(false);
            }

            UpdatePanel(hasAircraft);
            BlockCameraInputs();
        }

        private void LateUpdate()
        {
            if (_panel == null || !_panel.IsVisible) return;
            var cam = _camManager != null ? _camManager.mainCamera : Camera.main;
            if (cam == null) return;
            if (!_fovLocked)
            {
                _lockedFov = cam.fieldOfView;
                _fovLocked = true;
            }
            cam.fieldOfView = _lockedFov;
        }

        private void UpdatePanel(bool hasAircraft)
        {
            if (_panel == null) return;

            var infos = new List<AirportInfo>(_airbases.Count);
            var rb = hasAircraft && _aircraft != null ? _aircraft.rb : null;
            Vector3? pos = rb != null ? (Vector3?)rb.transform.position : null;
            float heading = rb != null ? Mathf.Repeat(rb.transform.eulerAngles.y, 360f) : 0f;
            float horizontalSpeed = rb != null ? new Vector2(rb.velocity.x, rb.velocity.z).magnitude : 0f;
            float groundTrack = horizontalSpeed > 2f
                ? Mathf.Repeat(Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg, 360f)
                : heading;
            Faction localFaction = null;
            if (_aircraft != null && _aircraft.Player != null && _aircraft.Player.HQ != null)
                localFaction = _aircraft.Player.HQ.faction;

            for (int i = 0; i < _airbases.Count; i++)
            {
                var ab = _airbases[i];
                var faction = ab.CurrentHQ != null ? ab.CurrentHQ.faction : null;
                var info = new AirportInfo
                {
                    Name = GetAirbaseName(ab),
                    HasPosition = pos.HasValue,
                    SourceIndex = i,
                    Runways = GetRunways(ab),
                    ElevationMeters = GetAirbaseElevation(ab),
                    IsMobile = ab.AttachedAirbase,
                    HasFaction = faction != null,
                    IsFriendly = faction != null && localFaction != null && faction == localFaction,
                    FactionColor = faction != null ? faction.color : FallbackFactionColor,
                    FactionTag = faction != null ? faction.factionTag : "---"
                };
                if (pos.HasValue && ab.center != null)
                {
                    var to = ab.center.position - pos.Value;
                    float brg = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    if (brg < 0f) brg += 360f;
                    var horizontal = new Vector3(to.x, 0f, to.z);
                    float closure = horizontal.sqrMagnitude > 1f && rb != null
                        ? Vector3.Dot(rb.velocity - GetAirbaseVelocity(ab), horizontal.normalized)
                        : 0f;
                    double eta = NavMath.EtaSeconds(horizontal.magnitude, closure);
                    info.Bearing = brg;
                    info.DistanceNm = horizontal.magnitude / 1852f;
                    info.SteerHeading = (float)NavMath.DriftCorrectedHeadingDegrees(brg, heading, groundTrack);
                    info.GroundSpeedKnots = horizontalSpeed * 1.9438445f;
                    info.HasEta = !double.IsNaN(eta) && !double.IsInfinity(eta);
                    info.EtaSeconds = info.HasEta ? (float)eta : 0f;
                }
                infos.Add(info);
            }

            infos.Sort((a, b) =>
            {
                if (a.HasPosition != b.HasPosition) return a.HasPosition ? -1 : 1;
                if (a.HasPosition) return a.DistanceNm.CompareTo(b.DistanceNm);
                return a.SourceIndex.CompareTo(b.SourceIndex);
            });

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

            _panel.SetAirports(infos, _selectedIndex);
        }

        private static string GetAirbaseName(Airbase ab)
        {
            var saved = ab.SavedAirbase;
            if (saved != null && !string.IsNullOrEmpty(saved.DisplayName))
                return saved.DisplayName;
            return CleanName(ab.name);
        }

        private static RunwayInfo[] GetRunways(Airbase airbase)
        {
            if (airbase.runways == null || airbase.runways.Length == 0)
                return null;

            var result = new List<RunwayInfo>();
            foreach (var runway in airbase.runways)
            {
                if (runway == null || runway.Start == null || runway.End == null)
                    continue;

                AddRunwayDirection(result, runway, false);
                if (runway.Reversable)
                    AddRunwayDirection(result, runway, true);
            }
            return result.Count > 0 ? result.ToArray() : null;
        }

        private static void AddRunwayDirection(List<RunwayInfo> result, Airbase.Runway runway, bool reverse)
        {
            Vector3 direction = runway.GetDirection(reverse);
            float heading = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);
            string label = runway.GetName(reverse);
            if (label.StartsWith("Runway "))
                label = "RWY " + label.Substring(7);
            result.Add(new RunwayInfo
            {
                Label = label,
                Heading = heading,
                LengthMeters = runway.Length
            });
        }

        private static Vector3 GetAirbaseVelocity(Airbase airbase)
        {
            if (airbase == null || airbase.runways == null) return Vector3.zero;
            for (int i = 0; i < airbase.runways.Length; i++)
            {
                var runway = airbase.runways[i];
                if (runway != null) return runway.GetVelocity();
            }
            return Vector3.zero;
        }

        private static float GetAirbaseElevation(Airbase airbase)
        {
            if (airbase == null) return 0f;
            var saved = airbase.SavedAirbase;
            if (saved != null) return saved.Center.y;
            return airbase.center != null ? airbase.center.position.y : 0f;
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
            var horizontal = new Vector3(to.x, 0f, to.z);
            float distance = horizontal.magnitude;
            float horizontalSpeed = new Vector2(rb.velocity.x, rb.velocity.z).magnitude;
            float groundTrack = horizontalSpeed > 2f
                ? Mathf.Repeat(Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg, 360f)
                : heading;
            float closure = horizontal.sqrMagnitude > 1f
                ? Vector3.Dot(rb.velocity - GetAirbaseVelocity(target), horizontal.normalized)
                : 0f;
            double eta = NavMath.EtaSeconds(distance, closure);

            Data.Heading = heading;
            Data.GroundTrack = groundTrack;
            Data.Bearing = bearing;
            Data.DistanceNm = distance / 1852f;
            Data.AirportName = GetAirbaseName(target);
            Data.Mode = _mode;
            Data.Course = _mode == CourseMode.Manual ? _manualCourse : bearing;
            Data.ToStation = NavMath.IsToStation(Data.Course, bearing);
            Data.SteerHeading = (float)NavMath.DriftCorrectedHeadingDegrees(bearing, heading, groundTrack);
            Data.Deviation = _mode == CourseMode.Manual
                ? (float)NavMath.CourseDeviationDegrees(Data.Course, bearing)
                : (float)NavMath.SteeringDeviationDegrees(heading, Data.SteerHeading);
            Data.Deflection = Mathf.Clamp(Data.Deviation / Plugin.FullDeflectionDeg.Value, -1f, 1f);
            Data.GroundSpeedKnots = horizontalSpeed * 1.9438445f;
            Data.HasEta = !double.IsNaN(eta) && !double.IsInfinity(eta);
            Data.EtaSeconds = Data.HasEta ? (float)eta : 0f;
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

        private void BlockCameraInputs()
        {
            bool panelVisible = _panel != null && _panel.IsVisible;
            if (panelVisible)
            {
                if (_camManager == null)
                    _camManager = Object.FindObjectOfType<CameraStateManager>();
                if (_camManager == null) return;
                if (!_camInputsOverridden)
                {
                    _cachedAllowInputs = _camManager.allowInputs;
                    _cachedFovChangeSpeed = _camManager.fovChangeSpeed;
                    _cachedFovChangeInertia = _camManager.fovChangeInertia;
                    _camInputsOverridden = true;
                }
                _camManager.allowInputs = false;
                _camManager.fovChangeSpeed = 0f;
                _camManager.fovChangeInertia = 0f;
                var state = _camManager.currentState;
                if (state != null)
                {
                    var type = state.GetType();
                    OverrideCameraStateField(state, type, "zoomSpeed");
                    OverrideCameraStateField(state, type, "FOVAdjustment");
                    OverrideCameraStateField(state, type, "viewDistAdjust");
                }
            }
            else
            {
                RestoreCameraInputs();
                _fovLocked = false;
            }
        }

        private void RestoreCameraInputs()
        {
            if (_camManager != null && _camInputsOverridden)
            {
                _camManager.allowInputs = _cachedAllowInputs;
                _camManager.fovChangeSpeed = _cachedFovChangeSpeed;
                _camManager.fovChangeInertia = _cachedFovChangeInertia;
                _camInputsOverridden = false;
            }
            foreach (var pair in _cachedCameraStateValues)
            {
                try
                {
                    var state = _camManager != null ? _camManager.currentState : null;
                    if (state != null && pair.Key.DeclaringType.IsInstanceOfType(state))
                        pair.Key.SetValue(state, pair.Value);
                }
                catch
                {
                }
            }
            _cachedCameraStateValues.Clear();
        }

        private void OverrideCameraStateField(object state, System.Type type, string name)
        {
            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || field.FieldType != typeof(float)) return;
            if (!_cachedCameraStateValues.ContainsKey(field))
                _cachedCameraStateValues[field] = field.GetValue(state);
            field.SetValue(state, 0f);
        }

        private void OnDestroy()
        {
            RestoreCameraInputs();
            ModBarBridge.Unregister("no.vor");
            if (_instrument != null) Destroy(_instrument.gameObject);
            if (_panel != null) _panel.Destroy();
        }
    }
}
