using System;
using System.Collections.Generic;
using System.Text;
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
        private const float PanelWidth = 460f;
        private const float HeaderHeight = 38f;
        private const float RowHeight = 30f;
        private const float RowSpacing = 3f;
        private const float ListHeight = 250f;
        private const float ControlHeight = 26f;

        private GameObject _root;
        private RectTransform _panelRt;
        private GameObject _body;
        private RectTransform _contentRt;
        private ScrollRect _scrollRect;
        private GameObject _fadeTop;
        private GameObject _fadeBottom;

        private TextMeshProUGUI _headerReadout;
        private TextMeshProUGUI _minimizeLabel;
        private TMP_InputField _searchInput;
        private TMP_InputField _crsInput;
        private TextMeshProUGUI _targetLabel;
        private Button _autoBtn;
        private TextMeshProUGUI _autoText;
        private Button _manualBtn;
        private TextMeshProUGUI _manualText;
        private Button _toFromBtn;

        private readonly List<GameObject> _airportRows = new List<GameObject>();
        private readonly List<Image> _rowBg = new List<Image>();
        private readonly List<Button> _rowBtns = new List<Button>();
        private readonly List<TextMeshProUGUI> _rowName = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> _rowMeta = new List<TextMeshProUGUI>();
        private readonly List<int> _rowSourceIndex = new List<int>();
        private readonly List<bool> _rowSelected = new List<bool>();

        private IReadOnlyList<AirportInfo> _airports;
        private string _filter = "";
        private string _rowSignature = "";
        private string _headerText = "";
        private string _targetText = "";
        private int _selectedSourceIndex = -1;
        private bool _minimized;

        private CursorLockMode _prevLockState;
        private bool _prevCursorVisible;
        private bool _visible = true;

        public event Action<int> AirportSelected;
        public event Action<CourseMode> ModeChanged;
        public event Action<float> CourseAdjusted;
        public event Action<float> CourseSet;
        public event Action CourseFlipToFrom;
        public event Action NearestRequested;
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

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(RawImage),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            panelGo.transform.SetParent(_root.transform, false);
            _panelRt = panelGo.GetComponent<RectTransform>();
            _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRt.pivot = new Vector2(0.5f, 0.5f);
            _panelRt.sizeDelta = new Vector2(PanelWidth, 500f);
            _panelRt.anchoredPosition = new Vector2(Plugin.PanelX.Value, Plugin.PanelY.Value);
            var panelImage = panelGo.GetComponent<RawImage>();
            panelImage.texture = TextureFactory.CreatePanelBackground(64, 64, UiColors.BgPanel, UiColors.BorderPanel, 2f);
            panelImage.color = Color.white;

            var panelVlg = panelGo.GetComponent<VerticalLayoutGroup>();
            panelVlg.padding = new RectOffset(10, 10, 10, 12);
            panelVlg.spacing = 8f;
            panelVlg.childAlignment = TextAnchor.UpperCenter;
            panelVlg.childControlWidth = true;
            panelVlg.childControlHeight = true;
            panelVlg.childForceExpandWidth = true;
            panelVlg.childForceExpandHeight = false;
            var panelFitter = panelGo.GetComponent<ContentSizeFitter>();
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            BuildHeader(panelGo.transform, rootRt);
            BuildBody(panelGo.transform);

            SetVisible(false);
        }

        private void BuildHeader(Transform parent, RectTransform canvasRt)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement), typeof(WindowDragHandler));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = UiColors.BgPanelRaised;
            go.GetComponent<LayoutElement>().preferredHeight = HeaderHeight;
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(12, 6, 0, 0);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            go.GetComponent<WindowDragHandler>().Init(_panelRt, canvasRt, OnDragEnded);

            var title = MakeText(go.transform, "Title", "NO-VOR NAV", 15, FontStyles.Bold,
                UiColors.HudGreen, TextAlignmentOptions.MidlineLeft);
            title.enableWordWrapping = false;
            var titleLe = title.gameObject.AddComponent<LayoutElement>();
            titleLe.preferredWidth = 130f;

            _headerReadout = MakeText(go.transform, "Readout", "", 12, FontStyles.Normal,
                UiColors.TextSecondary, TextAlignmentOptions.MidlineRight);
            _headerReadout.enableWordWrapping = false;
            var readoutLe = _headerReadout.gameObject.AddComponent<LayoutElement>();
            readoutLe.flexibleWidth = 1f;
            _headerReadout.overflowMode = TextOverflowModes.Ellipsis;

            var minBtn = MakeButton(go.transform, "_", 28f, ControlHeight, ToggleMinimized);
            _minimizeLabel = minBtn.GetComponentInChildren<TextMeshProUGUI>();
            var closeBtn = MakeButton(go.transform, "X", 28f, ControlHeight, () => SetVisible(false));
            closeBtn.GetComponentInChildren<TextMeshProUGUI>().color = UiColors.TextSecondary;
        }

        private void BuildBody(Transform parent)
        {
            _body = new GameObject("Body", typeof(RectTransform), typeof(VerticalLayoutGroup));
            _body.transform.SetParent(parent, false);
            var vlg = _body.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            BuildSearchRow(_body.transform);
            BuildAirportList(_body.transform);
            BuildCourseDeck(_body.transform);
        }

        private void BuildSearchRow(Transform parent)
        {
            var go = new GameObject("SearchRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 30f;
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            _searchInput = MakeInput(go.transform, "SEARCH AIRPORTS", 12, false);
            var inputLe = _searchInput.gameObject.AddComponent<LayoutElement>();
            inputLe.flexibleWidth = 1f;
            _searchInput.onValueChanged.AddListener(new UnityAction<string>(OnFilterChanged));

            MakeButton(go.transform, "NEAREST", 84f, ControlHeight, () => NearestRequested?.Invoke());
        }

        private void BuildAirportList(Transform parent)
        {
            var scrollGo = new GameObject("AirportScroll", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
            scrollGo.transform.SetParent(parent, false);
            var le = scrollGo.GetComponent<LayoutElement>();
            le.preferredHeight = ListHeight;
            le.minHeight = 100f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewportGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            viewportGo.GetComponent<Image>().color = new Color(0.02f, 0.06f, 0.035f, 0.85f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _contentRt = contentGo.GetComponent<RectTransform>();
            _contentRt.anchorMin = new Vector2(0f, 1f);
            _contentRt.anchorMax = new Vector2(1f, 1f);
            _contentRt.pivot = new Vector2(0.5f, 1f);
            _contentRt.sizeDelta = new Vector2(0f, 10f);
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = RowSpacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.content = _contentRt;
            scrollRect.viewport = vpRt;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;
            _scrollRect = scrollRect;

            // Subtle top/bottom scrim fades, shown only when rows overflow the viewport.
            _fadeTop = MakeListFade(scrollGo.transform, true);
            _fadeBottom = MakeListFade(scrollGo.transform, false);
        }

        private GameObject MakeListFade(Transform parent, bool top)
        {
            var go = new GameObject(top ? "FadeTop" : "FadeBottom", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            float edge = top ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, edge);
            rt.anchorMax = new Vector2(1f, edge);
            rt.pivot = new Vector2(0.5f, edge);
            rt.sizeDelta = new Vector2(0f, 16f);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = TextureFactory.CreateFadeSprite(top);
            img.color = Color.white;
            img.raycastTarget = false;
            go.SetActive(false);
            return go;
        }

        private void RefreshFades()
        {
            bool overflow = _airportRows.Count > 0 &&
                _airportRows.Count * (RowHeight + RowSpacing) - RowSpacing > ListHeight + 0.5f;
            if (_fadeTop != null) _fadeTop.SetActive(overflow);
            if (_fadeBottom != null) _fadeBottom.SetActive(overflow);
        }

        private void BuildCourseDeck(Transform parent)
        {
            var deck = new GameObject("CourseDeck", typeof(RectTransform), typeof(VerticalLayoutGroup));
            deck.transform.SetParent(parent, false);
            var vlg = deck.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Segmented AUTO / MANUAL mode control.
            var modeRow = MakeFilledRow(deck.transform, ControlHeight, 4f);
            var autoBtn = MakeFlexButton(modeRow.transform, "AUTO", ControlHeight, () => ModeChanged?.Invoke(CourseMode.Auto));
            _autoBtn = autoBtn;
            _autoText = autoBtn.GetComponentInChildren<TextMeshProUGUI>();
            var manualBtn = MakeFlexButton(modeRow.transform, "MANUAL", ControlHeight, () => ModeChanged?.Invoke(CourseMode.Manual));
            _manualBtn = manualBtn;
            _manualText = manualBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Big CRS readout: click and type a course, scroll-wheel adjusts +/-1.
            var crsCaption = MakeText(deck.transform, "CrsCaption", "COURSE", 10, FontStyles.Bold,
                UiColors.TextMuted, TextAlignmentOptions.Center);
            var crsCaptionLe = crsCaption.gameObject.AddComponent<LayoutElement>();
            crsCaptionLe.preferredHeight = 14f;
            _crsInput = MakeInput(deck.transform, null, 26, true);
            var crsLe = _crsInput.gameObject.AddComponent<LayoutElement>();
            crsLe.preferredHeight = 44f;
            _crsInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            _crsInput.characterLimit = 3;
            _crsInput.onEndEdit.AddListener(new UnityAction<string>(OnCourseTyped));
            var trigger = _crsInput.gameObject.AddComponent<EventTrigger>();
            var scrollEntry = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            scrollEntry.callback.AddListener(new UnityAction<BaseEventData>(OnCourseScroll));
            trigger.triggers.Add(scrollEntry);

            var adjustRow = MakeFilledRow(deck.transform, ControlHeight, 8f);
            MakeFlexButton(adjustRow.transform, "-5", ControlHeight, () => CourseAdjusted?.Invoke(-5f));
            MakeFlexButton(adjustRow.transform, "-1", ControlHeight, () => CourseAdjusted?.Invoke(-1f));
            MakeFlexButton(adjustRow.transform, "+1", ControlHeight, () => CourseAdjusted?.Invoke(1f));
            MakeFlexButton(adjustRow.transform, "+5", ControlHeight, () => CourseAdjusted?.Invoke(5f));

            var setRow = MakeFilledRow(deck.transform, ControlHeight, 8f);
            MakeFlexButton(setRow.transform, "SET BRG", ControlHeight, () => SetCourseToBearing?.Invoke());
            MakeFlexButton(setRow.transform, "SET HDG", ControlHeight, () => SetCourseToHeading?.Invoke());
            _toFromBtn = MakeFlexButton(setRow.transform, "TO/FR", ControlHeight, () => CourseFlipToFrom?.Invoke());

            _targetLabel = MakeText(deck.transform, "Target", "", 12, FontStyles.Bold,
                UiColors.HudAmber, TextAlignmentOptions.Center);
            var targetLe = _targetLabel.gameObject.AddComponent<LayoutElement>();
            targetLe.preferredHeight = 18f;
        }

        private GameObject MakeCenteredRow(Transform parent, float height, float spacing)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            return go;
        }

        // Full-width row whose children share the width equally (aligned left/right edges).
        private GameObject MakeFilledRow(Transform parent, float height, float spacing)
        {
            var go = MakeCenteredRow(parent, height, spacing);
            go.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            return go;
        }

        private Button MakeFlexButton(Transform parent, string text, float height, UnityAction onClick)
        {
            var btn = MakeButton(parent, text, 0f, height, onClick);
            var le = btn.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredWidth = -1f;
            return btn;
        }

        private void OnDragEnded(Vector2 pos)
        {
            Plugin.PanelX.Value = pos.x;
            Plugin.PanelY.Value = pos.y;
        }

        private void OnFilterChanged(string value)
        {
            _filter = value ?? "";
            if (_airports != null)
                RebuildRowsIfNeeded(true);
        }

        private void OnCourseTyped(string value)
        {
            // Display text carries a degree symbol ("262°"); strip it before parsing.
            var cleaned = (value ?? "").Trim().TrimEnd('°').Trim();
            if (int.TryParse(cleaned, out int course))
                CourseSet?.Invoke(Mathf.Repeat(course, 360f));
            // On parse failure the next SetCourse refresh restores the display text.
        }

        private void OnCourseScroll(BaseEventData data)
        {
            var ped = data as PointerEventData;
            if (ped == null) return;
            CourseAdjusted?.Invoke(ped.scrollDelta.y > 0f ? 1f : -1f);
        }

        private void ToggleMinimized()
        {
            _minimized = !_minimized;
            if (_body != null) _body.SetActive(!_minimized);
            if (_minimizeLabel != null) _minimizeLabel.text = _minimized ? "+" : "_";
        }

        public void SetCourse(CourseMode mode, float course, bool toStation, string airportName)
        {
            if (_crsInput != null && !_crsInput.isFocused)
                _crsInput.SetTextWithoutNotify($"{Mathf.RoundToInt(course):000}°");

            string name = string.IsNullOrEmpty(airportName) ? "---" : airportName;
            string target = (mode == CourseMode.Manual && !toStation ? "FROM " : "TO ") + name;
            if (target != _targetText)
            {
                _targetText = target;
                if (_targetLabel != null) _targetLabel.text = target;
            }

            bool auto = mode == CourseMode.Auto;
            if (_autoBtn != null)
            {
                ApplyButtonTint(_autoBtn, auto ? UiColors.HudGreenDim : UiColors.BgPanelRaised);
                _autoText.color = auto ? UiColors.TextPrimary : UiColors.TextSecondary;
                ApplyButtonTint(_manualBtn, auto ? UiColors.BgPanelRaised : UiColors.HudGreenDim);
                _manualText.color = auto ? UiColors.TextSecondary : UiColors.TextPrimary;
            }

            // Tie the TO/FROM state to its control: amber when flying FROM the station.
            if (_toFromBtn != null)
            {
                bool from = mode == CourseMode.Manual && !toStation;
                ApplyButtonTint(_toFromBtn, from ? UiColors.HudAmberDim : UiColors.HudGreenDim);
            }
        }

        public void SetAirports(IReadOnlyList<AirportInfo> airports, int selectedSourceIndex)
        {
            bool selectionChanged = selectedSourceIndex != _selectedSourceIndex;
            _airports = airports;
            _selectedSourceIndex = selectedSourceIndex;
            RebuildRowsIfNeeded(false);
            RefreshSelection();
            RefreshMeta();
            RefreshHeaderReadout();
            if (selectionChanged)
                EnsureSelectedRowVisible();
        }

        // Keep the current-target row in view after selection changes (hotkey cycling,
        // NEAREST, click). No-op when the row is already fully visible or filtered out.
        private void EnsureSelectedRowVisible()
        {
            if (_scrollRect == null || _contentRt == null || _selectedSourceIndex < 0) return;

            int row = -1;
            for (int i = 0; i < _rowSourceIndex.Count; i++)
            {
                if (_rowSourceIndex[i] == _selectedSourceIndex) { row = i; break; }
            }
            if (row < 0) return;

            Canvas.ForceUpdateCanvases();
            float contentH = _contentRt.rect.height;
            float viewH = _scrollRect.viewport != null ? _scrollRect.viewport.rect.height : ListHeight;
            if (contentH <= viewH + 0.5f) return;

            float rowTop = row * (RowHeight + RowSpacing);
            float rowBottom = rowTop + RowHeight;
            float offset = (1f - _scrollRect.verticalNormalizedPosition) * (contentH - viewH);
            if (rowTop >= offset && rowBottom <= offset + viewH) return; // already fully visible

            float target = (rowTop + RowHeight * 0.5f - viewH * 0.5f) / (contentH - viewH);
            _scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(target);
        }

        private List<AirportInfo> FilteredAirports()
        {
            var result = new List<AirportInfo>();
            if (_airports == null) return result;
            for (int i = 0; i < _airports.Count; i++)
            {
                var info = _airports[i];
                if (_filter.Length == 0 ||
                    (info.Name != null && info.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0))
                    result.Add(info);
            }
            return result;
        }

        private bool RebuildRowsIfNeeded(bool force)
        {
            var displayed = FilteredAirports();
            var sb = new StringBuilder(displayed.Count * 8);
            for (int i = 0; i < displayed.Count; i++)
            {
                sb.Append(displayed[i].SourceIndex).Append(':').Append(displayed[i].Name).Append(';');
            }
            string signature = sb.ToString();
            if (!force && signature == _rowSignature)
                return false;
            _rowSignature = signature;

            foreach (var row in _airportRows)
                UnityEngine.Object.Destroy(row);
            _airportRows.Clear();
            _rowBg.Clear();
            _rowBtns.Clear();
            _rowName.Clear();
            _rowMeta.Clear();
            _rowSourceIndex.Clear();
            _rowSelected.Clear();

            for (int i = 0; i < displayed.Count; i++)
                AddAirportRow(displayed[i]);
            RefreshFades();
            return true;
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < _rowBg.Count; i++)
            {
                bool sel = _selectedSourceIndex >= 0 && _rowSourceIndex[i] == _selectedSourceIndex;
                if (sel == _rowSelected[i]) continue;
                _rowSelected[i] = sel;
                ApplyRowVisual(i);
            }
        }

        private void ApplyRowVisual(int i)
        {
            bool sel = _rowSelected[i];
            // Tint drives hover/press/keyboard-focus feedback; base color carries the
            // scrim (unselected) or the current-target highlight (selected).
            ApplyButtonTint(_rowBtns[i], sel ? UiColors.HudGreenDim : UiColors.RowScrim);
            _rowName[i].color = sel ? UiColors.HudGreen : UiColors.TextSecondary;
            _rowName[i].fontStyle = sel ? FontStyles.Bold : FontStyles.Normal;
            _rowMeta[i].color = sel ? UiColors.TextPrimary : UiColors.TextSecondary;
        }

        private void RefreshMeta()
        {
            if (_airports == null) return;
            for (int i = 0; i < _rowMeta.Count; i++)
            {
                // Look up fresh values by source index so re-sorted distances stay live.
                int src = _rowSourceIndex[i];
                AirportInfo info = default(AirportInfo);
                bool found = false;
                for (int j = 0; j < _airports.Count; j++)
                {
                    if (_airports[j].SourceIndex == src)
                    {
                        info = _airports[j];
                        found = true;
                        break;
                    }
                }
                string meta = found && info.HasPosition
                    ? $"BRG {Mathf.RoundToInt(info.Bearing):000}°  {info.DistanceKm:F1}km"
                    : "";
                if (_rowMeta[i].text != meta)
                    _rowMeta[i].text = meta;
            }
        }

        private void RefreshHeaderReadout()
        {
            string text = "";
            if (_airports != null)
            {
                for (int i = 0; i < _airports.Count; i++)
                {
                    var info = _airports[i];
                    if (info.SourceIndex != _selectedSourceIndex) continue;
                    text = info.HasPosition
                        ? $"{info.Name}  BRG {Mathf.RoundToInt(info.Bearing):000}°  {info.DistanceKm:F1}km"
                        : info.Name;
                    break;
                }
            }
            if (text == _headerText) return;
            _headerText = text;
            if (_headerReadout != null) _headerReadout.text = text;
        }

        private void AddAirportRow(AirportInfo info)
        {
            var go = new GameObject("Row" + info.SourceIndex, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_contentRt.transform, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = RowHeight;
            le.minHeight = RowHeight;

            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();
            int sourceIndex = info.SourceIndex;
            btn.onClick.AddListener(new UnityAction(() => AirportSelected?.Invoke(sourceIndex)));

            var nameTmp = MakeText(go.transform, "Name", info.Name, 13, FontStyles.Normal,
                UiColors.TextSecondary, TextAlignmentOptions.MidlineLeft);
            var nameRt = nameTmp.GetComponent<RectTransform>();
            nameRt.anchorMin = Vector2.zero;
            nameRt.anchorMax = Vector2.one;
            nameRt.offsetMin = new Vector2(10f, 0f);
            nameRt.offsetMax = new Vector2(-150f, 0f);
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;
            nameTmp.raycastTarget = false;

            var metaTmp = MakeText(go.transform, "Meta", "", 11, FontStyles.Normal,
                UiColors.TextSecondary, TextAlignmentOptions.MidlineRight);
            var metaRt = metaTmp.GetComponent<RectTransform>();
            metaRt.anchorMin = new Vector2(1f, 0f);
            metaRt.anchorMax = Vector2.one;
            metaRt.offsetMin = new Vector2(-146f, 0f);
            metaRt.offsetMax = new Vector2(-10f, 0f);
            metaTmp.raycastTarget = false;

            _airportRows.Add(go);
            _rowBg.Add(img);
            _rowBtns.Add(btn);
            _rowName.Add(nameTmp);
            _rowMeta.Add(metaTmp);
            _rowSourceIndex.Add(info.SourceIndex);
            _rowSelected.Add(false);
            ApplyRowVisual(_airportRows.Count - 1);
        }

        public void Toggle()
        {
            SetVisible(!_visible);
        }

        public bool IsVisible => _visible;

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

        private Button MakeButton(Transform parent, string text, float width, float height, UnityAction onClick)
        {
            var go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            var btn = go.GetComponent<Button>();
            ApplyButtonTint(btn, UiColors.BgPanelRaised);
            btn.onClick.AddListener(onClick);

            var tmp = MakeText(go.transform, "Text", text, 12, FontStyles.Bold,
                UiColors.TextPrimary, TextAlignmentOptions.Center);
            var tmpRt = tmp.GetComponent<RectTransform>();
            tmpRt.anchorMin = Vector2.zero;
            tmpRt.anchorMax = Vector2.one;
            tmpRt.offsetMin = Vector2.zero;
            tmpRt.offsetMax = Vector2.zero;
            tmp.raycastTarget = false;
            return btn;
        }

        // ColorTint transitions set the graphic color absolutely, so the tint must carry
        // the base color itself; otherwise hover/press flashes white over dark buttons.
        private static void ApplyButtonTint(Button btn, Color baseColor)
        {
            var colors = btn.colors;
            colors.colorMultiplier = 1f;
            colors.normalColor = baseColor;
            colors.highlightedColor = Brighten(baseColor, 1.5f);
            colors.pressedColor = Brighten(baseColor, 1.9f);
            colors.selectedColor = Brighten(baseColor, 1.5f);
            colors.disabledColor = new Color(baseColor.r * 0.5f, baseColor.g * 0.5f, baseColor.b * 0.5f, baseColor.a * 0.5f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = baseColor;
        }

        private static Color Brighten(Color c, float f)
        {
            return new Color(
                Mathf.Clamp01(c.r * f + 0.04f),
                Mathf.Clamp01(c.g * f + 0.04f),
                Mathf.Clamp01(c.b * f + 0.04f),
                c.a);
        }

        private TextMeshProUGUI MakeText(Transform parent, string name, string text, int fontSize,
            FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = FontLoader.GetDefaultFont();
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.text = text;
            tmp.alignment = alignment;
            return tmp;
        }

        private TMP_InputField MakeInput(Transform parent, string placeholder, int fontSize, bool big)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = UiColors.BgPanelRaised;

            var areaGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(go.transform, false);
            var areaRt = areaGo.GetComponent<RectTransform>();
            areaRt.anchorMin = Vector2.zero;
            areaRt.anchorMax = Vector2.one;
            areaRt.offsetMin = new Vector2(8f, 2f);
            areaRt.offsetMax = new Vector2(-8f, -2f);

            TextMeshProUGUI placeholderTmp = null;
            if (placeholder != null)
            {
                placeholderTmp = MakeText(areaGo.transform, "Placeholder", placeholder, fontSize,
                    FontStyles.Normal, UiColors.TextMuted,
                    big ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft);
                var phRt = placeholderTmp.GetComponent<RectTransform>();
                phRt.anchorMin = Vector2.zero;
                phRt.anchorMax = Vector2.one;
                phRt.offsetMin = Vector2.zero;
                phRt.offsetMax = Vector2.zero;
                placeholderTmp.raycastTarget = false;
            }

            var textTmp = MakeText(areaGo.transform, "Text", "", fontSize, big ? FontStyles.Bold : FontStyles.Normal,
                big ? UiColors.HudGreen : UiColors.TextPrimary,
                big ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft);
            var textRt = textTmp.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            textTmp.raycastTarget = false;

            var input = go.GetComponent<TMP_InputField>();
            input.textViewport = areaRt;
            input.textComponent = textTmp;
            if (placeholderTmp != null)
                input.placeholder = placeholderTmp;
            input.caretColor = UiColors.HudGreen;
            input.selectionColor = UiColors.HudGreenDim;
            return input;
        }
    }
}
