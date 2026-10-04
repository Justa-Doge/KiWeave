using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class SecretVault
    {
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "secrets.dat"); } }
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KiWeave local secret vault v1");
        internal static void Save(string name, string value)
        {
            ValidateName(name); if (value == null || value.Length > 4096) throw new ArgumentException("Secret value is empty or too large.");
            var all = LoadAll(); all[name] = value; Write(all);
        }
        internal static bool Delete(string name) { ValidateName(name); var all = LoadAll(); if (!all.Remove(name)) return false; Write(all); return true; }
        internal static string Resolve(string name) { ValidateName(name); string value; return LoadAll().TryGetValue(name, out value) ? value : null; }
        internal static string Expand(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            int start = 0; var output = new StringBuilder();
            while (start < text.Length) { int open = text.IndexOf("{vault:", start, StringComparison.Ordinal); if (open < 0) { output.Append(text.Substring(start)); break; } output.Append(text.Substring(start, open - start)); int close = text.IndexOf('}', open + 7); if (close < 0) throw new ArgumentException("An HTTP body contains an unterminated vault reference."); string name = text.Substring(open + 7, close - open - 7); string value = Resolve(name); if (value == null) throw new InvalidOperationException("Vault secret '" + name + "' was not found."); output.Append(value); start = close + 1; }
            return output.ToString();
        }
        internal static string[] Names() { return LoadAll().Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
        static Dictionary<string, string> LoadAll()
        {
            try { if (!File.Exists(Path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); byte[] encrypted = File.ReadAllBytes(Path); byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser); var d = new JavaScriptSerializer().DeserializeObject(Encoding.UTF8.GetString(plain)) as Dictionary<string, object>; return d == null ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : d.ToDictionary(x => x.Key, x => x.Value as string ?? "", StringComparer.OrdinalIgnoreCase); } catch { return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); }
        }
        static void Write(Dictionary<string, string> values)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); byte[] plain = Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(values)); byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser); File.WriteAllBytes(Path, encrypted);
        }
        static void ValidateName(string name) { if (String.IsNullOrWhiteSpace(name) || name.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(name, "\\A[a-zA-Z0-9._-]+\\z")) throw new ArgumentException("Secret names use letters, numbers, dots, underscores, or hyphens."); }
    }

    internal sealed class SecretVaultForm : Form
    {
        readonly TextBox name = new DesignTextBox(), value = new DesignTextBox { PasswordChar = '•' }; readonly ListBox list = new DesignListBox();
        internal SecretVaultForm()
        {
            Text = "KiWeave - Secrets vault"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Font = new System.Drawing.Font("Segoe UI", 10); ClientSize = new System.Drawing.Size(700, 500); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Local secrets vault", 20, true)); root.Controls.Add(UiStyle.Text("Secrets are protected for this Windows account and never included in exports or backups. HTTP bodies can reference them as {vault:name}.", 9, false)); list.Height = 180; root.Controls.Add(UiStyle.Field("Saved names", list)); root.Controls.Add(UiStyle.Field("Name", name)); root.Controls.Add(UiStyle.Field("Value", value)); var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(UiStyle.Button("Save secret", delegate { try { SecretVault.Save(name.Text.Trim(), value.Text); value.Clear(); RefreshList(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Secrets vault", MessageBoxButtons.OK, MessageBoxIcon.Error); } }, true)); buttons.Controls.Add(UiStyle.Button("Delete selected", delegate { if (list.SelectedItem != null) { SecretVault.Delete(list.SelectedItem.ToString()); RefreshList(); } })); buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); })); root.Controls.Add(buttons); RefreshList();
        }
        void RefreshList() { list.Items.Clear(); list.Items.AddRange(SecretVault.Names()); }
    }
}
