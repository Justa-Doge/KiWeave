using System;
using System.IO;
using System.Text;

namespace FunctionRowRemapper
{
    internal static class AtomicFile
    {
        internal static byte[] ReadAllBytesBounded(string path, int maxBytes)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", "path");
            if (maxBytes < 0) throw new ArgumentOutOfRangeException("maxBytes");
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                if (input.Length > maxBytes) throw new InvalidDataException("File exceeds the allowed size.");
                using (var output = new MemoryStream((int)Math.Min(input.Length, maxBytes))) {
                    byte[] buffer = new byte[32768]; int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                        if (read > maxBytes - output.Length) throw new InvalidDataException("File exceeds the allowed size.");
                        output.Write(buffer, 0, read);
                    }
                    return output.ToArray();
                }
            }
        }

        internal static string ReadAllTextBounded(string path, int maxBytes, Encoding encoding)
        {
            if (encoding == null) throw new ArgumentNullException("encoding");
            byte[] contents = ReadAllBytesBounded(path, maxBytes), preamble = encoding.GetPreamble();
            return DecodeBoundedText(contents, encoding, preamble);
        }

        internal static string ReadStreamTextBounded(Stream input, int maxBytes, Encoding encoding)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (maxBytes < 0) throw new ArgumentOutOfRangeException("maxBytes");
            if (encoding == null) throw new ArgumentNullException("encoding");
            using (var output = new MemoryStream(Math.Min(maxBytes, 32768))) {
                byte[] buffer = new byte[8192]; int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0) {
                    if (read > maxBytes - output.Length) throw new InvalidDataException("Stream exceeds the allowed size.");
                    output.Write(buffer, 0, read);
                }
                return DecodeBoundedText(output.ToArray(), encoding, encoding.GetPreamble());
            }
        }

        static string DecodeBoundedText(byte[] contents, Encoding encoding, byte[] preamble)
        {
            bool hasPreamble = preamble.Length > 0 && contents.Length >= preamble.Length;
            for (int i = 0; hasPreamble && i < preamble.Length; i++) if (contents[i] != preamble[i]) hasPreamble = false;
            return encoding.GetString(contents, hasPreamble ? preamble.Length : 0, contents.Length - (hasPreamble ? preamble.Length : 0));
        }

        internal static void WriteAllText(string path, string text, Encoding encoding, string backupExtension = null)
        {
            if (encoding == null) throw new ArgumentNullException("encoding");
            WriteAllBytes(path, encoding.GetBytes(text ?? String.Empty), backupExtension);
        }

        internal static void WriteAllBytes(string path, byte[] contents, string backupExtension = null)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", "path");
            if (contents == null) throw new ArgumentNullException("contents");
            string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                    stream.Write(contents, 0, contents.Length);
                    stream.Flush(true);
                }
                if (File.Exists(full)) {
                    string backup = String.IsNullOrEmpty(backupExtension) ? null : full + backupExtension;
                    File.Replace(temporary, full, backup, true);
                } else File.Move(temporary, full);
            } finally {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    internal static class LocalObservationStore
    {
        internal static string CompareAndSave(string path, string current, Func<string, string, string> compare)
        {
            string previous;
            try { previous = AtomicFile.ReadAllTextBounded(path, 1024, Encoding.ASCII).Trim(); }
            catch (FileNotFoundException) { previous = ""; }
            catch (DirectoryNotFoundException) { previous = ""; }
            catch { return "Could not read the previous local observation"; }
            try { AtomicFile.WriteAllText(path, current, Encoding.ASCII); }
            catch { return "Could not save the local observation"; }
            return compare(previous, current);
        }
    }
}
