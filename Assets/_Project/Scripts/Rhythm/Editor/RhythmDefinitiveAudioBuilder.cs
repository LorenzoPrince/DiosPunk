using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DiosPunk.EditorTools;
using UnityEditor;
using UnityEngine;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>
    /// Integra el audio definitivo y el chart real que entrega el equipo en Assets/_Project/Imports:
    ///  - SFX "Fx_*_NN" (NN = número de la guía de audio): se asignan a la SfxLibrary por rangos y se vuelven a
    ///    mapear en cada corrida, así que alcanza con soltar los archivos nuevos en Imports/Audio y regenerar.
    ///  - Música: nivel 1 (mezcla completa + capas #41/#42), menú principal (#35) y pre-nivel (#36).
    ///  - Chart real del nivel 1 (JSON) importado con SongChartImporter.
    /// Un efecto sin archivo definitivo conserva su placeholder (o queda en silencio si nunca tuvo uno).
    /// </summary>
    public static partial class RhythmTestSetupBuilder
    {
        private const string ImportAudioDir = "Assets/_Project/Imports/Audio";
        private const string Level1ChartPath = "Assets/_Project/Imports/Charts/Song_01_Comienzos_chart.json";
        private const string Level1MixPath = ImportAudioDir + "/Nivel 1 MUESTRA.mp3";
        private const string Level1BackingPath = ImportAudioDir + "/Mx_Nivel1bk_41.mp3";
        private const string Level1LeadPath = ImportAudioDir + "/Mx_Nivel1p_42.mp3";

        private static readonly Regex s_numberedFile = new Regex(@"_(\d{1,2})\.(wav|mp3|ogg)$", RegexOptions.IgnoreCase);

        private struct DefinitiveSfx
        {
            public SfxId id;
            public int from, to;
            public float volume, pitchMin, pitchMax;
        }

        // Rangos de la guía de audio (#NN). Las tomas de un rango son variantes que no se repiten seguidas.
        private static readonly DefinitiveSfx[] s_definitive =
        {
            new DefinitiveSfx { id = SfxId.CrowdShout, from = 0, to = 5, volume = 0.9f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.Cheer, from = 6, to = 9, volume = 0.9f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.PunkShout, from = 10, to = 16, volume = 1f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.Motto, from = 17, to = 30, volume = 1f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.ResultExcellent, from = 31, to = 31, volume = 1f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.ResultGood, from = 32, to = 32, volume = 1f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.LevelStart, from = 33, to = 33, volume = 1f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.Splash, from = 34, to = 34, volume = 0.9f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.UiClick, from = 38, to = 38, volume = 0.8f, pitchMin = 0.98f, pitchMax = 1.02f },
            new DefinitiveSfx { id = SfxId.UiHover, from = 39, to = 39, volume = 0.5f, pitchMin = 0.98f, pitchMax = 1.02f },
            new DefinitiveSfx { id = SfxId.UiImportant, from = 43, to = 43, volume = 0.9f, pitchMin = 1f, pitchMax = 1f },
            new DefinitiveSfx { id = SfxId.Pen, from = 44, to = 48, volume = 0.6f, pitchMin = 0.95f, pitchMax = 1.05f },
            new DefinitiveSfx { id = SfxId.PageTurn, from = 49, to = 52, volume = 0.8f, pitchMin = 0.97f, pitchMax = 1.03f }
        };

        // ---------------- SFX definitivos ----------------

        /// <summary>Asigna a la SfxLibrary los clips Fx_*_NN presentes en Imports/Audio. Devuelve los números sin archivo.</summary>
        private static void MapDefinitiveSfx(SfxLibrary library)
        {
            if (!Directory.Exists(ImportAudioDir)) return;

            // número de la guía -> ruta del asset
            var byNumber = new SortedDictionary<int, string>();
            foreach (string file in Directory.GetFiles(ImportAudioDir))
            {
                string name = Path.GetFileName(file);
                if (!name.StartsWith("Fx_", StringComparison.OrdinalIgnoreCase)) continue;
                Match m = s_numberedFile.Match(name);
                if (!m.Success) continue;
                byNumber[int.Parse(m.Groups[1].Value)] = ImportAudioDir + "/" + name;
            }

            var so = new SerializedObject(library);
            var missing = new List<int>();
            var mapped = new List<string>();

            foreach (DefinitiveSfx def in s_definitive)
            {
                var clips = new List<AudioClip>();
                for (int n = def.from; n <= def.to; n++)
                {
                    if (!byNumber.TryGetValue(n, out string path)) { missing.Add(n); continue; }
                    ImportSfxClip(path);
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null) clips.Add(clip);
                }
                if (clips.Count == 0) continue;

                SerializedProperty entry = so.FindProperty(LibraryField(def.id));
                if (entry == null) throw new MissingFieldException(nameof(SfxLibrary), LibraryField(def.id));
                SerializedProperty prop = entry.FindPropertyRelative("clips");

                // No se pisa nada asignado a mano fuera de los placeholders y de Imports/Audio.
                if (!AssignableWithPlaceholders(prop, ImportAudioDir)) continue;

                prop.arraySize = clips.Count;
                for (int i = 0; i < clips.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                entry.FindPropertyRelative("volume").floatValue = def.volume;
                entry.FindPropertyRelative("pitchMin").floatValue = def.pitchMin;
                entry.FindPropertyRelative("pitchMax").floatValue = def.pitchMax;
                mapped.Add(def.id + " x" + clips.Count);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);

            Debug.Log("[Audio] SFX definitivos asignados: " + (mapped.Count > 0 ? string.Join(", ", mapped) : "ninguno") +
                      ". Sin archivo en Imports/Audio (numeros de la guia): " + (missing.Count > 0 ? string.Join(", ", missing) : "ninguno") + ".");
        }

        /// <summary>SFX corto: PCM sin comprimir descomprimido al cargar (latencia mínima), sin forzar mono.</summary>
        private static void ImportSfxClip(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;

            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            if (!importer.forceToMono && s.loadType == AudioClipLoadType.DecompressOnLoad &&
                s.compressionFormat == AudioCompressionFormat.PCM) return;

            importer.forceToMono = false;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = s;
            importer.SaveAndReimport();
        }

        // ---------------- Música ----------------

        /// <summary>Clip de música de menú / pre-nivel por número de guía (#35, #36). null si el archivo no está.</summary>
        private static AudioClip LoadMenuMusicClip(int number)
        {
            if (!Directory.Exists(ImportAudioDir)) return null;
            foreach (string file in Directory.GetFiles(ImportAudioDir))
            {
                string name = Path.GetFileName(file);
                if (!name.StartsWith("Mx_", StringComparison.OrdinalIgnoreCase)) continue;
                Match m = s_numberedFile.Match(name);
                if (!m.Success || int.Parse(m.Groups[1].Value) != number) continue;

                string path = ImportAudioDir + "/" + name;
                ImportStreamingMusic(path);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            return null;
        }

        /// <summary>Música larga de loop: se reproduce en streaming (no ocupa memoria descomprimida).</summary>
        private static void ImportStreamingMusic(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return;

            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            if (!importer.forceToMono && s.loadType == AudioClipLoadType.Streaming &&
                s.compressionFormat == AudioCompressionFormat.Vorbis) return;

            importer.forceToMono = false;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            importer.defaultSampleSettings = s;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Canción jugable: PCM estéreo descomprimido al cargar (timing exacto con PlayScheduled, sin latencia de
        /// decodificación). Las capas de apoyo, que no se reproducen todavía, quedan comprimidas y sin precarga para no
        /// sumar memoria al cargar el SongData.
        /// </summary>
        private static AudioClip ImportSongMusic(string path, bool playable)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return null;

            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            AudioClipLoadType loadType = playable ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            AudioCompressionFormat format = playable ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;

            if (importer.forceToMono || s.loadType != loadType || s.compressionFormat != format || importer.preloadAudioData != playable)
            {
                importer.forceToMono = false;
                s.loadType = loadType;
                s.compressionFormat = format;
                s.quality = 0.7f;
                importer.defaultSampleSettings = s;
                importer.preloadAudioData = playable;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // ---------------- Nivel 1 real ----------------

        /// <summary>
        /// Nivel 1 "Volver a soñar": audio definitivo (mezcla completa) + capas #41/#42 + chart real del JSON (75 notas,
        /// 101 BPM). Devuelve null si faltan el audio o el chart (el generador cae al placeholder).
        /// </summary>
        private static SongData ConfigureRealLevel1(string songPath)
        {
            if (!File.Exists(Level1MixPath) || !File.Exists(Level1ChartPath)) return null;

            AudioClip mix = ImportSongMusic(Level1MixPath, true);
            AudioClip backing = File.Exists(Level1BackingPath) ? ImportSongMusic(Level1BackingPath, false) : null;
            AudioClip lead = File.Exists(Level1LeadPath) ? ImportSongMusic(Level1LeadPath, false) : null;
            if (mix == null) return null;

            var song = AssetDatabase.LoadAssetAtPath<SongData>(songPath);
            if (song == null)
            {
                song = ScriptableObject.CreateInstance<SongData>();
                AssetDatabase.CreateAsset(song, songPath);
            }

            var so = new SerializedObject(song);
            so.FindProperty("_audioClip").objectReferenceValue = mix;
            so.FindProperty("_punkStemClip").objectReferenceValue = null; // el nivel 1 no usa Modo Punk
            so.FindProperty("_backingTrackClip").objectReferenceValue = backing;
            so.FindProperty("_leadLineClip").objectReferenceValue = lead;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Notas, BPM (101) y título ("Volver a soñar") salen del JSON.
            string absolute = Path.GetFullPath(Level1ChartPath);
            if (!SongChartImporter.TryImportFromFile(song, absolute, out int imported, out string error))
            {
                Debug.LogError("[Charts] No se pudo importar el chart del nivel 1: " + error);
                return null;
            }

            EditorUtility.SetDirty(song);
            AssetDatabase.SaveAssetIfDirty(song);
            Debug.Log("[Levels] Nivel 1 real: " + imported + " notas, " + song.Bpm.ToString("0.#") + " BPM, audio " + mix.length.ToString("F1") + " s.");
            return song;
        }
    }
}
