using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FunctionRowRemapper
{
    public static class KeyboardDeviceInventory
    {
        const uint RIM_TYPEKEYBOARD = 1, RIM_TYPEHID = 2, RIDI_DEVICENAME = 0x20000007;
        const uint DIGCF_PRESENT = 0x00000002, SPDRP_HARDWAREID = 0x00000001, SPDRP_DEVICEDESC = 0x00000000, SPDRP_FRIENDLYNAME = 0x0000000C, SPDRP_LOCATION = 0x0000000D;
        static readonly Guid KeyboardClassGuid = new Guid("4D36E96B-E325-11CE-BFC1-08002BE10318");
        [StructLayout(LayoutKind.Sequential)] struct Entry { public IntPtr Handle; public uint Type; }
        [StructLayout(LayoutKind.Sequential)] struct DeviceInfoData { public uint Size; public Guid ClassGuid; public uint DevInst; public IntPtr Reserved; }
        [DllImport("user32.dll", SetLastError = true)] static extern uint GetRawInputDeviceList(IntPtr list, ref uint count, uint size);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiEnumDeviceInfo(IntPtr set, uint index, ref DeviceInfoData data);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceRegistryProperty(IntPtr set, ref DeviceInfoData data, uint property, out uint propertyType, byte[] buffer, uint bufferSize, out uint requiredSize);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiGetDeviceInstanceId(IntPtr set, ref DeviceInfoData data, StringBuilder instanceId, uint capacity, out uint requiredSize);
        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool SetupDiOpenDeviceInfo(IntPtr set, string instanceId, IntPtr hwndParent, uint flags, ref DeviceInfoData data);
        [DllImport("cfgmgr32.dll", SetLastError = true)] static extern int CM_Get_Parent(out uint parent, uint devInst, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern int CM_Get_Device_ID(uint devInst, StringBuilder buffer, uint length, uint flags);
        [DllImport("setupapi.dll", SetLastError = true)] static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
        public static string Describe()
        {
            try {
                var setupLines = SetupApiLines();
                if (setupLines.Count > 0) return String.Join("\r\n", setupLines.ToArray());
                uint count = 0; uint entrySize = (uint)Marshal.SizeOf(typeof(Entry));
                if (GetRawInputDeviceList(IntPtr.Zero, ref count, entrySize) == unchecked((uint)-1) || count == 0) return "None reported";
                IntPtr rawList = Marshal.AllocHGlobal(checked((int)(count * entrySize)));
                try {
                    uint actual = count;
                    if (GetRawInputDeviceList(rawList, ref actual, entrySize) == unchecked((uint)-1)) return "Unavailable";
                    var lines = new List<string>();
                    for (uint i = 0; i < actual; i++) {
                        Entry item = Marshal.PtrToStructure<Entry>(IntPtr.Add(rawList, checked((int)(i * entrySize))));
                        if (item.Type != RIM_TYPEKEYBOARD && item.Type != RIM_TYPEHID) continue;
                        string name = Name(item.Handle); if (name.Length == 0) continue;
                        string upper = name.ToUpperInvariant(); string vid = Match(upper, "VID_([0-9A-F]{4})"), pid = Match(upper, "PID_([0-9A-F]{4})");
                        if (item.Type == RIM_TYPEHID && vid != "05AC" && upper.IndexOf("KBD", StringComparison.Ordinal) < 0 && upper.IndexOf("KEYBOARD", StringComparison.Ordinal) < 0) continue;
                        string transport = item.Type == RIM_TYPEHID || upper.StartsWith("\\\\?\\HID") ? "HID" : "keyboard";
                        string note = vid == "05AC" && pid == "0267" ? "Apple Magic Keyboard (A1644); Eject may need an Apple filter driver" : vid == "05AC" ? "Apple keyboard; Eject may need an Apple filter driver" : "Windows keyboard input";
                        lines.Add(transport + " · " + (vid.Length > 0 ? "VID " + vid + " PID " + pid + " · " : "") + note);
                    }
                    return lines.Count == 0 ? "None reported" : String.Join("\r\n", lines.ToArray());
                } finally { Marshal.FreeHGlobal(rawList); }
            } catch { return "Unavailable"; }
        }
        static List<string> SetupApiLines()
        {
            var lines = new List<string>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); Guid classGuid = KeyboardClassGuid; IntPtr set = SetupDiGetClassDevs(ref classGuid, null, IntPtr.Zero, DIGCF_PRESENT);
            if (set == IntPtr.Zero || set == new IntPtr(-1)) return lines;
            try {
                for (uint i = 0; ; i++) {
                    var data = new DeviceInfoData { Size = (uint)Marshal.SizeOf(typeof(DeviceInfoData)) };
                    if (!SetupDiEnumDeviceInfo(set, i, ref data)) break;
                    string ids = Property(set, ref data, SPDRP_HARDWAREID); if (ids.Length == 0) continue;
                    string upper = ids.ToUpperInvariant(); string vid = Match(upper, "VID_([0-9A-F]{4})"), pid = Match(upper, "PID_([0-9A-F]{4})");
                    string friendly = Property(set, ref data, SPDRP_FRIENDLYNAME); if (friendly.Length == 0) friendly = Property(set, ref data, SPDRP_DEVICEDESC);
                    string location = Property(set, ref data, SPDRP_LOCATION);
                    var instance = new StringBuilder(512); uint instanceSize; SetupDiGetDeviceInstanceId(set, ref data, instance, (uint)instance.Capacity, out instanceSize);
                    string identity = instance.ToString(); bool appleLocation = location.IndexOf("APPLE KEYBOARD", StringComparison.OrdinalIgnoreCase) >= 0;
                    string parentFriendly = "", parentIdentity = ""; uint parentDevInst;
                    if (CM_Get_Parent(out parentDevInst, data.DevInst, 0) == 0) {
                        var parentId = new StringBuilder(512); if (CM_Get_Device_ID(parentDevInst, parentId, (uint)parentId.Capacity, 0) == 0) parentIdentity = parentId.ToString();
                        var parentData = new DeviceInfoData { Size = (uint)Marshal.SizeOf(typeof(DeviceInfoData)) };
                        if (SetupDiOpenDeviceInfo(set, parentIdentity, IntPtr.Zero, 0, ref parentData)) { parentFriendly = Property(set, ref parentData, SPDRP_FRIENDLYNAME); if (parentFriendly.Length == 0) parentFriendly = Property(set, ref parentData, SPDRP_DEVICEDESC); }
                    }
                    bool appleParent = parentFriendly.IndexOf("APPLE", StringComparison.OrdinalIgnoreCase) >= 0 || parentIdentity.IndexOf("05AC", StringComparison.OrdinalIgnoreCase) >= 0;
                    string label = vid == "05AC" && pid == "0267" ? "Apple Magic Keyboard (A1644)" : appleLocation || appleParent ? "Apple Keyboard" : friendly.Length > 0 ? friendly : "Keyboard device";
                    string detail = label + (vid.Length > 0 ? " · VID " + vid + " PID " + pid : "") + (identity.Length > 0 ? " · " + identity : "");
                    if (seen.Add(detail)) lines.Add(detail);
                }
            } finally { SetupDiDestroyDeviceInfoList(set); }
            return lines;
        }
        static string Property(IntPtr set, ref DeviceInfoData data, uint property)
        {
            uint type, required; byte[] buffer = new byte[4096]; if (!SetupDiGetDeviceRegistryProperty(set, ref data, property, out type, buffer, (uint)buffer.Length, out required)) return "";
            return Encoding.Unicode.GetString(buffer).TrimEnd('\0').Replace('\0', '\n').Split('\n')[0].Trim();
        }
        static string Name(IntPtr device) { uint size = 0; GetRawInputDeviceInfo(device, RIDI_DEVICENAME, IntPtr.Zero, ref size); if (size == 0) return ""; IntPtr buffer = Marshal.AllocHGlobal((int)(size * 2)); try { if (GetRawInputDeviceInfo(device, RIDI_DEVICENAME, buffer, ref size) <= 0) return ""; return Marshal.PtrToStringUni(buffer) ?? ""; } finally { Marshal.FreeHGlobal(buffer); } }
        static string Match(string text, string pattern) { Match m = Regex.Match(text, pattern); return m.Success ? m.Groups[1].Value : ""; }
    }
}
