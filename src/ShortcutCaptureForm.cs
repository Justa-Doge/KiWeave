using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class ShortcutCapture
    {
        internal static string Normalize(bool ctrl, bool alt, bool shift, bool win, int virtualKey, bool requireModifier)
        {
            if (virtualKey < 8 || virtualKey > 254 || IsModifier(virtualKey)) throw new ArgumentException("Press a regular key after the modifiers.");
            var parts = new List<string>();
            if (ctrl) parts.Add("Ctrl"); if (alt) parts.Add("Alt"); if (shift) parts.Add("Shift"); if (win) parts.Add("Win");
            if (requireModifier && parts.Count == 0 && !HotkeyChord.IsHardwareMediaKey(virtualKey)) throw new ArgumentException("Custom hotkeys need Ctrl, Alt, Shift or Win. Dedicated media buttons can be used by themselves.");
            string key = ((Keys)virtualKey).ToString();
            if (key.StartsWith("D", StringComparison.Ordinal) && key.Length == 2 && Char.IsDigit(key[1])) key = key.Substring(1);
            if (key == "Escape") key = "Esc";
            parts.Add(key);
            string value = String.Join("+", parts);
            Shortcuts.Parse(value, false);
            return requireModifier ? HotkeyChord.Normalize(value) : value;
        }
        internal static bool IsModifier(int key)
        {
            return key == 0x10 || key == 0x11 || key == 0x12 || key == 0x5B || key == 0x5C || (key >= 0xA0 && key <= 0xA5);
        }
        internal static bool IsCtrl(int key) { return key == 0x11 || key == 0xA2 || key == 0xA3; }
        internal static bool IsAlt(int key) { return key == 0x12 || key == 0xA4 || key == 0xA5; }
        internal static bool IsShift(int key) { return key == 0x10 || key == 0xA0 || key == 0xA1; }
        internal static bool IsWin(int key) { return key == 0x5B || key == 0x5C; }
    }

    internal sealed class ShortcutCaptureForm : Form
    {
        readonly bool requireModifier, previewOnly;
        readonly Label chord = new DesignLabel(), feedback = new DesignLabel();
        readonly Button accept;
        readonly HashSet<int> held = new HashSet<int>();
        Native.HookProc callback;
        IntPtr hook;
        string candidate = "";
        internal string Result { get; private set; }

        internal ShortcutCaptureForm(bool customHotkey) : this(customHotkey, false) { }
        internal ShortcutCaptureForm(bool customHotkey, bool preview)
        {
            requireModifier = customHotkey; previewOnly = preview;
            Text = "KiWeave shortcut capture"; Icon = Program.AppIcon(); Font = new Font("Segoe UI", 10); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Design.DarkTitlebar(this);
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(650, 440); MinimumSize = new Size(660, 470); MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 4 };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58)); Controls.Add(root);
            var header = UiStyle.Stack(); header.Controls.Add(UiStyle.Text("Record shortcut", 22, true));
            header.Controls.Add(UiStyle.Text(requireModifier ? "Hold one or more modifiers, then press the final key, or press a dedicated media button by itself." : "Press the key or shortcut you want this action to send.", 10, false)); root.Controls.Add(header, 0, 0);
            var card = new DesignCard { Dock = DockStyle.Fill, Margin = new Padding(0, 20, 0, 14) }; root.Controls.Add(card, 0, 1);
            var values = UiStyle.Stack(); values.Dock = DockStyle.Fill; card.Controls.Add(values);
            var current = UiStyle.Text("Captured shortcut", 9, true); values.Controls.Add(current);
            chord.AutoSize = true; chord.Font = new Font("Segoe UI", 22, FontStyle.Bold); chord.ForeColor = UiStyle.Blue; chord.Text = "Waiting for input…"; chord.Margin = new Padding(0, 8, 0, 18); values.Controls.Add(chord);
            feedback.AutoSize = true; feedback.ForeColor = UiStyle.Muted; feedback.MaximumSize = new Size(540, 0); feedback.Text = "Nothing is saved until you review the combination and choose Use shortcut."; values.Controls.Add(feedback);
            var privacy = UiStyle.Text("Private by design: capture exists only in this window, keeps no history, writes no log, and suppresses the keys so the shortcut does not activate while you record it. Escape by itself cancels.", 9, false);
            privacy.ForeColor = UiStyle.Muted; privacy.MaximumSize = new Size(590, 0); root.Controls.Add(privacy, 0, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            accept = UiStyle.Button("Use shortcut", delegate { if (!String.IsNullOrEmpty(candidate)) { Result = candidate; DialogResult = DialogResult.OK; Close(); } }, true); accept.Enabled = false; buttons.Controls.Add(accept);
            buttons.Controls.Add(UiStyle.Button("Cancel", delegate { DialogResult = DialogResult.Cancel; Close(); })); root.Controls.Add(buttons, 0, 3);
            Shown += delegate { if (!previewOnly) StartCapture(); };
            FormClosed += delegate { StopCapture(); held.Clear(); candidate = ""; chord.Text = feedback.Text = ""; };
        }

        void StartCapture()
        {
            callback = HandleKey;
            hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero) { feedback.Text = "KiWeave could not start temporary shortcut capture. Close this window and try again."; feedback.ForeColor = Color.FromArgb(255, 151, 153); }
        }
        void StopCapture() { if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; } callback = null; }
        IntPtr HandleKey(int code, IntPtr message, IntPtr dataPointer)
        {
            if (code < 0 || dataPointer == IntPtr.Zero) return Native.CallNextHookEx(hook, code, message, dataPointer);
            var data = (Native.KeyboardData)Marshal.PtrToStructure(dataPointer, typeof(Native.KeyboardData));
            if ((data.Flags & 0x10) != 0) return Native.CallNextHookEx(hook, code, message, dataPointer);
            int key = (int)data.Vk, messageId = message.ToInt32();
            bool down = messageId == 0x100 || messageId == 0x104, up = messageId == 0x101 || messageId == 0x105;
            if (!down && !up) return Native.CallNextHookEx(hook, code, message, dataPointer);
            if (down) {
                held.Add(key);
                if (ShortcutCapture.IsModifier(key)) {
                    chord.Text = ModifierText(); feedback.Text = "Keep holding the modifiers and press the final key."; feedback.ForeColor = UiStyle.Muted;
                } else if (key == 0x1B && !AnyModifier()) {
                    BeginInvoke((Action)delegate { DialogResult = DialogResult.Cancel; Close(); });
                } else {
                    try {
                        candidate = ShortcutCapture.Normalize(held.Any(ShortcutCapture.IsCtrl), held.Any(ShortcutCapture.IsAlt), held.Any(ShortcutCapture.IsShift), held.Any(ShortcutCapture.IsWin), key, requireModifier);
                        chord.Text = candidate; feedback.Text = "Captured. Release the keys, review the combination, then choose Use shortcut."; feedback.ForeColor = UiStyle.Muted; accept.Enabled = true;
                    } catch (ArgumentException ex) { candidate = ""; chord.Text = "Not accepted"; feedback.Text = ex.Message; feedback.ForeColor = Color.FromArgb(255, 151, 153); accept.Enabled = false; }
                }
            }
            if (up) held.Remove(key);
            return new IntPtr(1);
        }
        bool AnyModifier() { return held.Any(ShortcutCapture.IsCtrl) || held.Any(ShortcutCapture.IsAlt) || held.Any(ShortcutCapture.IsShift) || held.Any(ShortcutCapture.IsWin); }
        string ModifierText()
        {
            var names = new List<string>(); if (held.Any(ShortcutCapture.IsCtrl)) names.Add("Ctrl"); if (held.Any(ShortcutCapture.IsAlt)) names.Add("Alt"); if (held.Any(ShortcutCapture.IsShift)) names.Add("Shift"); if (held.Any(ShortcutCapture.IsWin)) names.Add("Win");
            return names.Count == 0 ? "Waiting for input…" : String.Join("+", names) + "+…";
        }
    }
}
