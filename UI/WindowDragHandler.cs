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
