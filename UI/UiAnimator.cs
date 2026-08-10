using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace NOVor.UI
{
    public class UiAnimator : MonoBehaviour
    {
        private RawImage _scanlineImage;
        private float _scrollSpeed = 0.5f;

        public void Init(RawImage scanlineImage)
        {
            _scanlineImage = scanlineImage;
            if (_scanlineImage != null)
                StartCoroutine(ScrollScanlines());
        }

        private IEnumerator ScrollScanlines()
        {
            var uvRect = _scanlineImage.uvRect;
            while (true)
            {
                uvRect.y += Time.deltaTime * _scrollSpeed;
                _scanlineImage.uvRect = uvRect;
                yield return null;
            }
        }
    }
}
