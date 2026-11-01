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
    public static class SecretVault
    {
        public static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "secrets.dat"); } }
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KiWeave local secret vault v1");
        internal const int MaxSecretCount = 256;
        internal const int MaxVaultBytes = 4 * 1024 * 1024;
        static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        public static void Save(string name, string value) { Save(Path, name, value); }
        internal static void Save(string path, string name, string value)
        {
            ValidateName(name); if (value == null || value.Length > 4096) throw new ArgumentException("Secret value is empty or too large.");
            var all = LoadAll(path);
            if (!all.ContainsKey(name) && all.Count >= MaxSecretCount) throw new InvalidOperationException("The secrets vault can contain at most " + MaxSecretCount + " entries.");
            all[name] = value; Write(path, all);
        }
        public static bool Delete(string name) { return Delete(Path, name); }
        internal static bool Delete(string path, string name) { ValidateName(name); var all = LoadAll(path); if (!all.Remove(name)) return false; Write(path, all); return true; }
        public static string Resolve(string name) { return Resolve(Path, name); }
        internal static string Resolve(string path, string name) { ValidateName(name); string value; return LoadAll(path).TryGetValue(name, out value) ? value : null; }
        public static string Expand(string text)
        {
            if (String.IsNullOrEmpty(text)) return text;
            int start = 0; var output = new StringBuilder();
            while (start < text.Length) { int open = text.IndexOf("{vault:", start, StringComparison.Ordinal); if (open < 0) { output.Append(text.Substring(start)); break; } output.Append(text.Substring(start, open - start)); int close = text.IndexOf('}', open + 7); if (close < 0) throw new ArgumentException("An HTTP body contains an unterminated vault reference."); string name = text.Substring(open + 7, close - open - 7); string value = Resolve(name); if (value == null) throw new InvalidOperationException("Vault secret '" + name + "' was not found."); output.Append(value); start = close + 1; }
            return output.ToString();
        }
        public static string[] Names() { return LoadAll(Path).Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
        internal static Dictionary<string, string> LoadAll(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A vault path is required.", "path");
            if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                byte[] encrypted = AtomicFile.ReadAllBytesBounded(path, MaxVaultBytes + 64 * 1024);
                byte[] plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                if (plain.Length > MaxVaultBytes) throw new InvalidDataException("Vault exceeds the allowed size.");
                var serializer = new JavaScriptSerializer { MaxJsonLength = MaxVaultBytes };
                var d = serializer.DeserializeObject(StrictUtf8.GetString(plain)) as Dictionary<string, object>;
                if (d == null || d.Count > MaxSecretCount || d.Any(item => !(item.Value is string) || ((string)item.Value).Length > 4096)) throw new InvalidDataException();
                foreach (string name in d.Keys) ValidateName(name);
                return d.ToDictionary(item => item.Key, item => (string)item.Value, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception error) { throw new InvalidDataException("The saved secrets vault could not be read; it was left unchanged.", error); }
        }
        static void Write(string path, Dictionary<string, string> values)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A vault path is required.", "path");
            if (values == null || values.Count > MaxSecretCount) throw new InvalidOperationException("The secrets vault can contain at most " + MaxSecretCount + " entries.");
            foreach (var item in values) { ValidateName(item.Key); if (item.Value == null || item.Value.Length > 4096) throw new InvalidDataException("A saved secret is invalid or too large."); }
            byte[] plain = StrictUtf8.GetBytes(new JavaScriptSerializer { MaxJsonLength = MaxVaultBytes }.Serialize(values));
            if (plain.Length > MaxVaultBytes) throw new InvalidOperationException("The secrets vault exceeds the allowed size.");
            byte[] encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            AtomicFile.WriteAllBytes(path, encrypted, ".bak");
        }
        static void ValidateName(string name) { if (String.IsNullOrWhiteSpace(name) || name.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(name, "\\A[a-zA-Z0-9._-]+\\z")) throw new ArgumentException("Secret names use letters, numbers, dots, underscores, or hyphens."); }
    }

#if !WPF_REWRITE
    internal sealed class SecretVaultForm : Form
    {
        readonly TextBox name = new DesignTextBox(), value = new DesignTextBox { PasswordChar = '•' }; readonly ListBox list = new DesignListBox();
        internal SecretVaultForm()
        {
            Text = "KiWeave - Secrets vault"; Icon = Program.AppIcon(); BackColor = UiStyle.Canvas; ForeColor = UiStyle.Ink; Font = new System.Drawing.Font("Segoe UI", 10); ClientSize = new System.Drawing.Size(700, 500); StartPosition = FormStartPosition.CenterParent; Design.DarkTitlebar(this);
            var root = UiStyle.Stack(); root.Padding = new Padding(24); Controls.Add(root); root.Controls.Add(UiStyle.Text("Local secrets vault", 20, true)); root.Controls.Add(UiStyle.Text("Secrets are protected for this Windows account and never included in exports or backups. HTTP bodies can reference them as {vault:name}.", 9, false)); list.Height = 180; root.Controls.Add(UiStyle.Field("Saved names", list)); root.Controls.Add(UiStyle.Field("Name", name)); root.Controls.Add(UiStyle.Field("Value", value)); var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.RightToLeft }; buttons.Controls.Add(UiStyle.Button("Save secret", delegate { try { SecretVault.Save(name.Text.Trim(), value.Text); value.Clear(); RefreshList(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Secrets vault", MessageBoxButtons.OK, MessageBoxIcon.Error); } }, true)); buttons.Controls.Add(UiStyle.Button("Delete selected", delegate { if (list.SelectedItem != null) try { SecretVault.Delete(list.SelectedItem.ToString()); RefreshList(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Secrets vault", MessageBoxButtons.OK, MessageBoxIcon.Error); } })); buttons.Controls.Add(UiStyle.Button("Done", delegate { Close(); })); root.Controls.Add(buttons); RefreshList();
        }
        void RefreshList() { list.Items.Clear(); try { list.Items.AddRange(SecretVault.Names()); list.Enabled = true; } catch (InvalidDataException ex) { list.Enabled = false; list.Items.Add("Vault unreadable; existing data preserved"); MessageBox.Show(this, ex.Message, "Secrets vault", MessageBoxButtons.OK, MessageBoxIcon.Warning); } }
    }
#endif
}
