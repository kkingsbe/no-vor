using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    internal sealed class KeyBindingPanel
    {
        private const float RowHeight = 32f;

        private sealed class BindingRow
        {
            public string Name;
            public ConfigEntry<KeyboardShortcut> Entry;
            public ConfigEntry<string> Hotas;
            public Button CaptureButton;
            public TextMeshProUGUI ValueLabel;
        }

        private readonly List<BindingRow> _rows = new List<BindingRow>();
        private GameObject _root;
        private BindingRow _capturing;
        private Action _closeRequested;

        public bool IsVisible { get; private set; }

        public void Create(Transform parent, Action closeRequested)
        {
            _closeRequested = closeRequested;
            _root = new GameObject("Controls", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(LayoutElement));
            _root.transform.SetParent(parent, false);
            _root.GetComponent<Image>().color = UiColors.Chrome;
            var element = _root.GetComponent<LayoutElement>();
            element.preferredHeight = 372f;
            element.flexibleHeight = 0f;
            var layout = _root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = MakeHorizontal(_root.transform, "Title", 28f, 6f);
            var label = MakeText(title.transform, "Label", "CONTROLS", 15, FontStyles.Bold,
                UiColors.Amber, TextAlignmentOptions.MidlineLeft);
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            MakeButton(title.transform, "CLOSE", 58f, 26f, () => _closeRequested?.Invoke(), 9);

            var hint = MakeText(_root.transform, "Hint",
                "Click a binding, then press a key/chord or HOTAS button. Esc cancels.",
                10, FontStyles.Normal, UiColors.PanelSecondaryText, TextAlignmentOptions.MidlineLeft);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;

            var columns = MakeHorizontal(_root.transform, "Columns", 268f, 10f);
            var left = MakeVertical(columns.transform, "Left");
            var right = MakeVertical(columns.transform, "Right");
            AddBinding(left.transform, "NEXT AIRPORT", Plugin.NextAirportKey, Plugin.HotasNextAirport);
            AddBinding(left.transform, "PREVIOUS AIRPORT", Plugin.PrevAirportKey, Plugin.HotasPrevAirport);
            AddBinding(left.transform, "SHOW / HIDE HUD", Plugin.ToggleHudKey, Plugin.HotasToggleHud);
            AddBinding(left.transform, "OPEN / CLOSE PANEL", Plugin.ToggleMenuKey, Plugin.HotasToggleMenu);
            AddBinding(left.transform, "COURSE DECREASE", Plugin.CourseDecreaseKey, Plugin.HotasCourseDecrease);
            AddBinding(left.transform, "COURSE INCREASE", Plugin.CourseIncreaseKey, Plugin.HotasCourseIncrease);
            AddBinding(right.transform, "DIRECT TO FIELD", Plugin.DirectToKey, Plugin.HotasDirectTo);
            AddBinding(right.transform, "HUD NUDGE UP", Plugin.HudNudgeUpKey, Plugin.HotasHudNudgeUp);
            AddBinding(right.transform, "HUD NUDGE DOWN", Plugin.HudNudgeDownKey, Plugin.HotasHudNudgeDown);
            AddBinding(right.transform, "HUD NUDGE LEFT", Plugin.HudNudgeLeftKey, Plugin.HotasHudNudgeLeft);
            AddBinding(right.transform, "HUD NUDGE RIGHT", Plugin.HudNudgeRightKey, Plugin.HotasHudNudgeRight);

            var footer = MakeText(_root.transform, "Footer",
                "HOTAS BUTTONS ONLY — ANALOG AXES AND POV HATS ARE NOT SUPPORTED",
                9, FontStyles.Bold, UiColors.PanelDisabledText, TextAlignmentOptions.MidlineLeft);
            footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 18f;
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            if (!visible) CancelCapture();
            if (_root != null) _root.SetActive(visible);
            if (visible) RefreshRows();
            else ClearOwnedSelection();
        }

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
                return true;
            }
            if (HotasInput.TryCapture(out HotasBinding hotas))
            {
                BindingRow row = _capturing;
                _capturing = null;
                row.Hotas.Value = HotasBinding.Serialize(hotas);
                Plugin.Log?.LogInfo("NO VOR: " + row.Name + " bound to " + HotasBinding.Format(hotas));
                RefreshRows();
                return true;
            }
            return true;
        }

        public void CancelCapture()
        {
            if (_capturing == null) return;
            _capturing = null;
            RefreshRows();
        }

        private void AddBinding(Transform parent, string name, ConfigEntry<KeyboardShortcut> entry,
            ConfigEntry<string> hotas)
        {
            var row = MakeHorizontal(parent, "Binding_" + name, RowHeight, 4f);
            var nameLabel = MakeText(row.transform, "Name", name, 9, FontStyles.Bold,
                UiColors.PanelSecondaryText, TextAlignmentOptions.MidlineLeft);
            nameLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 118f;
            var binding = new BindingRow { Name = name, Entry = entry, Hotas = hotas };
            binding.CaptureButton = MakeButton(row.transform, "", 0f, 26f,
                () => BeginCapture(binding), 9);
            binding.CaptureButton.GetComponent<LayoutElement>().flexibleWidth = 1f;
            binding.ValueLabel = binding.CaptureButton.GetComponentInChildren<TextMeshProUGUI>();
            MakeButton(row.transform, "CLEAR", 42f, 26f, () => ClearBinding(binding), 8);
            _rows.Add(binding);
        }

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
            row.Hotas.Value = "";
            RefreshRows();
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                BindingRow row = _rows[i];
                bool capturing = row == _capturing;
                row.ValueLabel.text = capturing ? "PRESS KEY / HOTAS…" : Format(row);
                row.ValueLabel.color = capturing ? UiColors.Amber :
                    IsBound(row) ? UiColors.PanelText : UiColors.PanelDisabledText;
                row.CaptureButton.GetComponent<Image>().color = capturing
                    ? UiColors.SelectionSurface : UiColors.ChromeRaised;
            }
        }

        private static string Format(BindingRow row)
        {
            string kb = row.Entry.Value.MainKey == KeyCode.None ? null : row.Entry.Value.ToString();
            string hotas = HotasInput.IsBound(row.Hotas.Value) ? HotasInput.Label(row.Hotas.Value) : null;
            if (kb == null && hotas == null) return "<NOT BOUND>";
            if (kb == null) return hotas;
            if (hotas == null) return kb;
            return kb + " + " + hotas;
        }

        private static bool IsBound(BindingRow row)
        {
            return row.Entry.Value.MainKey != KeyCode.None || HotasInput.IsBound(row.Hotas.Value);
        }

        private void ClearOwnedSelection()
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (selected != null && _root != null && selected.transform.IsChildOf(_root.transform))
                eventSystem.SetSelectedGameObject(null);
        }

        private static GameObject MakeHorizontal(Transform parent, string name, float height, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            return go;
        }

        private static GameObject MakeVertical(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
        }

        private static Button MakeButton(Transform parent, string text, float width, float height,
            UnityAction action, int fontSize)
        {
            var go = new GameObject("Button_" + text, typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            var image = go.GetComponent<Image>();
            image.color = UiColors.ChromeRaised;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(action);
            var label = MakeText(go.transform, "Label", text, fontSize, FontStyles.Bold,
                UiColors.PanelText, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.raycastTarget = false;
            return button;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, string text, int size,
            FontStyles style, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = FontLoader.GetDefaultFont();
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
