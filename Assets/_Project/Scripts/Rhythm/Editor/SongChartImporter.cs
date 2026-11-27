// Dios Punk - Importador / exportador de charts JSON para SongData
// Ubicacion: Assets/_Project/Scripts/Rhythm/Editor/SongChartImporter.cs (ensamblado DiosPunk.Rhythm.Editor)
//
// Uso:
//   1) Click derecho sobre el asset SongData (ej. Resources/Songs/Song_01_Comienzos) en el Inspector
//      -> menu de los tres puntitos -> "Importar chart JSON..."
//      o bien seleccionar el asset y usar el menu "Dios Punk > Charts > Importar chart JSON en SongData seleccionado".
//   2) Elegir el archivo .json (ej. Song_01_Comienzos_chart.json).
//   3) El script reemplaza _notes y _bpm (y _title si el JSON trae titulo). Es Undo-able (Ctrl+Z).
//
// Formato: el "beat" es la fuente de verdad:
//   targetTimeDSP = firstBeatSeconds + beat * 60 / bpm + globalOffsetMs / 1000
//   Si una nota no trae "beat" se usa su "time" (segundos desde el primer sample del audio).
//   Para corregir sincronia general alcanza con cambiar "globalOffsetMs" en el JSON y reimportar.
//
// Nota: SongData esta en el ensamblado DiosPunk.Rhythm. Si ese .asmdef NO tiene "Auto Referenced" activado,
// crear un Editor .asmdef en Assets/Editor que referencie DiosPunk.Rhythm (Platforms: solo Editor).

#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text;
using DiosPunk.Rhythm;
using UnityEditor;
using UnityEngine;

namespace DiosPunk.EditorTools
{
    public static class SongChartImporter
    {
        [System.Serializable]
        private class ChartNote
        {
            public float beat = -1f;
            public double time = -1.0;
            public int lane;
            public bool isDebuffNote;
            public string section;
            public string pitch;
        }

        [System.Serializable]
        private class ChartFile
        {
            public string songResource;
            public string title;
            public float bpm;
            public double firstBeatSeconds;
            public double globalOffsetMs;
            public ChartNote[] notes;
        }

        // ---------- Importar ----------

        [MenuItem("CONTEXT/SongData/Importar chart JSON...")]
        private static void ImportFromContext(MenuCommand command) => Import((SongData)command.context);

        [MenuItem("Dios Punk/Charts/Importar chart JSON en SongData seleccionado")]
        private static void ImportFromMenu()
        {
            var song = Selection.activeObject as SongData;
            if (song == null)
            {
                EditorUtility.DisplayDialog("Importar chart", "Selecciona un asset SongData en el Project.", "OK");
                return;
            }
            Import(song);
        }

        private static void Import(SongData song)
        {
            string path = EditorUtility.OpenFilePanel("Chart JSON para " + song.name, Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;

            if (!TryImportFromFile(song, path, out int count, out string error))
            {
                EditorUtility.DisplayDialog("Importar chart", error, "OK");
            }
        }

        /// <summary>
        /// Importa un chart JSON en el SongData sin diálogos (lo usa el generador de escenas). Reemplaza _notes, _bpm y,
        /// si el JSON trae título, _title. Devuelve false y el motivo en error si el archivo no es válido.
        /// </summary>
        public static bool TryImportFromFile(SongData song, string path, out int imported, out string error)
        {
            imported = 0;
            error = null;

            ChartFile chart;
            try { chart = JsonUtility.FromJson<ChartFile>(File.ReadAllText(path, Encoding.UTF8)); }
            catch (System.Exception e)
            {
                error = "No se pudo leer el JSON:\n" + e.Message;
                return false;
            }

            if (chart == null || chart.notes == null || chart.notes.Length == 0)
            {
                error = "El JSON no tiene notas.";
                return false;
            }
            if (chart.bpm <= 0f)
            {
                error = "El JSON necesita un bpm mayor a 0.";
                return false;
            }

            var so = new SerializedObject(song);
            var notesProp = so.FindProperty("_notes");
            var bpmProp = so.FindProperty("_bpm");
            var titleProp = so.FindProperty("_title");
            if (notesProp == null || bpmProp == null)
            {
                error = "No se encontraron los campos _notes/_bpm en SongData. Revisar nombres de campos.";
                return false;
            }

            Undo.RecordObject(song, "Importar chart JSON");

            double secondsPerBeat = 60.0 / chart.bpm;
            double offset = chart.globalOffsetMs / 1000.0;
            int skipped = 0;

            notesProp.arraySize = chart.notes.Length;
            int write = 0;
            foreach (var n in chart.notes)
            {
                if (n.lane < 0 || n.lane > 3) { skipped++; continue; }
                double t = n.beat >= 0f ? chart.firstBeatSeconds + n.beat * secondsPerBeat : n.time;
                if (t < 0.0) { skipped++; continue; }
                t += offset;

                var el = notesProp.GetArrayElementAtIndex(write++);
                el.FindPropertyRelative("targetTimeDSP").doubleValue = t;
                el.FindPropertyRelative("laneIndex").intValue = n.lane;
                el.FindPropertyRelative("isDebuffNote").boolValue = n.isDebuffNote;
            }
            notesProp.arraySize = write;

            bpmProp.floatValue = chart.bpm;
            if (titleProp != null && !string.IsNullOrEmpty(chart.title)) titleProp.stringValue = chart.title;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(song);
            AssetDatabase.SaveAssets();

            imported = write;
            Debug.Log($"[Charts] {song.name}: {write} notas importadas desde {Path.GetFileName(path)} " +
                      $"(bpm {chart.bpm.ToString(CultureInfo.InvariantCulture)}, offset {chart.globalOffsetMs} ms" +
                      (skipped > 0 ? $", {skipped} ignoradas por carril/tiempo invalido" : "") + ").", song);
            return true;
        }

        // ---------- Exportar (para editar a mano y volver a importar) ----------

        [MenuItem("CONTEXT/SongData/Exportar chart a JSON...")]
        private static void ExportFromContext(MenuCommand command)
        {
            var song = (SongData)command.context;
            string path = EditorUtility.SaveFilePanel("Exportar chart", Application.dataPath, song.name + "_chart", "json");
            if (string.IsNullOrEmpty(path)) return;

            var notes = song.Notes ?? new NoteData[0];
            var chart = new ChartFile
            {
                songResource = song.name,
                title = song.Title,
                bpm = song.Bpm,
                firstBeatSeconds = 0.0,
                globalOffsetMs = 0.0,
                notes = new ChartNote[notes.Length]
            };
            for (int i = 0; i < notes.Length; i++)
            {
                chart.notes[i] = new ChartNote
                {
                    beat = -1f,                    // se exporta en segundos absolutos
                    time = notes[i].targetTimeDSP,
                    lane = notes[i].laneIndex,
                    isDebuffNote = notes[i].isDebuffNote
                };
            }
            File.WriteAllText(path, JsonUtility.ToJson(chart, true), Encoding.UTF8);
            Debug.Log($"[Charts] {song.name}: {notes.Length} notas exportadas a {path}", song);
        }
    }
}
#endif
