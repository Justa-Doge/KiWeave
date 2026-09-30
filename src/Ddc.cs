using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FunctionRowRemapper
{
    public sealed class DdcOperation
    {
        public string Id, Label; public byte Code; public int Direction;
        public override string ToString() { return Label; }
        public static readonly DdcOperation[] All = {
            new DdcOperation { Id = "BrightnessUp", Label = "Brightness up", Code = 0x10, Direction = 1 },
            new DdcOperation { Id = "BrightnessDown", Label = "Brightness down", Code = 0x10, Direction = -1 },
            new DdcOperation { Id = "ContrastUp", Label = "Contrast up", Code = 0x12, Direction = 1 },
            new DdcOperation { Id = "ContrastDown", Label = "Contrast down", Code = 0x12, Direction = -1 },
            new DdcOperation { Id = "VolumeUp", Label = "Monitor volume up", Code = 0x62, Direction = 1 },
            new DdcOperation { Id = "VolumeDown", Label = "Monitor volume down", Code = 0x62, Direction = -1 }
        };
        public static DdcOperation Find(string id) { return All.FirstOrDefault(o => o.Id == id); }
        public static uint Next(uint current, uint maximum, int step, int direction)
        {
            if (maximum == 0 || maximum > 65535 || current > maximum || step < 1 || step > 20 || (direction != -1 && direction != 1)) throw new ArgumentException("Invalid monitor range or step.");
            long delta = Math.Max(1, (long)Math.Round(maximum * step / 100.0, MidpointRounding.AwayFromZero));
            return (uint)Math.Max(0, Math.Min(maximum, (long)current + direction * delta));
        }
    }
    public sealed class DdcMonitor
    {
        public string Id, Name, Status; public byte[] Codes = new byte[0];
        public override string ToString() { return Name + (Codes.Length == 0 ? " (unavailable)" : ""); }
    }
    public interface IDdcController { void Apply(Mapping mapping, Func<bool> stillActive); }

    // All dxva2 I/O is serialized off the keyboard/UI threads. Handles are scoped to each operation.
    public sealed class DdcService : IDdcController
    {
        public static readonly DdcService Shared = new DdcService();
        readonly object gate = new object();
        DdcMonitor[] monitors = new DdcMonitor[0];
        static readonly byte[] AllowedCodes = { 0x10, 0x12, 0x62 };
        public DdcMonitor[] Scan()
        {
            lock (gate) {
                var found = new List<DdcMonitor>();
                using (var list = PhysicalSet.Open()) foreach (var p in list.Items) {
                    var supported = new List<byte>(); string caps = ReadCapabilities(p.Handle);
                    var model = Regex.Match(caps ?? "", @"\bmodel\(([^()]{1,80})\)", RegexOptions.IgnoreCase);
                    if (model.Success) p.Name = model.Groups[1].Value.Trim() + " " + p.Name.Substring(p.Name.LastIndexOf('('));
                    HashSet<byte> advertised = ParseVcpCodes(caps);
                    foreach (byte code in AllowedCodes) {
                        if (advertised != null && !advertised.Contains(code)) continue;
                        uint type, current, maximum;
                        if (GetVCPFeatureAndVCPFeatureReply(p.Handle, code, out type, out current, out maximum) && maximum > 0 && maximum <= 65535 && current <= maximum) supported.Add(code);
                    }
                    found.Add(new DdcMonitor { Id = p.Id, Name = p.Name, Codes = supported.ToArray(), Status = supported.Count == 0 ? "No supported DDC/CI controls responded. Check the monitor's DDC/CI setting and cable." : "Detected: " + String.Join(", ", supported.Select(c => c == 0x10 ? "brightness" : c == 0x12 ? "contrast" : "monitor volume")) + "." });
                }
                monitors = found.ToArray(); return monitors.ToArray();
            }
        }
        public void Apply(Mapping mapping, Func<bool> stillActive)
        {
            ConfigStore.Validate(mapping, false);
            lock (gate) {
                if (!stillActive()) return;
                var known = monitors.FirstOrDefault(m => m.Id == mapping.MonitorId);
                var operation = DdcOperation.Find(mapping.MonitorControl);
                if (known == null || !known.Codes.Contains(operation.Code)) throw new InvalidOperationException("Monitor control unavailable. Open settings and click Detect monitors.");
                using (var list = PhysicalSet.Open()) {
                    var p = list.Items.SingleOrDefault(m => m.Id == mapping.MonitorId);
                    if (p == null) throw new InvalidOperationException("The selected monitor is disconnected. Reconnect it or choose another monitor.");
                    uint type, current, maximum;
                    if (!GetVCPFeatureAndVCPFeatureReply(p.Handle, operation.Code, out type, out current, out maximum)) throw new InvalidOperationException("The monitor stopped responding to DDC/CI. Check its power, cable and DDC/CI setting.");
                    uint next = DdcOperation.Next(current, maximum, mapping.MonitorStep, operation.Direction);
                    if (!stillActive() || next == current) return;
                    if (!SetVCPFeature(p.Handle, operation.Code, next)) throw new InvalidOperationException("The monitor rejected the DDC/CI adjustment.");
                }
            }
        }
        // Parse only top-level tokens within vcp(...), excluding enumerated values such as 60(10 12).
        internal static HashSet<byte> ParseVcpCodes(string caps)
        {
            if (String.IsNullOrWhiteSpace(caps)) return null;
            Match m = Regex.Match(caps, @"\bvcp\s*\(", RegexOptions.IgnoreCase);
            if (!m.Success) return null;
            var result = new HashSet<byte>(); int depth = 1; int i = m.Index + m.Length;
            while (i < caps.Length && depth > 0) {
                char c = caps[i];
                if (c == '(') { depth++; i++; } else if (c == ')') { depth--; i++; }
                else if (depth == 1 && Uri.IsHexDigit(c)) {
                    int start = i; while (i < caps.Length && Uri.IsHexDigit(caps[i])) i++;
                    byte code; if (i - start != 2 || !Byte.TryParse(caps.Substring(start, i - start), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out code)) return null;
                    result.Add(code);
                } else if (depth == 1 && !Char.IsWhiteSpace(c)) return null; else i++;
            }
            return depth == 0 ? result : null;
        }
        static string ReadCapabilities(IntPtr handle)
        {
            uint length;
            if (!GetCapabilitiesStringLength(handle, out length) || length == 0 || length > 65536) return null;
            var text = new StringBuilder((int)length);
            return CapabilitiesRequestAndCapabilitiesReply(handle, text, length) ? text.ToString() : null;
        }
        // Explicit integration test only: a small decrement with read-back and unconditional restoration.
        // The normal application never invokes this routine.
        internal string VerifyHardwareRoundTrip(byte code)
        {
            if (!AllowedCodes.Contains(code)) throw new ArgumentException("Unsupported test control.");
            lock (gate) {
                var known = monitors.FirstOrDefault(m => m.Codes.Contains(code));
                if (known == null) throw new InvalidOperationException("No detected monitor supports this control.");
                using (var list = PhysicalSet.Open()) {
                    var p = list.Items.Single(m => m.Id == known.Id); uint type, before, maximum;
                    if (!GetVCPFeatureAndVCPFeatureReply(p.Handle, code, out type, out before, out maximum) || maximum == 0 || before > maximum) throw new InvalidOperationException("Cannot read original monitor setting.");
                    // At zero, avoid raising volume during a test; the unchanged write path is still checked.
                    uint next = before == 0 ? before : before - 1;
                    try {
                        if (!SetVCPFeature(p.Handle, code, next)) throw new InvalidOperationException("DDC/CI test write rejected.");
                        System.Threading.Thread.Sleep(150);
                        uint observed, max;
                        if (!GetVCPFeatureAndVCPFeatureReply(p.Handle, code, out type, out observed, out max) || observed != next) throw new InvalidOperationException("DDC/CI change read-back did not match.");
                    } finally {
                        if (!SetVCPFeature(p.Handle, code, before)) throw new InvalidOperationException("Monitor restoration failed. Restore the original value " + before + " in the monitor menu.");
                        System.Threading.Thread.Sleep(150);
                        uint restored, max;
                        if (!GetVCPFeatureAndVCPFeatureReply(p.Handle, code, out type, out restored, out max) || restored != before) throw new InvalidOperationException("Could not verify monitor restoration to " + before + ".");
                    }
                    return known.Name + ": VCP " + code.ToString("X2") + " " + before + " -> " + next + " -> " + before + " (restored and verified)";
                }
            }
        }
        sealed class Physical
        {
            public IntPtr Handle; public string Id, Name;
        }
        sealed class PhysicalSet : IDisposable
        {
            public readonly List<Physical> Items = new List<Physical>();
            public static PhysicalSet Open()
            {
                var set = new PhysicalSet(); Exception failure = null;
                MonitorEnum callback = delegate(IntPtr h, IntPtr dc, ref Rect r, IntPtr data) {
                    try {
                        var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                        if (!GetMonitorInfo(h, ref info)) return true;
                        var device = new DisplayDevice { Size = Marshal.SizeOf(typeof(DisplayDevice)) };
                        // Multiple physical monitors sharing one logical handle have no guaranteed identity ordering.
                        uint count;
                        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(h, out count) || count != 1 || !EnumDisplayDevices(info.Device, 0, ref device, 1) || String.IsNullOrEmpty(device.DeviceId)) return true;
                        var physical = new PhysicalMonitor[1];
                        if (!GetPhysicalMonitorsFromHMONITOR(h, 1, physical)) return true;
                        try {
                            string id;
                            using (var sha = SHA256.Create()) id = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(device.DeviceId.ToUpperInvariant()))).Replace("-", "").ToLowerInvariant();
                            set.Items.Add(new Physical { Handle = physical[0].Handle, Id = id, Name = physical[0].Description + " (" + info.Device.Replace(@"\\.\", "") + ")" });
                        } catch { DestroyPhysicalMonitor(physical[0].Handle); throw; }
                        return true;
                    } catch (Exception ex) { failure = ex; return false; }
                };
                try {
                    if (!EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)) throw new InvalidOperationException("Could not enumerate connected monitors.", failure);
                    // Never silently target an ambiguous identity.
                    foreach (var duplicate in set.Items.GroupBy(p => p.Id).Where(g => g.Count() > 1).ToArray()) foreach (var p in duplicate.ToArray()) { DestroyPhysicalMonitor(p.Handle); set.Items.Remove(p); }
                    return set;
                } catch { set.Dispose(); throw; }
            }
            public void Dispose() { foreach (var p in Items) DestroyPhysicalMonitor(p.Handle); Items.Clear(); }
        }
        [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct DisplayDevice { public int Size; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct PhysicalMonitor { public IntPtr Handle; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description; }
        delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnum callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice output, uint flags);
        [DllImport("dxva2.dll")] static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
        [DllImport("dxva2.dll", CharSet = CharSet.Unicode)] static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] physical);
        [DllImport("dxva2.dll")] static extern bool DestroyPhysicalMonitor(IntPtr monitor);
        [DllImport("dxva2.dll")] static extern bool GetCapabilitiesStringLength(IntPtr monitor, out uint length);
        [DllImport("dxva2.dll", CharSet = CharSet.Ansi)] static extern bool CapabilitiesRequestAndCapabilitiesReply(IntPtr monitor, StringBuilder text, uint length);
        [DllImport("dxva2.dll")] static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr monitor, byte code, out uint type, out uint current, out uint maximum);
        [DllImport("dxva2.dll")] static extern bool SetVCPFeature(IntPtr monitor, byte code, uint value);
    }
}
