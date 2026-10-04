using System;
using Microsoft.Win32;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class SessionAwareness
    {
        internal static bool IsRemoteDesktop { get { try { return SystemInformation.TerminalServerSession || String.Equals(Environment.GetEnvironmentVariable("SESSIONNAME"), "RDP-Tcp", StringComparison.OrdinalIgnoreCase); } catch { return false; } } }
        internal static bool IsVirtualMachine
        {
            get {
                try { using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS")) { string text = ((key == null ? null : key.GetValue("SystemManufacturer")) as string) + " " + ((key == null ? null : key.GetValue("SystemProductName")) as string); return text.IndexOf("VMware", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("VirtualBox", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("KVM", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Virtual Machine", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("QEMU", StringComparison.OrdinalIgnoreCase) >= 0; } }
                catch { return false; }
            }
        }
        internal static string Describe(bool remote, bool vm) { if (remote && vm) return "Remote Desktop · virtual machine"; if (remote) return "Remote Desktop session"; if (vm) return "Virtual machine session"; return "Local Windows session"; }
        internal static string CurrentDescription() { return Describe(IsRemoteDesktop, IsVirtualMachine); }
    }
}
