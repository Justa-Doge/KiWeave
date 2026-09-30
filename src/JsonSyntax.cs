using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    // JavaScriptSerializer is deliberately preceded by strict JSON syntax checking.
    // In particular, reject duplicate properties rather than silently taking the last one.
    internal sealed class JsonSyntax
    {
        readonly string text; int pos;
        JsonSyntax(string text) { this.text = text; }
        internal static void Check(string text) { var p = new JsonSyntax(text); p.Value(0); p.White(); if (p.pos != text.Length) p.Fail(); }
        void Fail() { throw new ArgumentException("Invalid JSON syntax or duplicate field near character " + (pos + 1) + "."); }
        void White() { while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\r' || text[pos] == '\n' || text[pos] == '\t')) pos++; }
        bool Take(char c) { White(); if (pos < text.Length && text[pos] == c) { pos++; return true; } return false; }
        void Need(char c) { if (!Take(c)) Fail(); }
        string String()
        {
            White(); int start = pos; Need('"');
            while (pos < text.Length) {
                char c = text[pos++];
                if (c == '"') return new JavaScriptSerializer().Deserialize<string>(text.Substring(start, pos - start));
                if (c < 32) Fail();
                if (c == '\\') {
                    if (pos >= text.Length) Fail(); char esc = text[pos++];
                    if (esc == 'u') { for (int i = 0; i < 4; i++) { if (pos >= text.Length || !Uri.IsHexDigit(text[pos++])) Fail(); } }
                    else if ("\"\\/bfnrt".IndexOf(esc) < 0) Fail();
                }
            }
            Fail(); return null;
        }
        void Value(int depth)
        {
            White(); if (depth > 10 || pos >= text.Length) Fail();
            if (Take('{')) {
                var seen = new HashSet<string>(); if (Take('}')) return;
                do { string name = String(); if (!seen.Add(name)) Fail(); Need(':'); Value(depth + 1); if (Take('}')) return; } while (Take(',')); Fail();
            } else if (Take('[')) {
                if (Take(']')) return;
                do { Value(depth + 1); if (Take(']')) return; } while (Take(',')); Fail();
            } else if (text[pos] == '"') String();
            else if (text[pos] == 't') Literal("true"); else if (text[pos] == 'f') Literal("false"); else if (text[pos] == 'n') Literal("null");
            else {
                if (text[pos] == '-') pos++;
                if (pos >= text.Length) Fail();
                if (text[pos] == '0') pos++; else { if (text[pos] < '1' || text[pos] > '9') Fail(); Digits(); }
                if (pos < text.Length && text[pos] == '.') { pos++; int n = pos; Digits(); if (pos == n) Fail(); }
                if (pos < text.Length && (text[pos] == 'e' || text[pos] == 'E')) { pos++; if (pos < text.Length && (text[pos] == '+' || text[pos] == '-')) pos++; int n = pos; Digits(); if (pos == n) Fail(); }
            }
        }
        void Digits() { while (pos < text.Length && text[pos] >= '0' && text[pos] <= '9') pos++; }
        void Literal(string s) { if (pos + s.Length > text.Length || text.Substring(pos, s.Length) != s) Fail(); pos += s.Length; }
    }
}
