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
    public sealed class NavPanel
    {
        private const float PanelWidth = 820f;
        private const float PanelHeight = 430f;
        private const float HeaderHeight = 36f;
        private const float RowHeight = 28f;
        private const float RowSpacing = 2f;
        private const float ListHeight = 298f;
        private const float ControlHeight = 26f;

        private sealed class AirportRow
        {
            public int SourceIndex;
            public Button Button;
            public Image SelectionRail;
            public Image FactionRail;
            public TextMeshProUGUI Name;
            public TextMeshProUGUI Bearing;
            public TextMeshProUGUI Distance;
        }

        private GameObject _root;
        private GameObject _ownedEventSystem;
        private RectTransform _panelRt;
        private GameObject _body;
        private RectTransform _contentRt;
        private ScrollRect _scrollRect;
        private TextMeshProUGUI _emptyLabel;
        private TextMeshProUGUI _headerReadout;
        private TextMeshProUGUI _minimizeLabel;
        private TMP_InputField _searchInput;
        private PanelHsi _hsi;
        private Button _autoButton;
        private TextMeshProUGUI _autoLabel;
        private Button _manualButton;
        private TextMeshProUGUI _manualLabel;
        private Button _nearButton;
        private TextMeshProUGUI _nearLabel;
        private Button _nameButton;
        private TextMeshProUGUI _nameLabel;
        private Button _friendlyButton;
        private TextMeshProUGUI _friendlyLabel;
        private GameObject _runwaySection;
        private Transform _runwayRow;
        private TextMeshProUGUI _fieldFactsTop;
        private TextMeshProUGUI _fieldFactsBottom;

        private readonly List<AirportRow> _rows = new List<AirportRow>();
        private readonly List<Button> _runwayButtons = new List<Button>();
        private readonly List<TextMeshProUGUI> _runwayLabels = new List<TextMeshProUGUI>();
        private readonly List<RunwayInfo> _runways = new List<RunwayInfo>();
        private int _runwaySourceIndex = -1;
        private int _selectedRunwayIndex = -1;
        private IReadOnlyList<AirportInfo> _airports;
        private CdiData _navigation;
        private string _filter = "";
        private string _rowSignature = "";
        private string _runwaySignature = "";
        private int _selectedSourceIndex = -1;
        private AirportSortMode _sortMode;
        private bool _friendlyOnly;
        private bool _minimized;
        private bool _visible = true;
        private CursorLockMode _previousLockState;
        private bool _previousCursorVisible;

        public event Action<int> AirportSelected;
        public event Action<CourseMode> ModeChanged;
        public event Action<float> CourseAdjusted;
        public event Action SetReciprocalCourse;
        public event Action SetCourseToBearing;
        public event Action SetCourseToHeading;
        public event Action<int, float> RunwaySelected;

        public void Create()
        {
            _sortMode = Plugin.SortByName.Value ? AirportSortMode.Name : AirportSortMode.Nearest;
            _friendlyOnly = Plugin.FriendlyOnly.Value;
            _previousLockState = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;

            _root = new GameObject("NOVorNavCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;
            UnityEngine.Object.DontDestroyOnLoad(_root);

            var scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
            {
                _ownedEventSystem = new GameObject("NOVorEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                UnityEngine.Object.DontDestroyOnLoad(_ownedEventSystem);
            }

            var canvasRect = _root.GetComponent<RectTransform>();
            canvasRect.anchorMin = Vector2.zero;
            canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = Vector2.zero;
            canvasRect.offsetMax = Vector2.zero;

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(_root.transform, false);
            _panelRt = panel.GetComponent<RectTransform>();
            _panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRt.pivot = new Vector2(0.5f, 0.5f);
            _panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panelRt.anchoredPosition = new Vector2(Plugin.PanelX.Value, Plugin.PanelY.Value);

            var panelImage = panel.GetComponent<Image>();
            panelImage.sprite = TextureFactory.CreateFramedSprite(UiColors.Chrome, UiColors.Rule, 1);
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Color.white;

            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var trigger = panel.AddComponent<EventTrigger>();
            var scrollEntry = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
            scrollEntry.callback.AddListener(new UnityAction<BaseEventData>(data => data.Use()));
            trigger.triggers.Add(scrollEntry);

            BuildHeader(panel.transform, canvasRect);
            BuildBody(panel.transform);
            RefreshFilterStyles();
            SetVisible(false);
        }

        private void BuildHeader(Transform parent, RectTransform canvasRect)
        {
            var header = MakeHorizontal(parent, "Header", HeaderHeight, 6f);
            header.GetComponent<Image>().color = UiColors.ChromeRaised;
            var layout = header.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 4, 0, 0);
            header.AddComponent<WindowDragHandler>().Init(_panelRt, canvasRect, OnDragEnded);

            var title = MakeText(header.transform, "Title", "NAV / CDI", 15, FontStyles.Bold,
                UiColors.Amber, TextAlignmentOptions.MidlineLeft);
            title.enableWordWrapping = false;
            title.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;

            _headerReadout = MakeText(header.transform, "Readout", "", 12, FontStyles.Normal,
                UiColors.PanelText, TextAlignmentOptions.MidlineRight);
            _headerReadout.enableWordWrapping = false;
            _headerReadout.overflowMode = TextOverflowModes.Ellipsis;
            _headerReadout.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var minimize = MakeButton(header.transform, "–", 32f, 28f, ToggleMinimized);
            _minimizeLabel = minimize.GetComponentInChildren<TextMeshProUGUI>();
            StyleAction(minimize, _minimizeLabel);
            var close = MakeButton(header.transform, "×", 32f, 28f, () => SetVisible(false));
            StyleAction(close, close.GetComponentInChildren<TextMeshProUGUI>());
        }

        private void BuildBody(Transform parent)
        {
            _body = MakeHorizontal(parent, "Body", PanelHeight - HeaderHeight - 22f, 8f);
            _body.GetComponent<Image>().color = UiColors.Transparent;
            var airportPane = MakeVertical(_body.transform, "AirportPane", 482f, 4f);
            BuildSearchRow(airportPane.transform);
            BuildColumnHeader(airportPane.transform);
            BuildAirportList(airportPane.transform);

            var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            divider.transform.SetParent(_body.transform, false);
            divider.GetComponent<Image>().color = UiColors.Rule;
            divider.GetComponent<LayoutElement>().preferredWidth = 1f;

            var navPane = MakeVertical(_body.transform, "NavPane", 303f, 4f);
            BuildNavigationPane(navPane.transform);
        }

        private void BuildSearchRow(Transform parent)
        {
            var row = MakeHorizontal(parent, "SearchRow", 28f, 4f);
            _searchInput = MakeInput(row.transform, "SEARCH FIELDS");
            _searchInput.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _searchInput.onValueChanged.AddListener(new UnityAction<string>(OnFilterChanged));

            _nearButton = MakeButton(row.transform, "NEAR", 48f, ControlHeight, SetSortNearest, 10);
            _nearLabel = _nearButton.GetComponentInChildren<TextMeshProUGUI>();
            _nameButton = MakeButton(row.transform, "A–Z", 42f, ControlHeight, SetSortName, 10);
            _nameLabel = _nameButton.GetComponentInChildren<TextMeshProUGUI>();
            _friendlyButton = MakeButton(row.transform, "FRIENDLY", 70f, ControlHeight, ToggleFriendlyOnly, 9);
            _friendlyLabel = _friendlyButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void BuildColumnHeader(Transform parent)
        {
            var row = new GameObject("ColumnHeader", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = Vector2.one;
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, 18f);
            var rowElement = row.GetComponent<LayoutElement>();
            rowElement.preferredHeight = 18f;
            rowElement.preferredWidth = 0f;
            rowElement.flexibleWidth = 1f;
            var rowImage = row.GetComponent<Image>();
            rowImage.color = UiColors.InstrumentWell;
            rowImage.raycastTarget = false;
            PlaceText(row.transform, "Field", "FIELD", UiColors.PanelMuted,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-124f, 0f),
                TextAlignmentOptions.MidlineLeft, 9, FontStyles.Bold);
            PlaceText(row.transform, "Bearing", "BRG", UiColors.PanelMuted,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-120f, 0f), new Vector2(-58f, 0f),
                TextAlignmentOptions.MidlineRight, 9, FontStyles.Bold);
            PlaceText(row.transform, "Distance", "NM", UiColors.PanelMuted,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-54f, 0f), new Vector2(-13f, 0f),
                TextAlignmentOptions.MidlineRight, 9, FontStyles.Bold);
        }

        private void BuildAirportList(Transform parent)
        {
            var scroll = new GameObject("AirportScroll", typeof(RectTransform), typeof(Image),
                typeof(ScrollRect), typeof(LayoutElement));
            scroll.transform.SetParent(parent, false);
            scroll.GetComponent<Image>().color = UiColors.InstrumentWell;
            var scrollRectTransform = scroll.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 1f);
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.pivot = new Vector2(0.5f, 1f);
            scrollRectTransform.sizeDelta = new Vector2(0f, ListHeight);
            var scrollElement = scroll.GetComponent<LayoutElement>();
            scrollElement.preferredHeight = ListHeight;
            scrollElement.preferredWidth = 0f;
            scrollElement.flexibleWidth = 1f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scroll.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(-10f, 0f);
            viewport.GetComponent<Image>().color = UiColors.Transparent;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _contentRt = content.GetComponent<RectTransform>();
            _contentRt.anchorMin = new Vector2(0f, 1f);
            _contentRt.anchorMax = new Vector2(1f, 1f);
            _contentRt.pivot = new Vector2(0.5f, 1f);
            _contentRt.sizeDelta = new Vector2(0f, 10f);
            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = RowSpacing;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = BuildScrollbar(scroll.transform);
            _scrollRect = scroll.GetComponent<ScrollRect>();
            _scrollRect.content = _contentRt;
            _scrollRect.viewport = viewportRect;
            _scrollRect.vertical = true;
            _scrollRect.horizontal = false;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 20f;
            _scrollRect.verticalScrollbar = scrollbar;
            _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            _emptyLabel = MakeText(viewport.transform, "Empty", "NO MATCHING FIELDS", 11,
                FontStyles.Normal, UiColors.PanelMuted, TextAlignmentOptions.Center);
            Stretch(_emptyLabel.rectTransform);
            _emptyLabel.raycastTarget = false;
            _emptyLabel.gameObject.SetActive(false);
        }

        private Scrollbar BuildScrollbar(Transform parent)
        {
            var track = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            track.transform.SetParent(parent, false);
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(1f, 0f);
            trackRect.anchorMax = Vector2.one;
            trackRect.pivot = new Vector2(1f, 0.5f);
            trackRect.sizeDelta = new Vector2(8f, 0f);
            track.GetComponent<Image>().color = UiColors.Chrome;

            var area = new GameObject("SlidingArea", typeof(RectTransform));
            area.transform.SetParent(track.transform, false);
            var areaRect = area.GetComponent<RectTransform>();
            Stretch(areaRect, new Vector2(1f, 1f), new Vector2(-1f, -1f));

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(area.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            Stretch(handleRect);
            handle.GetComponent<Image>().color = UiColors.AmberDim;

            var scrollbar = track.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private void BuildNavigationPane(Transform parent)
        {
            var modeRow = MakeHorizontal(parent, "ModeRow", ControlHeight, 4f);
            _autoButton = MakeFlexButton(modeRow.transform, "AUTO", () => ModeChanged?.Invoke(CourseMode.Auto));
            _autoLabel = _autoButton.GetComponentInChildren<TextMeshProUGUI>();
            _manualButton = MakeFlexButton(modeRow.transform, "MANUAL", () => ModeChanged?.Invoke(CourseMode.Manual));
            _manualLabel = _manualButton.GetComponentInChildren<TextMeshProUGUI>();

            var hsiSlot = new GameObject("HsiSlot", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(LayoutElement));
            hsiSlot.transform.SetParent(parent, false);
            hsiSlot.GetComponent<LayoutElement>().preferredHeight = 210f;
            var hsiSlotImage = hsiSlot.GetComponent<Image>();
            hsiSlotImage.sprite = TextureFactory.CreateFramedSprite(UiColors.InstrumentWell, UiColors.Rule, 1);
            hsiSlotImage.type = Image.Type.Sliced;
            hsiSlotImage.color = Color.white;
            hsiSlotImage.raycastTarget = false;
            var hsiObject = new GameObject("HSI", typeof(RectTransform), typeof(Image), typeof(PanelHsi));
            hsiObject.transform.SetParent(hsiSlot.transform, false);
            var hsiRect = hsiObject.GetComponent<RectTransform>();
            hsiRect.anchorMin = new Vector2(0.5f, 0.5f);
            hsiRect.anchorMax = new Vector2(0.5f, 0.5f);
            hsiRect.pivot = new Vector2(0.5f, 0.5f);
            hsiRect.anchoredPosition = new Vector2(0f, -2f);
            _hsi = hsiObject.GetComponent<PanelHsi>();
            _hsi.Init(184f);
            _hsi.CourseAdjusted += delta => CourseAdjusted?.Invoke(delta);

            var actionRow = MakeHorizontal(parent, "ActionRow", ControlHeight, 4f);
            MakeActionButton(actionRow.transform, "SET BRG", () => SetCourseToBearing?.Invoke());
            MakeActionButton(actionRow.transform, "SET HDG", () => SetCourseToHeading?.Invoke());
            MakeActionButton(actionRow.transform, "RECIP", () => SetReciprocalCourse?.Invoke());

            _runwaySection = MakeVertical(parent, "RunwaySection", 48f, 2f);
            var runwayCaption = MakeText(_runwaySection.transform, "Caption", "RUNWAY COURSE", 9,
                FontStyles.Bold, UiColors.PanelMuted, TextAlignmentOptions.Center);
            runwayCaption.gameObject.AddComponent<LayoutElement>().preferredHeight = 12f;
            _runwayRow = MakeHorizontal(_runwaySection.transform, "RunwayRow", ControlHeight, 4f).transform;

            _fieldFactsTop = MakeText(parent, "FactsTop", "ELEV ---     ETA --:--", 11,
                FontStyles.Normal, UiColors.PanelText, TextAlignmentOptions.Center);
            _fieldFactsTop.gameObject.AddComponent<LayoutElement>().preferredHeight = 17f;
            _fieldFactsBottom = MakeText(parent, "FactsBottom", "STEER ---     GS ---", 11,
                FontStyles.Normal, UiColors.PanelText, TextAlignmentOptions.Center);
            _fieldFactsBottom.gameObject.AddComponent<LayoutElement>().preferredHeight = 17f;
        }

        public void SetAirports(IReadOnlyList<AirportInfo> airports, int selectedSourceIndex)
        {
            bool selectionChanged = selectedSourceIndex != _selectedSourceIndex;
            if (selectionChanged) _selectedRunwayIndex = -1;
            _airports = airports;
            _selectedSourceIndex = selectedSourceIndex;
            RebuildRowsIfNeeded(false);
            RefreshRows();
            RefreshHeader();
            RefreshRunways();
            RefreshFieldFacts();
            if (selectionChanged) EnsureSelectedRowVisible();
        }

        public void SetNavigation(CdiData data)
        {
            _navigation = data;
            _hsi?.SetData(data);
            StyleToggle(_autoButton, _autoLabel, data.Mode == CourseMode.Auto);
            StyleToggle(_manualButton, _manualLabel, data.Mode == CourseMode.Manual);
            RefreshRunwaySelection(data.Course);
            RefreshFieldFacts();
        }

        private List<AirportInfo> DisplayedAirports()
        {
            var displayed = new List<AirportInfo>();
            if (_airports == null) return displayed;
            for (int i = 0; i < _airports.Count; i++)
            {
                var info = _airports[i];
                if (_friendlyOnly && !info.IsFriendly) continue;
                if (_filter.Length > 0 &&
                    (info.Name == null || info.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                displayed.Add(info);
            }

            displayed.Sort((a, b) =>
            {
                if (_sortMode == AirportSortMode.Name)
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (a.HasPosition != b.HasPosition) return a.HasPosition ? -1 : 1;
                if (a.HasPosition) return a.DistanceNm.CompareTo(b.DistanceNm);
                return a.SourceIndex.CompareTo(b.SourceIndex);
            });
            return displayed;
        }

        private void RebuildRowsIfNeeded(bool force)
        {
            var displayed = DisplayedAirports();
            var signature = new StringBuilder(displayed.Count * 10);
            for (int i = 0; i < displayed.Count; i++)
                signature.Append(displayed[i].SourceIndex).Append(':').Append(displayed[i].Name).Append(';');
            string value = signature.ToString();
            if (!force && value == _rowSignature) return;
            _rowSignature = value;

            for (int i = 0; i < _rows.Count; i++)
                UnityEngine.Object.Destroy(_rows[i].Button.gameObject);
            _rows.Clear();
            for (int i = 0; i < displayed.Count; i++) AddAirportRow(displayed[i]);
            _emptyLabel.gameObject.SetActive(displayed.Count == 0);
        }

        private void AddAirportRow(AirportInfo info)
        {
            var rowObject = new GameObject("Field" + info.SourceIndex, typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            rowObject.transform.SetParent(_contentRt, false);
            var rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = Vector2.one;
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(0f, RowHeight);
            var rowElement = rowObject.GetComponent<LayoutElement>();
            rowElement.preferredHeight = RowHeight;
            rowElement.preferredWidth = 0f;
            rowElement.flexibleWidth = 1f;
            var button = rowObject.GetComponent<Button>();
            int sourceIndex = info.SourceIndex;
            button.onClick.AddListener(new UnityAction(() => AirportSelected?.Invoke(sourceIndex)));

            var selectionRail = PlaceImage(rowObject.transform, "Selection", UiColors.Transparent,
                new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
            var factionRail = PlaceImage(rowObject.transform, "Faction", info.FactionColor,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(4f, 0f), new Vector2(8f, 0f));
            var name = PlaceText(rowObject.transform, "Name", "", UiColors.PanelText,
                Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-126f, 0f),
                TextAlignmentOptions.MidlineLeft, 11, FontStyles.Normal);
            name.overflowMode = TextOverflowModes.Ellipsis;
            var bearing = PlaceText(rowObject.transform, "Bearing", "", UiColors.PanelMuted,
                new Vector2(1f, 0f), Vector2.one, new Vector2(-120f, 0f), new Vector2(-58f, 0f),
                TextAlignmentOptions.MidlineRight, 10, FontStyles.Normal);
            var distance = PlaceText(rowObject.transform, "Distance", "", UiColors.PanelMuted,
                new Vector2(1f, 0f), Vector2.one, new Vector2(-54f, 0f), new Vector2(-13f, 0f),
                TextAlignmentOptions.MidlineRight, 10, FontStyles.Normal);

            _rows.Add(new AirportRow
            {
                SourceIndex = info.SourceIndex,
                Button = button,
                SelectionRail = selectionRail,
                FactionRail = factionRail,
                Name = name,
                Bearing = bearing,
                Distance = distance
            });
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                if (!TryGetAirport(row.SourceIndex, out AirportInfo info)) continue;
                bool selected = row.SourceIndex == _selectedSourceIndex;
                row.SelectionRail.color = selected ? UiColors.Amber : UiColors.Transparent;
                row.FactionRail.color = info.HasFaction ? info.FactionColor : UiColors.Rule;
                row.Name.text = info.Name + (info.IsMobile ? "  MOV" : "");
                row.Name.color = selected ? UiColors.Amber : UiColors.PanelText;
                row.Name.fontStyle = selected ? FontStyles.Bold : FontStyles.Normal;
                row.Bearing.text = info.HasPosition ? $"{Mathf.RoundToInt(info.Bearing):000}°" : "---";
                row.Distance.text = info.HasPosition ? info.DistanceNm.ToString("0.0") : "--.-";
                row.Bearing.color = selected ? UiColors.PanelText : UiColors.PanelMuted;
                row.Distance.color = selected ? UiColors.PanelText : UiColors.PanelMuted;
                ApplyButtonTint(row.Button, selected ? UiColors.ChromeRaised : UiColors.RowSurface);
            }
        }

        private void RefreshHeader()
        {
            if (!TryGetAirport(_selectedSourceIndex, out AirportInfo info))
            {
                _headerReadout.text = "NO FIELD SELECTED";
                return;
            }
            _headerReadout.text = info.HasPosition
                ? $"{info.Name}  {Mathf.RoundToInt(info.Bearing):000}°  {info.DistanceNm:0.0} NM"
                : info.Name;
        }

        private void RefreshRunways()
        {
            RunwayInfo[] runways = null;
            if (TryGetAirport(_selectedSourceIndex, out AirportInfo info)) runways = info.Runways;
            bool hasRunways = runways != null && runways.Length > 0;
            _runwaySection.SetActive(hasRunways);
            if (!hasRunways) return;

            var signature = new StringBuilder();
            for (int i = 0; i < runways.Length; i++)
                signature.Append(runways[i].Label).Append(':').Append(runways[i].Heading).Append(':')
                    .Append(runways[i].LengthMeters).Append(';');
            string value = signature.ToString();
            if (value == _runwaySignature && _runwaySourceIndex == _selectedSourceIndex) return;
            _runwaySignature = value;
            _runwaySourceIndex = _selectedSourceIndex;

            for (int i = 0; i < _runwayButtons.Count; i++)
                UnityEngine.Object.Destroy(_runwayButtons[i].gameObject);
            _runwayButtons.Clear();
            _runwayLabels.Clear();
            _runways.Clear();

            for (int i = 0; i < runways.Length; i++)
            {
                var runway = runways[i];
                float heading = runway.Heading;
                int runwayIndex = i;
                string label = $"{CompactRunwayName(runway.Label)} {Mathf.RoundToInt(heading):000}° {runway.LengthMeters / 1000f:0.0} km";
                var button = MakeFlexButton(_runwayRow, label, () => SelectRunway(runwayIndex, heading), 9);
                _runwayButtons.Add(button);
                _runwayLabels.Add(button.GetComponentInChildren<TextMeshProUGUI>());
                _runways.Add(runway);
            }
            if (_navigation != null) RefreshRunwaySelection(_navigation.Course);
        }

        private void RefreshRunwaySelection(float course)
        {
            if (_selectedRunwayIndex >= 0 && _selectedRunwayIndex < _runways.Count &&
                Mathf.Abs(Mathf.DeltaAngle(_runways[_selectedRunwayIndex].Heading, course)) > 1f)
                _selectedRunwayIndex = -1;

            if (_selectedRunwayIndex < 0)
            {
                int match = -1;
                int matches = 0;
                for (int i = 0; i < _runways.Count; i++)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(_runways[i].Heading, course)) > 1f) continue;
                    match = i;
                    matches++;
                }
                if (matches == 1) _selectedRunwayIndex = match;
            }

            for (int i = 0; i < _runwayButtons.Count && i < _runways.Count; i++)
            {
                bool selected = i == _selectedRunwayIndex;
                StyleToggle(_runwayButtons[i], _runwayLabels[i], selected);
            }
        }

        private void SelectRunway(int index, float heading)
        {
            _selectedRunwayIndex = index;
            RefreshRunwaySelection(heading);
            RunwaySelected?.Invoke(index, heading);
        }

        private static string CompactRunwayName(string label)
        {
            if (string.IsNullOrEmpty(label)) return "RWY";
            return label.Replace("Flight Deck", "DECK")
                .Replace("Short Takeoff", "STOL")
                .Replace("Takeoff Runway", "RWY");
        }

        private void RefreshFieldFacts()
        {
            if (_navigation == null || !TryGetAirport(_selectedSourceIndex, out AirportInfo info))
            {
                _fieldFactsTop.text = "ELEV ---     ETA --:--";
                _fieldFactsBottom.text = "STEER ---     GS ---";
                return;
            }
            string elevation = info.IsMobile ? "N/A" : Mathf.RoundToInt(info.ElevationMeters) + " m";
            _fieldFactsTop.text = $"ELEV {elevation}     ETA {FormatEta(_navigation)}";
            _fieldFactsBottom.text =
                $"STEER {Mathf.RoundToInt(_navigation.SteerHeading):000}°     GS {Mathf.RoundToInt(_navigation.GroundSpeedKnots)} kt";
        }

        private static string FormatEta(CdiData data)
        {
            if (!data.HasEta) return "N/A";
            int total = Mathf.Clamp(Mathf.RoundToInt(data.EtaSeconds), 0, 5999);
            return $"{total / 60:00}:{total % 60:00}";
        }

        private bool TryGetAirport(int sourceIndex, out AirportInfo info)
        {
            if (_airports != null)
            {
                for (int i = 0; i < _airports.Count; i++)
                {
                    if (_airports[i].SourceIndex != sourceIndex) continue;
                    info = _airports[i];
                    return true;
                }
            }
            info = default(AirportInfo);
            return false;
        }

        private void EnsureSelectedRowVisible()
        {
            int index = -1;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].SourceIndex != _selectedSourceIndex) continue;
                index = i;
                break;
            }
            if (index < 0 || _scrollRect == null) return;

            Canvas.ForceUpdateCanvases();
            float contentHeight = _contentRt.rect.height;
            float viewportHeight = _scrollRect.viewport.rect.height;
            if (contentHeight <= viewportHeight) return;
            float rowCenter = index * (RowHeight + RowSpacing) + RowHeight * 0.5f;
            float target = (rowCenter - viewportHeight * 0.5f) / (contentHeight - viewportHeight);
            _scrollRect.verticalNormalizedPosition = 1f - Mathf.Clamp01(target);
        }

        private void OnFilterChanged(string value)
        {
            _filter = value ?? "";
            RebuildRowsIfNeeded(true);
            RefreshRows();
        }

        private void SetSortNearest()
        {
            _sortMode = AirportSortMode.Nearest;
            Plugin.SortByName.Value = false;
            RefreshFilterStyles();
            RebuildRowsIfNeeded(true);
            RefreshRows();
        }

        private void SetSortName()
        {
            _sortMode = AirportSortMode.Name;
            Plugin.SortByName.Value = true;
            RefreshFilterStyles();
            RebuildRowsIfNeeded(true);
            RefreshRows();
        }

        private void ToggleFriendlyOnly()
        {
            _friendlyOnly = !_friendlyOnly;
            Plugin.FriendlyOnly.Value = _friendlyOnly;
            RefreshFilterStyles();
            RebuildRowsIfNeeded(true);
            RefreshRows();
        }

        private void RefreshFilterStyles()
        {
            StyleToggle(_nearButton, _nearLabel, _sortMode == AirportSortMode.Nearest);
            StyleToggle(_nameButton, _nameLabel, _sortMode == AirportSortMode.Name);
            StyleToggle(_friendlyButton, _friendlyLabel, _friendlyOnly);
        }

        private void ToggleMinimized()
        {
            _minimized = !_minimized;
            _body.SetActive(!_minimized);
            _minimizeLabel.text = _minimized ? "+" : "–";
            _panelRt.sizeDelta = new Vector2(PanelWidth, _minimized ? HeaderHeight + 16f : PanelHeight);
        }

        private void OnDragEnded(Vector2 position)
        {
            Plugin.PanelX.Value = position.x;
            Plugin.PanelY.Value = position.y;
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
                _previousLockState = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = _previousLockState;
                Cursor.visible = _previousCursorVisible;
            }
        }

        public void Destroy()
        {
            if (_visible)
            {
                Cursor.lockState = _previousLockState;
                Cursor.visible = _previousCursorVisible;
            }
            if (_root != null) UnityEngine.Object.Destroy(_root);
            if (_ownedEventSystem != null) UnityEngine.Object.Destroy(_ownedEventSystem);
        }

        private static GameObject MakeHorizontal(Transform parent, string name, float height, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = UiColors.Transparent;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return go;
        }

        private static GameObject MakeVertical(Transform parent, string name, float width, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
        }

        private Button MakeButton(Transform parent, string text, float width, float height,
            UnityAction onClick, int fontSize = 11)
        {
            var go = new GameObject("Button_" + text, typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            ApplyButtonTint(button, UiColors.ChromeRaised);
            var label = MakeText(go.transform, "Label", text, fontSize, FontStyles.Bold,
                UiColors.PanelText, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.raycastTarget = false;
            return button;
        }

        private Button MakeFlexButton(Transform parent, string text, UnityAction onClick, int fontSize = 11)
        {
            var button = MakeButton(parent, text, 0f, ControlHeight, onClick, fontSize);
            var element = button.GetComponent<LayoutElement>();
            element.preferredWidth = -1f;
            element.flexibleWidth = 1f;
            return button;
        }

        private void MakeActionButton(Transform parent, string text, UnityAction onClick)
        {
            var button = MakeFlexButton(parent, text, onClick, 10);
            StyleAction(button, button.GetComponentInChildren<TextMeshProUGUI>());
        }

        private TMP_InputField MakeInput(Transform parent, string placeholder)
        {
            var go = new GameObject("SearchInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = TextureFactory.CreateFramedSprite(UiColors.InstrumentWell, UiColors.Rule, 1);
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            Stretch(area.GetComponent<RectTransform>(), new Vector2(8f, 2f), new Vector2(-8f, -2f));
            var text = MakeText(area.transform, "Text", "", 10, FontStyles.Normal,
                UiColors.PanelText, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            var hint = MakeText(area.transform, "Placeholder", placeholder, 10, FontStyles.Normal,
                UiColors.PanelMuted, TextAlignmentOptions.MidlineLeft);
            Stretch(hint.rectTransform);

            var input = go.GetComponent<TMP_InputField>();
            input.textViewport = area.GetComponent<RectTransform>();
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = UiColors.Amber;
            input.selectionColor = UiColors.AmberDim;
            return input;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, int fontSize,
            FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = FontLoader.GetDefaultFont();
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.text = text;
            return label;
        }

        private static TextMeshProUGUI PlaceText(Transform parent, string name, string text, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            TextAlignmentOptions alignment, int size, FontStyles style)
        {
            var label = MakeText(parent, name, text, size, style, color, alignment);
            label.rectTransform.anchorMin = anchorMin;
            label.rectTransform.anchorMax = anchorMax;
            label.rectTransform.offsetMin = offsetMin;
            label.rectTransform.offsetMax = offsetMax;
            label.raycastTarget = false;
            return label;
        }

        private static Image PlaceImage(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void StyleToggle(Button button, TextMeshProUGUI label, bool selected)
        {
            if (button == null || label == null) return;
            ApplyButtonTint(button, selected ? UiColors.SelectionFill : UiColors.ChromeRaised);
            label.color = selected ? UiColors.PanelText : UiColors.PanelMuted;
        }

        private static void StyleAction(Button button, TextMeshProUGUI label)
        {
            if (button == null || label == null) return;
            ApplyButtonTint(button, UiColors.ChromeRaised);
            label.color = UiColors.PanelText;
            var image = button.GetComponent<Image>();
            image.sprite = TextureFactory.CreateFramedSprite(UiColors.ChromeRaised, UiColors.Rule, 1);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

        private static void ApplyButtonTint(Button button, Color baseColor)
        {
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.GetComponent<Image>().color = baseColor;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
