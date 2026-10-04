using System;

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
    }
}
