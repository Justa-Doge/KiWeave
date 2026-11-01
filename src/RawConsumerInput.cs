using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal sealed class RawConsumerInput : IDisposable
    {
        const int WmInput = 0x00FF;
        const uint RidInput = 0x10000003;
        const uint RidevRemove = 0x00000001;
        const uint RidevInputSink = 0x00000100;
        const uint RidevDevNotify = 0x00002000;
        const int WmInputDeviceChange = 0x00FE;
        const int GidcRemoval = 2;
        const uint GetPreparsedData = 0x20000005;
        const uint MaxInputBytes = 1024 * 1024;
        const uint MaxUsageCount = 4096;
        const int HidpStatusSuccess = 0x00110000;
        const ushort ConsumerPage = 0x0C;

        readonly Action<string> callback;
        readonly ConsumerUsageState usageState = new ConsumerUsageState();
        bool active;

        [StructLayout(LayoutKind.Sequential)]
        internal struct DeviceRegistration { public ushort Page, Usage; public uint Flags; public IntPtr Target; }

        [StructLayout(LayoutKind.Sequential)]
        struct RawInputHeader { public uint Type, Size; public IntPtr Device, WParam; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterRawInputDevices(DeviceRegistration[] devices, uint count, uint size);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint GetRawInputDeviceInfo(IntPtr device, uint command, IntPtr data, ref uint size);

        [DllImport("hid.dll")]
        static extern int HidP_GetUsages(ushort reportType, ushort usagePage, ushort linkCollection, ushort[] usages, ref uint usageCount, IntPtr preparsedData, IntPtr report, uint reportLength);

        [DllImport("hid.dll")]
        static extern uint HidP_MaxUsageListLength(ushort reportType, ushort usagePage, IntPtr preparsedData);

        internal RawConsumerInput(IntPtr windowHandle, Action<string> callback)
        {
            if (windowHandle == IntPtr.Zero) throw new ArgumentException("A valid window handle is required.", "windowHandle");
            if (callback == null) throw new ArgumentNullException("callback");
            this.callback = callback;
            active = RegisterRawInputDevices(new[] { new DeviceRegistration { Page = ConsumerPage, Usage = 1, Flags = RidevInputSink | RidevDevNotify, Target = windowHandle } }, 1, (uint)Marshal.SizeOf(typeof(DeviceRegistration)));
        }

        internal bool IsActive { get { return active; } }

        internal static DeviceRegistration CreateRemovalRegistration()
        {
            return new DeviceRegistration { Page = ConsumerPage, Usage = 1, Flags = RidevRemove, Target = IntPtr.Zero };
        }

        internal bool Handle(ref Message message)
        {
            return HandleMessage(message.Msg, message.WParam, message.LParam);
        }

        // WPF HwndSource hook entry point, shared with the WinForms Message path.
        internal bool HandleMessage(int message, IntPtr inputHandle) { return HandleMessage(message, IntPtr.Zero, inputHandle); }
        internal bool HandleMessage(int message, IntPtr wParam, IntPtr inputHandle)
        {
            if (message == WmInputDeviceChange) {
                if (wParam.ToInt64() == GidcRemoval) usageState.ResetDevice(inputHandle);
                return false;
            }
            if (!active || message != WmInput) return false;
            try { ReadInput(inputHandle); }
            catch { /* Malformed or removed HID devices must not take down the UI thread. */ }
            return true;
        }

        internal static bool HasValidReportLayout(uint totalSize, uint headerSize, uint reportLength, uint reportCount)
        {
            const uint HidHeaderSize = 8;
            if (totalSize > MaxInputBytes || totalSize < headerSize || totalSize - headerSize < HidHeaderSize || reportLength == 0 || reportCount == 0 || reportCount > 256) return false;
            uint availableBytes = totalSize - headerSize - HidHeaderSize;
            return reportCount <= availableBytes / reportLength;
        }

        internal static bool IsSuccessfulUsageQuery(int status) { return status == HidpStatusSuccess; }

        void ReadInput(IntPtr inputHandle)
        {
            uint size = 0;
            uint headerSize = (uint)Marshal.SizeOf(typeof(RawInputHeader));
            if (GetRawInputData(inputHandle, RidInput, IntPtr.Zero, ref size, headerSize) != 0 || size < headerSize + 8 || size > MaxInputBytes) return;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                uint copied = GetRawInputData(inputHandle, RidInput, buffer, ref size, headerSize);
                if (copied == UInt32.MaxValue || copied < headerSize + 8 || copied > MaxInputBytes) return;
                ReadHidReports(buffer, copied, headerSize);
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        void ReadHidReports(IntPtr buffer, uint totalSize, uint headerSize)
        {
            RawInputHeader header = (RawInputHeader)Marshal.PtrToStructure(buffer, typeof(RawInputHeader));
            if (header.Type != 2 || header.Device == IntPtr.Zero) return;
            if (header.Size < headerSize + 8 || header.Size > totalSize || header.Size > MaxInputBytes) { usageState.ResetDevice(header.Device); return; }

            uint preparsedSize = 0;
            uint probe = GetRawInputDeviceInfo(header.Device, GetPreparsedData, IntPtr.Zero, ref preparsedSize);
            if (probe == UInt32.MaxValue || preparsedSize == 0 || preparsedSize > MaxInputBytes) { usageState.ResetDevice(header.Device); return; }

            IntPtr preparsed = Marshal.AllocHGlobal((int)preparsedSize);
            try
            {
                uint copied = GetRawInputDeviceInfo(header.Device, GetPreparsedData, preparsed, ref preparsedSize);
                if (copied == 0 || copied == UInt32.MaxValue || copied > MaxInputBytes) { usageState.ResetDevice(header.Device); return; }
                ReadReports(buffer, header.Size, headerSize, preparsed, header.Device);
            }
            finally { Marshal.FreeHGlobal(preparsed); }
        }

        void ReadReports(IntPtr buffer, uint totalSize, uint headerSize, IntPtr preparsed, IntPtr device)
        {
            IntPtr hid = IntPtr.Add(buffer, checked((int)headerSize));
            uint reportLength = unchecked((uint)Marshal.ReadInt32(hid));
            uint reportCount = unchecked((uint)Marshal.ReadInt32(IntPtr.Add(hid, 4)));
            if (!HasValidReportLayout(totalSize, headerSize, reportLength, reportCount)) { usageState.ResetDevice(device); return; }

            uint maxUsages = HidP_MaxUsageListLength(0, ConsumerPage, preparsed);
            if (maxUsages == 0 || maxUsages > MaxUsageCount) { usageState.ResetDevice(device); return; }
            var usages = new ushort[(int)maxUsages];
            IntPtr reports = IntPtr.Add(hid, 8);
            var found = new HashSet<ushort>();
            for (uint i = 0; i < reportCount; i++)
            {
                IntPtr report = IntPtr.Add(reports, checked((int)(i * reportLength)));
                ReadPageUsages(preparsed, report, reportLength, ConsumerPage, usages, found);
            }

            foreach (ushort usage in usageState.Update(device, found)) callback(ConsumerControlDecoder.NameFor(ConsumerPage, usage));
        }

        static void ReadPageUsages(IntPtr preparsed, IntPtr report, uint reportLength, ushort page, ushort[] usages, HashSet<ushort> found)
        {
            uint count = (uint)usages.Length;
            if (!IsSuccessfulUsageQuery(HidP_GetUsages(0, page, 0, usages, ref count, preparsed, report, reportLength))) return;
            for (uint i = 0; i < count && i < usages.Length; i++)
                if (ConsumerControlDecoder.NameFor(page, usages[i]) != null) found.Add(usages[i]);
        }

        public void Dispose()
        {
            if (!active) return;
            RegisterRawInputDevices(new[] { CreateRemovalRegistration() }, 1, (uint)Marshal.SizeOf(typeof(DeviceRegistration)));
            active = false;
            usageState.Clear();
        }
    }

    internal sealed class ConsumerUsageState
    {
        readonly Dictionary<IntPtr, HashSet<ushort>> heldByDevice = new Dictionary<IntPtr, HashSet<ushort>>();

        internal ushort[] Update(IntPtr device, IEnumerable<ushort> usages)
        {
            if (device == IntPtr.Zero) throw new ArgumentException("A raw-input device is required.", "device");
            var current = new HashSet<ushort>(usages ?? Enumerable.Empty<ushort>());
            HashSet<ushort> previous;
            heldByDevice.TryGetValue(device, out previous);
            ushort[] pressed = current.Where(usage => previous == null || !previous.Contains(usage)).ToArray();
            if (current.Count == 0) heldByDevice.Remove(device); else heldByDevice[device] = current;
            return pressed;
        }

        internal void ResetDevice(IntPtr device) { if (device != IntPtr.Zero) heldByDevice.Remove(device); }
        internal void Clear() { heldByDevice.Clear(); }
    }
}
