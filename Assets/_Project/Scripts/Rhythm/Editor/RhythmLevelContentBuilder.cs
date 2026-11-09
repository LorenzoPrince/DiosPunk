using System.IO;
using DiosPunk.Flow;
using UnityEditor;
using UnityEngine;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>
    /// Contenido de los 5 niveles: LevelData, DialogueData (arco del GDD), SongData con su chart y su audio
    /// placeholder (metrónomo a distinto BPM), y el epílogo. Los textos son un BORRADOR escrito solo a partir del
    /// GDD (lugares, personajes y tono); el equipo debe revisarlos contra la historia real antes de publicar.
    /// </summary>
    public static partial class RhythmTestSetupBuilder
    {
        private const string EpiloguePath = DialogueDir + "/Dialogue_Epilogue.asset";

        private struct LevelSpec
        {
            public string title, stage, difficulty, songName;
            public float bpm;
            public int beats;
            public int offbeatEvery;
            public int warmupBeats;       // beats iniciales a media densidad
            public float speed;           // multiplicador de velocidad de caída del nivel
        }

        // Dificultad creciente y orgánica: BPM, duración y contratiempos suben de a poco; el nivel 2 entra con
        // media densidad y cae más lento para que el paso desde el nivel 1 no sea un salto brusco.
        private static readonly LevelSpec[] Specs =
        {
            new LevelSpec { title = "Comienzos", stage = "El cuarto", difficulty = "Facil", songName = "Song_01_Comienzos", bpm = 100f, beats = 56, offbeatEvery = 0, warmupBeats = 0, speed = 1.00f },
            new LevelSpec { title = "Sueños Punk Rock", stage = "El garaje", difficulty = "Facil", songName = "Song_02_SuenosPunkRock", bpm = 110f, beats = 64, offbeatEvery = 12, warmupBeats = 16, speed = 0.90f },
            new LevelSpec { title = "Peatonal Córdoba", stage = "La peatonal", difficulty = "Media", songName = "Song_03_PeatonalCordoba", bpm = 120f, beats = 72, offbeatEvery = 4, warmupBeats = 0, speed = 0.95f },
            new LevelSpec { title = "Facultad de Medicina", stage = "Las escalinatas", difficulty = "Media", songName = "Song_04_FacultadDeMedicina", bpm = 130f, beats = 80, offbeatEvery = 2, warmupBeats = 0, speed = 1.00f },
            new LevelSpec { title = "Lucha Interior", stage = "El escenario final", difficulty = "Dificil", songName = "Song_05_LuchaInterior", bpm = 140f, beats = 96, offbeatEvery = 1, warmupBeats = 0, speed = 1.05f }
        };

        // ---------------- Canciones (audio + chart) ----------------

        private static SongData[] CreateLevelSongs()
        {
            Directory.CreateDirectory(SongDir);
            var songs = new SongData[Specs.Length];

            for (int i = 0; i < Specs.Length; i++)
            {
                LevelSpec spec = Specs[i];
                string wavPath = $"{AudioDir}/Level{i + 1}_{spec.bpm:0}BPM.wav";
                WriteClickTrackWav(wavPath, spec.bpm, FirstBeatSeconds, spec.beats, 1.5f);
                AssetDatabase.ImportAsset(wavPath, ImportAssetOptions.ForceUpdate);

                var importer = (AudioImporter)AssetImporter.GetAtPath(wavPath);
                importer.forceToMono = true;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(wavPath);

                string songPath = $"{SongDir}/{spec.songName}.asset";
                var song = AssetDatabase.LoadAssetAtPath<SongData>(songPath);
                if (song == null)
                {
                    song = ScriptableObject.CreateInstance<SongData>();
                    AssetDatabase.CreateAsset(song, songPath);
                }

                var so = new SerializedObject(song);
                so.FindProperty("_title").stringValue = spec.title + " (placeholder " + spec.bpm.ToString("0") + " BPM)";
                so.FindProperty("_audioClip").objectReferenceValue = clip;
                so.FindProperty("_bpm").floatValue = spec.bpm;
                so.FindProperty("_testFirstBeatSeconds").floatValue = FirstBeatSeconds;
                so.FindProperty("_testBeatCount").intValue = spec.beats;
                so.FindProperty("_testOffbeatEvery").intValue = spec.offbeatEvery;
                so.FindProperty("_testWarmupBeats").intValue = spec.warmupBeats;
                so.ApplyModifiedPropertiesWithoutUndo();

                var method = typeof(SongData).GetMethod("GenerateTestChart",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                method.Invoke(song, null);

                EditorUtility.SetDirty(song);
                AssetDatabase.SaveAssetIfDirty(song);
                songs[i] = song;
            }
            return songs;
        }

        // ---------------- Niveles ----------------

        private static LevelData[] CreateLevels(DialogueData[] dialogues)
        {
            var levels = new LevelData[Specs.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                string path = $"{LevelDir}/Level_{i + 1:00}.asset";
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                if (level == null)
                {
                    level = ScriptableObject.CreateInstance<LevelData>();
                    AssetDatabase.CreateAsset(level, path);
                }

                var so = new SerializedObject(level);
                so.FindProperty("_title").stringValue = Specs[i].title;
                so.FindProperty("_stage").stringValue = Specs[i].stage;
                so.FindProperty("_difficulty").stringValue = Specs[i].difficulty;
                so.FindProperty("_songResource").stringValue = Specs[i].songName;
                so.FindProperty("_dialogue").objectReferenceValue = dialogues[i];
                so.FindProperty("_noteSpeedMultiplier").floatValue = Specs[i].speed;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(level);
                levels[i] = level;
            }
            return levels;
        }

        /// <summary>Comprueba que cada nivel enlaza narrativa, preparación (datos) y chart.</summary>
        private static void ValidateLevels(LevelCatalog catalog)
        {
            bool ok = true;
            for (int i = 0; i < catalog.Count; i++)
            {
                LevelData level = catalog.Levels[i];
                SongData song = level != null ? level.LoadSong() : null;
                string problem = level == null ? "LevelData vacio"
                    : level.Dialogue == null || level.Dialogue.Lines.Length == 0 ? "sin dialogo"
                    : song == null ? "no se encuentra la cancion '" + level.SongResource + "' en Resources/Songs"
                    : song.Notes.Length == 0 ? "chart vacio"
                    : song.AudioClip == null ? "cancion sin audio"
                    : null;

                if (problem != null)
                {
                    ok = false;
                    Debug.LogError($"[Levels] Nivel {i + 1}: {problem}");
                }
            }
            if (ok) Debug.Log("[Levels] Los " + catalog.Count + " niveles enlazan narrativa, cancion, audio y chart.");
        }

        // ---------------- Diálogos ----------------

        private struct Line
        {
            public string speaker, text;
            public bool portrait;
            public Line(string speaker, string text, bool portrait = false) { this.speaker = speaker; this.text = text; this.portrait = portrait; }
        }

        private static DialogueData[] CreateStoryDialogues(Sprite portrait)
        {
            Directory.CreateDirectory(DialogueDir);
            var dialogues = new DialogueData[5];

            dialogues[0] = WriteDialogue("Dialogue_01", portrait, null, null, new[]
            {
                new Line("Narrador", "Rosario. Un cuarto chico, una guitarra con las cuerdas justas y muchas ganas de que alguien escuche."),
                new Line("Dios Punk", "Tres acordes alcanzan. Este cuarto suena más fuerte que cualquier estadio.", true),
                new Line("Mamá", "¿Otra vez ensayando? Bajá un poquito el volumen, que los vecinos también tienen oídos."),
                new Line("Dios Punk", "Mamá, los vecinos son mi primer público.", true),
                new Line("Narrador", "Así empezó todo: con ruido, con humor y sin pedir permiso.")
            });

            dialogues[1] = WriteDialogue("Dialogue_02", portrait, "¿Cómo encarás el primer recital?", new[]
            {
                new DialogueChoice { label = "Con calma", variable = ChoiceVariable.Difficulty, value = -1 },
                new DialogueChoice { label = "Como siempre", variable = ChoiceVariable.Difficulty, value = 0 },
                new DialogueChoice { label = "A todo volumen", variable = ChoiceVariable.Difficulty, value = 1 }
            }, new[]
            {
                new Line("Narrador", "Un garaje, amplificadores prestados y una banda con nombre de sueño: Sueños Punk Rock."),
                new Line("Compañero de banda", "Si no sale prolijo, que salga fuerte."),
                new Line("Dios Punk", "Prolijo es para los discos. Nosotros somos de vivo.", true),
                new Line("Amigo", "Hay un bar que nos da la fecha del sábado. ¿Nos animamos?"),
                new Line("Dios Punk", "Claro. Total, lo peor que puede pasar es que suene a punk.", true)
            });

            dialogues[2] = WriteDialogue("Dialogue_03", portrait, "¿Dónde te parás hoy?", new[]
            {
                new DialogueChoice { label = "En la esquina de siempre", variable = ChoiceVariable.ScenarioVariant, value = 0 },
                new DialogueChoice { label = "Frente a la vidriera", variable = ChoiceVariable.ScenarioVariant, value = 1 }
            }, new[]
            {
                new Line("Narrador", "La peatonal Córdoba al mediodía: pasos apurados, vidrieras y un tipo con una guitarra."),
                new Line("Transeúnte", "¿Eso es música o se te cayó la púa al piso?"),
                new Line("Dios Punk", "Es lo que tengo y lo toco igual.", true),
                new Line("Otro transeúnte", "Che, al menos tiene actitud..."),
                new Line("Narrador", "Algunos pasan de largo. Otros se frenan a escuchar. Alcanza con que uno se frene.")
            });

            dialogues[3] = WriteDialogue("Dialogue_04", portrait, "¿Qué le respondés al purista?", new[]
            {
                new DialogueChoice { label = "Con humor", variable = ChoiceVariable.Difficulty, value = -1 },
                new DialogueChoice { label = "Con una canción", variable = ChoiceVariable.Difficulty, value = 0 },
                new DialogueChoice { label = "Más fuerte todavía", variable = ChoiceVariable.Difficulty, value = 1 }
            }, new[]
            {
                new Line("Narrador", "Las escalinatas de la Facultad de Medicina: buen eco, cero permisos y público con apuntes bajo el brazo."),
                new Line("Purista", "Eso no es punk de verdad. Falta mugre y sobra entusiasmo."),
                new Line("Dios Punk", "El punk es hacerlo vos mismo. Hasta el cartel lo pinté yo.", true),
                new Line("Amigo", "Los volantes quedaron fotocopiados torcidos."),
                new Line("Dios Punk", "Mejor. Así se nota que están hechos a mano.", true),
                new Line("Narrador", "Sin sellos, sin productores, sin permiso: autogestión pura.")
            });

            dialogues[4] = WriteDialogue("Dialogue_05", portrait, "¿Cómo encarás esta canción?", new[]
            {
                new DialogueChoice { label = "Paso a paso", variable = ChoiceVariable.Difficulty, value = -1 },
                new DialogueChoice { label = "Con todo", variable = ChoiceVariable.Difficulty, value = 0 },
                new DialogueChoice { label = "Sin red", variable = ChoiceVariable.Difficulty, value = 1 }
            }, new[]
            {
                new Line("Narrador", "No todo el ruido viene de afuera. A veces el más difícil está adentro."),
                new Line("Dios Punk", "Hay días en que cuesta hasta levantar la guitarra.", true),
                new Line("Amigo", "Pero igual la levantás."),
                new Line("Dios Punk", "Porque si me quedo callado, gana el silencio. Así que... a tocar.", true),
                new Line("Narrador", "Esta es la canción más larga. Y también la más tuya.")
            });

            return dialogues;
        }

        private static DialogueData CreateEpilogue(Sprite portrait)
        {
            return WriteDialogue("Dialogue_Epilogue", portrait, null, null, new[]
            {
                new Line("Narrador", "El Dios Punk tocó en cuartos, garajes, bares, escalinatas y calles. En todos lados con la misma convicción."),
                new Line("Narrador", "Este juego no cuenta cómo terminó la historia. Cuenta cómo sonaba."),
                new Line("Narrador", "Sonaba a fotocopia, a pegamento, a amplificador prendido y a ganas de ser escuchado."),
                new Line("Narrador", "Gracias por tocar con él. Que el ruido siga."),
                new Line("Dios Punk", "¡Uno, dos, tres, cuatro!", true)
            });
        }

        private static DialogueData WriteDialogue(string name, Sprite portrait, string choicePrompt, Line[] lines)
        {
            return WriteDialogue(name, portrait, choicePrompt, null, lines);
        }

        private static DialogueData WriteDialogue(string name, Sprite portrait, string choicePrompt, DialogueChoice[] choices, Line[] lines)
        {
            string path = $"{DialogueDir}/{name}.asset";
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<DialogueData>();
                AssetDatabase.CreateAsset(data, path);
            }

            var so = new SerializedObject(data);
            SerializedProperty linesProp = so.FindProperty("_lines");
            linesProp.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
                SetDialogueLine(linesProp, i, lines[i].speaker, lines[i].text, lines[i].portrait ? portrait : null);

            so.FindProperty("_choicePrompt").stringValue = choicePrompt ?? string.Empty;
            SerializedProperty choicesProp = so.FindProperty("_choices");
            int count = choices != null ? choices.Length : 0;
            choicesProp.arraySize = count;
            for (int i = 0; i < count; i++) SetChoice(choicesProp, i, choices[i].label, choices[i].variable, choices[i].value);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
