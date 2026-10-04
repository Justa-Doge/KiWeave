using System;
using System.Runtime.InteropServices;

namespace FunctionRowRemapper
{
    // Optional output only. KiWeave does not ship, install, or enable ViGEm.
    // The provider must already be installed and the caller must explicitly
    // opt in before this adapter loads its DLL or creates a virtual device.
    internal sealed class VirtualGamepadBackend : IDisposable
    {
        const uint VigemErrorNone = 0;
        const ushort TargetTypeX360 = 1;
        [StructLayout(LayoutKind.Sequential)] struct XusbReport { public ushort Buttons; public byte LeftTrigger, RightTrigger; public short LeftX, LeftY, RightX, RightY; }
        [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr vigem_alloc();
        [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)] static extern uint vigem_connect(IntPtr client);
        [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)] static extern void vigem_free(IntPtr client);
        [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)] static extern IntPtr vigem_target_x360_alloc();
        [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)] static extern void vigem_target_free(IntPtr target);
        [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)] static extern uint vigem_target_add(IntPtr client, IntPtr target);
        [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)] static extern uint vigem_target_remove(IntPtr client, IntPtr target);
        [DllImport("ViGEmClient.dll", CallingConvention = CallingConvention.Cdecl)] static extern uint vigem_target_x360_update(IntPtr client, IntPtr target, XusbReport report);
        readonly IntPtr client, target;
        bool disposed;
        VirtualGamepadBackend(IntPtr client, IntPtr target) { this.client = client; this.target = target; }
        internal static string Describe() { return "Optional virtual-gamepad output is disabled by default; no driver is installed by KiWeave."; }
        internal static bool TryCreate(bool explicitlyEnabled, out VirtualGamepadBackend backend, out string error)
        {
            backend = null; error = "";
            if (!explicitlyEnabled) { error = "Virtual-gamepad output requires an explicit opt-in."; return false; }
            IntPtr client = IntPtr.Zero, target = IntPtr.Zero;
            try {
                client = vigem_alloc(); if (client == IntPtr.Zero) throw new InvalidOperationException("The optional ViGEm provider is unavailable.");
                if (vigem_connect(client) != VigemErrorNone) throw new InvalidOperationException("The optional virtual-gamepad provider could not connect.");
                target = vigem_target_x360_alloc(); if (target == IntPtr.Zero) throw new InvalidOperationException("The optional virtual-gamepad target could not be created.");
                if (vigem_target_add(client, target) != VigemErrorNone) throw new InvalidOperationException("The optional virtual-gamepad target could not be attached.");
                backend = new VirtualGamepadBackend(client, target); return true;
            } catch (DllNotFoundException) { error = "Optional ViGEmClient.dll is not installed."; }
            catch (EntryPointNotFoundException) { error = "The installed virtual-gamepad provider is incompatible."; }
            catch (Exception ex) { error = ex.Message; }
            if (target != IntPtr.Zero) { try { vigem_target_free(target); } catch { } }
            if (client != IntPtr.Zero) { try { vigem_free(client); } catch { } }
            return false;
        }
        internal void Update(ushort buttons, byte leftTrigger, byte rightTrigger, short leftX, short leftY, short rightX, short rightY)
        {
            if (disposed) throw new ObjectDisposedException("VirtualGamepadBackend");
            var report = new XusbReport { Buttons = buttons, LeftTrigger = leftTrigger, RightTrigger = rightTrigger, LeftX = leftX, LeftY = leftY, RightX = rightX, RightY = rightY };
            if (vigem_target_x360_update(client, target, report) != VigemErrorNone) throw new InvalidOperationException("The optional virtual-gamepad update failed.");
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            try { vigem_target_remove(client, target); } catch { }
            try { vigem_target_free(target); } catch { }
            try { vigem_free(client); } catch { }
        }
    }
}
