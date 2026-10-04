using System;
using System.Runtime.InteropServices;

namespace FunctionRowRemapper
{
    internal static class HardwareInputInventory
    {
        [DllImport("winmm.dll", EntryPoint = "midiInGetNumDevs")] static extern uint MidiInGetNumDevs();
        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern uint XInputGetState(uint userIndex, IntPtr state);
        internal static string Describe()
        {
            try { int midi = (int)MidiInGetNumDevs(), controllers = 0; IntPtr state = Marshal.AllocHGlobal(16); try { for (uint i = 0; i < 4; i++) if (XInputGetState(i, state) == 0) controllers++; } finally { Marshal.FreeHGlobal(state); } return controllers + " XInput controller" + (controllers == 1 ? "" : "s") + " · " + midi + " MIDI input device" + (midi == 1 ? "" : "s") + " · " + (GameInputSupport.IsAvailable ? "Game Input API available" : "Game Input API unavailable"); } catch { return "Unavailable"; }
        }
    }
}
