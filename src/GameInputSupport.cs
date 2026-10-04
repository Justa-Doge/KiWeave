using System;
using System.Collections;
using System.Reflection;

namespace FunctionRowRemapper
{
    internal static class GameInputSupport
    {
        internal static bool IsAvailable
        {
            get { try { return Type.GetType("Windows.Gaming.Input.Gamepad, Windows, ContentType=WindowsRuntime") != null || Type.GetType("Windows.Gaming.Input.Gamepad, Windows.Gaming.Input, ContentType=WindowsRuntime") != null; } catch { return false; } }
        }
        internal static string Describe()
        {
            return IsAvailable ? "Windows Game Input API is available; XInput trigger polling remains the safe active backend." : "Windows Game Input API is unavailable on this runtime; XInput remains the active controller backend.";
        }
        internal static bool TryReadButtons(int userIndex, out ushort buttons)
        {
            buttons = 0;
            try {
                Type type = Type.GetType("Windows.Gaming.Input.Gamepad, Windows, ContentType=WindowsRuntime") ?? Type.GetType("Windows.Gaming.Input.Gamepad, Windows.Gaming.Input, ContentType=WindowsRuntime");
                if (type == null) return false;
                object gamepads = type.GetProperty("Gamepads", BindingFlags.Public | BindingFlags.Static).GetValue(null, null); var list = gamepads as IEnumerable; if (list == null) return false;
                int index = 0; foreach (object pad in list) { if (index++ != userIndex) continue; object reading = pad.GetType().GetMethod("GetCurrentReading", Type.EmptyTypes).Invoke(pad, null); object value = reading.GetType().GetProperty("Buttons").GetValue(reading, null); buttons = unchecked((ushort)Convert.ToUInt64(value)); return true; }
            } catch { }
            return false;
        }
    }
}
