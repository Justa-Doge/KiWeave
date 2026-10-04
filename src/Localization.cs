using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace FunctionRowRemapper
{
    internal static class Localization
    {
        static readonly Dictionary<string, string> BuiltIn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "Settings", "Configuración" }, { "Save", "Guardar" }, { "Cancel", "Cancelar" }, { "Done", "Listo" }, { "Back up everything", "Hacer copia de seguridad" }, { "Restore backup", "Restaurar copia" }, { "Encrypted backup", "Copia cifrada" }, { "Check for updates", "Buscar actualizaciones" }, { "Privacy center", "Centro de privacidad" }, { "Diagnostics", "Diagnóstico" }, { "Why did this run?", "¿Por qué se ejecutó?" }, { "Secrets vault", "Bóveda de secretos" }, { "MIDI triggers", "Activadores MIDI" }, { "Controller triggers", "Activadores del controlador" }, { "Open data folder", "Abrir carpeta de datos" }, { "Open log folder", "Abrir carpeta de registros" }, { "Manage profiles", "Administrar perfiles" }, { "Profile schedules", "Horarios de perfiles" }, { "Welcome guide", "Guía de bienvenida" }, { "Find mappings", "Buscar asignaciones" }, { "Live key tester", "Prueba de teclas" }, { "Conflict center", "Centro de conflictos" }, { "Undo and history", "Deshacer e historial" }, { "Support bundle", "Paquete de soporte" }, { "Release readiness", "Preparación de lanzamiento" }, { "Lock editing", "Bloquear edición" }, { "Restart KiWeave", "Reiniciar KiWeave" }, { "Exit KiWeave", "Salir de KiWeave" }, { "Use portable data", "Usar datos portátiles" }, { "Import", "Importar" }, { "Export", "Exportar" }, { "Delete selected", "Eliminar seleccionada" }, { "Add", "Agregar" }
        };
        internal static readonly string[] Languages = { "English", "Español" };
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "language.json"); } }
        static string current = "English";
        internal static string Current { get { return current; } }
        internal static void Load() { try { if (File.Exists(Path)) { var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path, Encoding.UTF8)) as Dictionary<string, object>; string value = d == null || !d.ContainsKey("language") ? "" : d["language"] as string; current = value == "Español" ? value : "English"; } } catch { current = "English"; } }
        internal static void Save(string language) { if (Array.IndexOf(Languages, language) < 0) throw new ArgumentException("Unsupported language."); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); File.WriteAllText(Path, "{\r\n  \"version\": 1,\r\n  \"language\": \"" + language + "\"\r\n}\r\n", new UTF8Encoding(false)); current = language; }
        internal static string Translate(string text) { if (Current != "Español" || String.IsNullOrEmpty(text)) return text; string translated; return BuiltIn.TryGetValue(text, out translated) ? translated : text; }
    }
}
