using System;
using System.Reflection;

namespace NOVor.Integrations
{
    internal static class ModBarBridge
    {
        private static Type _api;
        private static MethodInfo _register;
        private static MethodInfo _unregister;

        public static bool Register(string id, string name, string tooltip, Func<bool> isVisible, Action toggle)
        {
            Resolve();
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
            Resolve();
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

        private static void Resolve()
        {
            if (_api != null) return;
            var asm = FindApiAssembly();
            if (asm == null) return;
            _api = asm.GetType("NoModBar.Core.ModBarApi");
            if (_api == null) return;
            _register = _api.GetMethod("Register", new[] { typeof(object) });
            _unregister = _api.GetMethod("Unregister", new[] { typeof(string) });
        }

        private static Assembly FindApiAssembly()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    if (assemblies[i].GetName().Name == "NoModBar.Core")
                        return assemblies[i];
                }
                catch
                {
                }
            }
            return null;
        }
    }
}
