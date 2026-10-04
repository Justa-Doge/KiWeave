using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace FunctionRowRemapper
{
    internal static class Localization
    {
        static readonly Dictionary<string, string> BuiltIn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "Settings", "Configuración" }, { "Save", "Guardar" }, { "Cancel", "Cancelar" }, { "Done", "Listo" },
            { "Back up everything", "Hacer copia de seguridad" }, { "Restore backup", "Restaurar copia" }, { "Encrypted backup", "Copia cifrada" },
            { "Check for updates", "Buscar actualizaciones" }, { "Privacy center", "Centro de privacidad" }, { "Diagnostics", "Diagnóstico" },
            { "Why did this run?", "¿Por qué se ejecutó?" }, { "Secrets vault", "Bóveda de secretos" }, { "MIDI triggers", "Activadores MIDI" },
            { "Controller triggers", "Activadores del controlador" }, { "Open data folder", "Abrir carpeta de datos" }, { "Open log folder", "Abrir carpeta de registros" },
            { "Manage profiles", "Administrar perfiles" }, { "Profile schedules", "Horarios de perfiles" }, { "Welcome guide", "Guía de bienvenida" },
            { "Find mappings", "Buscar asignaciones" }, { "Live key tester", "Prueba de teclas" }, { "Conflict center", "Centro de conflictos" },
            { "Undo and history", "Deshacer e historial" }, { "Support bundle", "Paquete de soporte" }, { "Release readiness", "Preparación de lanzamiento" },
            { "Lock editing", "Bloquear edición" }, { "Restart KiWeave", "Reiniciar KiWeave" }, { "Exit KiWeave", "Salir de KiWeave" },
            { "Use portable data", "Usar datos portátiles" }, { "Import", "Importar" }, { "Export", "Exportar" }, { "Delete selected", "Eliminar seleccionada" },
            { "Add", "Agregar" }, { "Open", "Abrir" }, { "Close", "Cerrar" }, { "Refresh", "Actualizar" }, { "Reset", "Restablecer" },
            { "Apply", "Aplicar" }, { "Clear", "Borrar" }, { "Remove", "Quitar" }, { "Edit", "Editar" }, { "Duplicate", "Duplicar" },
            { "Profile", "Perfil" }, { "Profiles", "Perfiles" }, { "Layer", "Capa" }, { "Layers", "Capas" }, { "Base", "Base" },
            { "Search", "Buscar" }, { "Search mappings", "Buscar asignaciones" }, { "No results", "Sin resultados" }, { "No matching topics.", "No hay temas coincidentes." },
            { "Action library", "Biblioteca de acciones" }, { "Conditional action", "Acción condicional" }, { "Sequence builder", "Constructor de secuencias" },
            { "KiWeave offline guide", "Guía sin conexión de KiWeave" }, { "KiWeave diagnostics", "Diagnóstico de KiWeave" }, { "KiWeave profiles", "Perfiles de KiWeave" },
            { "KiWeave layers", "Capas de KiWeave" }, { "KiWeave conflict center", "Centro de conflictos de KiWeave" }, { "KiWeave privacy center", "Centro de privacidad de KiWeave" },
            { "KiWeave integrations", "Integraciones de KiWeave" }, { "KiWeave live key tester", "Prueba de teclas de KiWeave" }, { "Welcome to KiWeave", "Bienvenido a KiWeave" },
            { "About KiWeave", "Acerca de KiWeave" }, { "Detect monitors", "Detectar monitores" }, { "Simulate collision", "Simular colisión" },
            { "Collision simulation", "Simulación de colisión" }, { "No known conflicts", "No hay conflictos conocidos" }, { "Nothing needs attention.", "Nada requiere atención." },
            { "Saved triggers", "Activadores guardados" }, { "Saved profiles", "Perfiles guardados" }, { "Delete", "Eliminar" }, { "Use shortcut", "Usar atajo" },
            { "Waiting for input…", "Esperando entrada…" }, { "Waiting for input...", "Esperando entrada..." }, { "No mappings match that search.", "Ninguna asignación coincide con la búsqueda." },
            { "Suspend shortcuts by app", "Suspender atajos por aplicación" }, { "KiWeave Safe Mode", "Modo seguro de KiWeave" }, { "KiWeave release readiness", "Preparación de lanzamiento de KiWeave" },
            { "KiWeave import review", "Revisión de importación de KiWeave" }, { "KiWeave profile import", "Importación de perfil de KiWeave" }, { "Backup privacy preset", "Preajuste de privacidad de copia" },
            { "Encrypted full backup", "Copia de seguridad completa cifrada" }, { "English", "Inglés" }, { "Español", "Español" }, { "All", "Todo" },
            { "Warnings and above", "Advertencias y superiores" }, { "Critical only", "Solo críticos" }, { "Enabled", "Activado" }, { "Disabled", "Desactivado" }
        };
        internal static readonly string[] Languages = { "English", "Español" };
        internal static string Path { get { return System.IO.Path.Combine(AppStorage.DataFolder, "language.json"); } }
        static string current = "English";
        internal static string Current { get { return current; } }
        internal static void Load() { try { if (File.Exists(Path)) { var d = new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path, Encoding.UTF8)) as Dictionary<string, object>; string value = d == null || !d.ContainsKey("language") ? "" : d["language"] as string; current = value == "Español" ? value : "English"; } } catch { current = "English"; } }
        internal static void Save(string language) { if (Array.IndexOf(Languages, language) < 0) throw new ArgumentException("Unsupported language."); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)); File.WriteAllText(Path, "{\r\n  \"version\": 1,\r\n  \"language\": \"" + language + "\"\r\n}\r\n", new UTF8Encoding(false)); current = language; }
        internal static string Translate(string text) { if (Current != "Español" || String.IsNullOrEmpty(text)) return text; string translated; return BuiltIn.TryGetValue(text, out translated) ? translated : text; }
        internal static void Apply(Control root)
        {
            if (root == null || Current != "Español") return;
            root.Text = Translate(root.Text);
            var combo = root as ComboBox;
            if (combo != null) for (int i = 0; i < combo.Items.Count; i++) combo.Items[i] = Translate(Convert.ToString(combo.Items[i]));
            foreach (Control child in root.Controls) Apply(child);
        }
    }
}
