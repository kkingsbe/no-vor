using UnityEngine;
using UnityEngine.UI;
using NOVor.Core;

namespace NOVor.UI
{
    public class CockpitHud : MonoBehaviour
    {
        private CdiInstrument _block;
        private HeadingTapeCues _tapeCues;

        public void Initialize(RawImage compass)
        {
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            if (_block == null)
            {
                var host = new GameObject("NOVorCdiBlock", typeof(RectTransform));
                host.transform.SetParent(transform, false);
                _block = host.AddComponent<CdiInstrument>();
            }

            if (_tapeCues == null && compass != null)
            {
                var cuesHost = new GameObject("NOVorNativeHeadingTapeCues", typeof(RectTransform));
                cuesHost.transform.SetParent(compass.rectTransform, false);
                _tapeCues = cuesHost.AddComponent<HeadingTapeCues>();
                _tapeCues.Initialize(compass);
            }
        }

        public bool NeedsTapeCues => _tapeCues == null;

        public void ApplyOffsets(float x, float y)
        {
            _block?.ApplyOffsets(x, y);
        }

        public void SetVisible(bool visible)
        {
            _block?.SetVisible(visible);
            _tapeCues?.SetVisible(visible);
        }

        public void SetData(CdiData data)
        {
            _block?.SetData(data);
            _tapeCues?.SetData(data);
        }

        private void OnDestroy()
        {
            if (_tapeCues != null) Destroy(_tapeCues.gameObject);
        }
    }
}
