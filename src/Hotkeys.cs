using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    public sealed class HotkeyChord
    {
        public const int Alt = 1, Ctrl = 2, Shift = 4, Win = 8, NoRepeat = 0x4000;
        public int Modifiers; public int Key;
        public static bool IsHardwareMediaKey(int key)
        {
            return Shortcuts.MediaLabels.Keys.Any(name => (int)(Keys)Enum.Parse(typeof(Keys), name, false) == key);
        }
        public static bool IsHardwareMediaShortcut(string text)
        {
            try { HotkeyChord chord = Parse(text); return chord.Modifiers == 0 && IsHardwareMediaKey(chord.Key); }
            catch { return false; }
        }
        public static HotkeyChord Parse(string text)
        {
            int[] keys = Shortcuts.Parse(text, false);
            int modifiers = 0;
            foreach (int k in keys.Take(keys.Length - 1)) {
                if (k == 0x11) modifiers |= Ctrl;
                else if (k == 0x12) modifiers |= Alt;
                else if (k == 0x10) modifiers |= Shift;
                else if (k == 0x5B) modifiers |= Win;
            }
            if (modifiers == 0 && !IsHardwareMediaKey(keys[keys.Length - 1])) throw new ArgumentException("Custom hotkeys need Ctrl, Alt, Shift or Win. Dedicated media buttons can be used by themselves.");
            return new HotkeyChord { Modifiers = modifiers, Key = keys[keys.Length - 1] };
        }
        public static string Normalize(string text)
        {
            HotkeyChord h = Parse(text); var parts = new List<string>();
            if ((h.Modifiers & Ctrl) != 0) parts.Add("Ctrl"); if ((h.Modifiers & Alt) != 0) parts.Add("Alt");
            if ((h.Modifiers & Shift) != 0) parts.Add("Shift"); if ((h.Modifiers & Win) != 0) parts.Add("Win");
            string key = ((Keys)h.Key).ToString(); if (key.StartsWith("D") && key.Length == 2) key = key.Substring(1);
            if (key == "Escape") key = "Esc"; parts.Add(key); return String.Join("+", parts);
        }
    }
}
