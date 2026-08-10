using TMPro;
using UnityEngine;

namespace NOVor.UI
{
    internal static class FontLoader
    {
        private static TMP_FontAsset _cached;

        public static TMP_FontAsset GetDefaultFont()
        {
            if (_cached != null) return _cached;
            if (TMP_Settings.defaultFontAsset != null)
            {
                _cached = TMP_Settings.defaultFontAsset;
                return _cached;
            }
            var fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (fallback != null) _cached = fallback;
            return _cached;
        }
    }
}
