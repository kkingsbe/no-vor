using System;
using System.Reflection;

namespace NOVor.Integrations
{
    internal static class ModBarBridge
    {
        private static Type _api;
        private static MethodInfo _register;
        private static MethodInfo _unregister;
        private static bool _resolved;

        private static void Resolve()
        {
            _resolved = true;
            _api = Type.GetType("NoModBar.ModBarApi, NoModBar");
            if (_api == null) return;
            _register = _api.GetMethod("Register", new[] { typeof(object) });
            _unregister = _api.GetMethod("Unregister", new[] { typeof(string) });
        }

        public static bool Register(string id, string name, string tooltip, Func<bool> isVisible, Action toggle)
        {
            if (!_resolved) Resolve();
            if (_register == null) return false;
            try
            {
                var entry = new { Id = id, Name = name, Tooltip = tooltip, IsVisible = isVisible, Toggle = toggle };
                return (bool)_register.Invoke(null, new object[] { entry });
            }
            catch
            {
                return false;
            }
        }

        public static bool Unregister(string id)
        {
            if (!_resolved) Resolve();
            if (_unregister == null) return false;
            try
            {
                return (bool)_unregister.Invoke(null, new object[] { id });
            }
            catch
            {
                return false;
            }
        }
    }
}
