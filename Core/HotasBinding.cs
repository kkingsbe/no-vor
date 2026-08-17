using System;

namespace NOVor.Core
{
    public struct HotasBinding
    {
        private const int FieldCount = 3;

        public string DeviceName;
        public Guid DeviceGuid;
        public int DeviceId;
        public int ButtonIndex;

        public static readonly HotasBinding Empty;

        public bool IsEmpty
        {
            get { return DeviceGuid == Guid.Empty; }
        }

        public static string Serialize(HotasBinding binding)
        {
            if (binding.IsEmpty) return "";
            return binding.DeviceName + "|" + binding.DeviceGuid + "|" + binding.DeviceId +
                "|" + binding.ButtonIndex;
        }

        public static bool TryParse(string value, out HotasBinding binding)
        {
            binding = Empty;
            if (string.IsNullOrEmpty(value)) return false;
            string[] parts = value.Split('|');
            if (parts.Length < FieldCount) return false;
            int button;
            int deviceId;
            Guid guid;
            if (!int.TryParse(parts[parts.Length - 1], out button)) return false;
            if (!int.TryParse(parts[parts.Length - 2], out deviceId)) return false;
            if (!Guid.TryParse(parts[parts.Length - FieldCount], out guid)) return false;
            binding = new HotasBinding
            {
                DeviceName = string.Join("|", parts, 0, parts.Length - FieldCount),
                DeviceGuid = guid,
                DeviceId = deviceId,
                ButtonIndex = button
            };
            return true;
        }

        public static string Format(HotasBinding binding)
        {
            if (binding.IsEmpty) return "<NOT BOUND>";
            string name = string.IsNullOrEmpty(binding.DeviceName) ? "JOYSTICK" : binding.DeviceName;
            return name + " B" + (binding.ButtonIndex + 1);
        }
    }
}