using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace NOVor.UI
{
    public sealed class HsiCourseSelector : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private const float MinDragRadiusFraction = 0.18f;
        private RectTransform _rect;
        private bool _dragging;
        private float _lastAngle;

        public event Action<float> Delta;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!TryAngle(eventData, out _lastAngle)) return;
            _dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || !TryAngle(eventData, out float angle)) return;
            float delta = Mathf.DeltaAngle(_lastAngle, angle);
            _lastAngle = angle;
            if (Mathf.Abs(delta) > 0.001f) Delta?.Invoke(delta);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            eventData.Use();
            Delta?.Invoke(eventData.scrollDelta.y > 0f ? 1f : -1f);
        }

        private bool TryAngle(PointerEventData eventData, out float angle)
        {
            angle = 0f;
            if (_rect == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return false;
            float radius = Mathf.Min(_rect.rect.width, _rect.rect.height) * 0.5f;
            if (radius <= 0f || local.magnitude < radius * MinDragRadiusFraction) return false;
            angle = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
            return true;
        }
    }
}
