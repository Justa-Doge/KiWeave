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
            return key == 0xB8 || Shortcuts.MediaLabels.Keys.Where(name => name != "MediaEject").Any(name => (int)(Keys)Enum.Parse(typeof(Keys), name, false) == key);
        }
        public static bool IsHardwareMediaShortcut(string text)
        {
            try { HotkeyChord chord = Parse(text); return chord.Modifiers == 0 && IsHardwareMediaKey(chord.Key); }
            catch { return false; }
        }
        public static bool IsStandaloneKeyboardKey(int key)
        {
            return (key >= (int)Keys.F13 && key <= (int)Keys.F24) || key == (int)Keys.Apps || key == (int)Keys.Insert || key == (int)Keys.Delete || key == (int)Keys.Home || key == (int)Keys.End || key == (int)Keys.PageUp || key == (int)Keys.PageDown || key == (int)Keys.PrintScreen || key == (int)Keys.Scroll || key == (int)Keys.Pause || key == 0xB8;
        }
        internal static CustomHotkey FindCustomHotkey(Configuration configuration, string shortcut)
        {
            if (configuration == null || String.IsNullOrWhiteSpace(shortcut)) return null;
            string normalized; try { normalized = Normalize(shortcut); } catch { return null; }
            return (configuration.CustomHotkeys ?? new CustomHotkey[0]).FirstOrDefault(item => {
                if (item == null) return false;
                try { return String.Equals(Normalize(item.Shortcut), normalized, StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            });
        }
        internal static int ModifierChordKey(HotkeyChord chord) { return (chord.Modifiers << 16) | (chord.Key & 0xFFFF); }
        internal static bool HasMatchingModifierChord(int[] activeChordKeys, int key, Func<int, bool> isDown)
        {
            if (activeChordKeys == null || activeChordKeys.Length == 0 || isDown == null) return false;
            int modifiers = 0;
            if (isDown(0x11)) modifiers |= Ctrl;
            if (isDown(0x12)) modifiers |= Alt;
            if (isDown(0x10)) modifiers |= Shift;
            if (isDown(0x5B) || isDown(0x5C)) modifiers |= Win;
            if (modifiers == 0) return false;
            return Array.IndexOf(activeChordKeys, (modifiers << 16) | (key & 0xFFFF)) >= 0;
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
            if (modifiers == 0 && !IsHardwareMediaKey(keys[keys.Length - 1]) && !IsStandaloneKeyboardKey(keys[keys.Length - 1])) throw new ArgumentException("Custom hotkeys need Ctrl, Alt, Shift or Win. Dedicated media and extended keyboard keys can be used by themselves.");
            return new HotkeyChord { Modifiers = modifiers, Key = keys[keys.Length - 1] };
        }
        public static string Normalize(string text)
        {
            HotkeyChord h = Parse(text); var parts = new List<string>();
            if ((h.Modifiers & Ctrl) != 0) parts.Add("Ctrl"); if ((h.Modifiers & Alt) != 0) parts.Add("Alt");
            if ((h.Modifiers & Shift) != 0) parts.Add("Shift"); if ((h.Modifiers & Win) != 0) parts.Add("Win");
            string key = h.Key == 0xB8 ? "MediaEject" : ((Keys)h.Key).ToString(); if (key.StartsWith("D") && key.Length == 2) key = key.Substring(1);
            if (key == "Escape") key = "Esc"; parts.Add(key); return String.Join("+", parts);
        }
        internal static string NormalizeCustomHotkeyReplacement(Configuration configuration, int index, string proposedShortcut)
        {
            if (configuration == null) throw new ArgumentNullException("configuration");
            CustomHotkey[] hotkeys = configuration.CustomHotkeys ?? new CustomHotkey[0];
            if (index < 0 || index > hotkeys.Length) throw new ArgumentOutOfRangeException("index");
            string normalized = Normalize(proposedShortcut);
            for (int i = 0; i < hotkeys.Length; i++) {
                if (i == index || hotkeys[i] == null) continue;
                if (String.Equals(Normalize(hotkeys[i].Shortcut), normalized, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Another custom hotkey already uses " + normalized + ".", "proposedShortcut");
            }
            return normalized;
        }
    }

    internal static class HotkeyRegistration
    {
        internal const int FirstId = 3000;
        internal static bool ShouldPollForeground(bool notificationsAvailable, bool registrationNeedsRetry)
        {
            return !notificationsAvailable || registrationNeedsRetry;
        }
        internal static bool ShouldRefresh(bool? wasSuspended, bool suspended, bool registrationNeedsRetry, long now, long retryAt)
        {
            return !wasSuspended.HasValue || wasSuspended.Value != suspended || (registrationNeedsRetry && now >= retryAt);
        }
        internal static string[] Apply(Configuration configuration, IntPtr window, Dictionary<int, CustomHotkey> registered,
            bool enabled, bool suspended, Func<IntPtr, int, uint, uint, bool> register,
            Func<IntPtr, int, bool> unregister, Func<int> lastError)
        {
            if (registered == null) throw new ArgumentNullException("registered");
            if (register == null) throw new ArgumentNullException("register");
            if (unregister == null) throw new ArgumentNullException("unregister");
            if (lastError == null) throw new ArgumentNullException("lastError");

            var keep = new HashSet<int>();
            CustomHotkey[] hotkeys = configuration == null ? new CustomHotkey[0] : configuration.CustomHotkeys ?? new CustomHotkey[0];
            if (enabled && !suspended) {
                for (int i = 0; i < hotkeys.Length; i++) {
                    if (hotkeys[i] == null || hotkeys[i].Action == null || hotkeys[i].Action.Kind == ActionKind.PassThrough || HotkeyChord.IsHardwareMediaShortcut(hotkeys[i].Shortcut)) continue;
                    HotkeyChord chord; try { chord = HotkeyChord.Parse(hotkeys[i].Shortcut); } catch { continue; }
                    if (chord.Modifiers == 0) continue;
                    int id = FirstId + i; CustomHotkey old;
                    try { if (registered.TryGetValue(id, out old) && old != null &&
                            String.Equals(HotkeyChord.Normalize(old.Shortcut), HotkeyChord.Normalize(hotkeys[i].Shortcut), StringComparison.OrdinalIgnoreCase)) {
                            registered[id] = hotkeys[i].Copy(); keep.Add(id);
                        } } catch { }
                }
            }

            var failures = new List<string>();
            foreach (int id in registered.Keys.ToArray()) if (!keep.Contains(id)) {
                bool released = false;
                int releaseError = 0;
                try { released = unregister(window, id); if (!released) releaseError = lastError(); } catch { }
                // If Windows confirms there is no such registration, it is safe
                // to forget our stale bookkeeping and apply the replacement.
                if (!released && releaseError == 1419) released = true; // ERROR_HOTKEY_NOT_REGISTERED
                if (released) registered.Remove(id);
                else {
                    CustomHotkey old = registered[id];
                    keep.Add(id);
                    failures.Add(HotkeyChord.Normalize(old.Shortcut) + " could not be released; Windows may still reserve it until KiWeave closes.");
                }
            }

            if (!enabled || suspended) return failures.ToArray();
            for (int i = 0; i < hotkeys.Length; i++) {
                CustomHotkey hotkey = hotkeys[i];
                if (hotkey == null) { failures.Add("Custom hotkey " + (i + 1) + " is missing."); continue; }
                if (hotkey.Action == null || hotkey.Action.Kind == ActionKind.PassThrough) continue;
                int id = FirstId + i; if (keep.Contains(id)) continue;
                HotkeyChord chord;
                try { chord = HotkeyChord.Parse(hotkey.Shortcut); }
                catch (Exception ex) { failures.Add("Custom hotkey " + (i + 1) + " could not be parsed: " + ex.Message); continue; }
                // Modifierless media and extended keys are dispatched by the
                // low-level hook/raw-input path, not RegisterHotKey. Trying to
                // register them here can produce a false conflict warning.
                if (chord.Modifiers == 0) continue;
                if (chord.Key == (int)Keys.F12) {
                    failures.Add(HotkeyChord.Normalize(hotkey.Shortcut) + " could not be registered: F12 is reserved for debuggers by Windows.");
                    continue;
                }
                bool succeeded;
                try { succeeded = register(window, id, (uint)(chord.Modifiers | HotkeyChord.NoRepeat), (uint)chord.Key); }
                catch (Exception ex) { failures.Add(HotkeyChord.Normalize(hotkey.Shortcut) + " could not be registered: " + ex.Message); continue; }
                if (succeeded) { registered[id] = hotkey.Copy(); continue; }
                int error = 0; try { error = lastError(); } catch { }
                string message = error == 1409
                    ? "Windows or another app already owns this shortcut."
                    : error == 0 ? "Windows rejected the shortcut." : new System.ComponentModel.Win32Exception(error).Message;
                failures.Add(HotkeyChord.Normalize(hotkey.Shortcut) + " could not be registered: " + message);
            }
            return failures.ToArray();
        }
        internal static int[] ActiveModifierChordKeys(Configuration configuration, Dictionary<int, CustomHotkey> registered)
        {
            var keys = new HashSet<int>();
            CustomHotkey[] configured = configuration == null ? new CustomHotkey[0] : configuration.CustomHotkeys ?? new CustomHotkey[0];
            foreach (CustomHotkey hotkey in configured) {
                if (hotkey == null || hotkey.Action == null || hotkey.Action.Kind != ActionKind.PassThrough) continue;
                try { HotkeyChord chord = HotkeyChord.Parse(hotkey.Shortcut); if (chord.Modifiers != 0 && chord.Key >= 0x70 && chord.Key <= 0x7B) keys.Add(HotkeyChord.ModifierChordKey(chord)); }
                catch { }
            }
            if (registered != null) foreach (CustomHotkey item in registered.Values) {
                if (item == null) continue;
                CustomHotkey active = HotkeyChord.FindCustomHotkey(configuration, item.Shortcut);
                if (active == null || active.Action == null || active.Action.Kind == ActionKind.PassThrough) continue;
                try { HotkeyChord chord = HotkeyChord.Parse(active.Shortcut); if (chord.Modifiers != 0 && chord.Key >= 0x70 && chord.Key <= 0x7B) keys.Add(HotkeyChord.ModifierChordKey(chord)); }
                catch { }
            }
            return keys.ToArray();
        }
    }
}
