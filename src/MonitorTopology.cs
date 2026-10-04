using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class MonitorTopology
    {
        internal static string Describe()
        {
            try { var screens = Screen.AllScreens; if (screens.Length == 0) return "No display surfaces reported"; return screens.Length + " display" + (screens.Length == 1 ? "" : "s") + " · " + String.Join(", ", screens.Select(s => s.Primary ? "Primary" : s.Bounds.Width + "×" + s.Bounds.Height).ToArray()); } catch { return "Unavailable"; }
        }
        internal static bool HasMultiple { get { try { return Screen.AllScreens.Length > 1; } catch { return false; } } }
    }
}
