using System;
using NintrollerLib;

namespace Shared
{
    public class DeviceInfo
    {
        public string DeviceID
        {
            get
            {
                return string.IsNullOrEmpty(DevicePath) ? InstanceGUID.ToString() : DevicePath;
            }
        }

        // For Wii/U Controllers
        public string DevicePath { get; set; } = string.Empty;
        public ControllerType Type { get; set; }

        // For Joysticks
        public Guid InstanceGUID { get; set; } = Guid.Empty;
        public string VID { get; set; } = string.Empty;
        public string PID { get; set; } = string.Empty;

        public bool SameDevice(string identifier)
        {
            if (!string.IsNullOrEmpty(DevicePath))
            {
                return identifier == DevicePath;
            }
            return identifier == InstanceGUID.ToString();
        }

        public bool SameDevice(Guid guid)
        {
            if (InstanceGUID != Guid.Empty)
            {
                return guid.Equals(InstanceGUID);
            }

            return false;
        }
    }
}
