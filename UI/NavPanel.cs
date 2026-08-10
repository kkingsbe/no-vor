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
