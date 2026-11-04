using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>
    /// Genera placeholders (sprites, WAV de metrónomo, prefab, SongData) y arma la escena de prueba M01/M02.
    /// Idempotente: se puede ejecutar varias veces; reutiliza lo que ya exista.
    /// Menú: DiosPunk/Rhythm/Build Test Setup.
    /// </summary>
    public static class RhythmTestSetupBuilder
    {
        private const string ArtDir = "Assets/_Project/Art/Placeholders";
        private const string AudioDir = "Assets/_Project/Audio/Placeholders";
        private const string PrefabDir = "Assets/_Project/Prefabs";
        private const string SongDir = "Assets/_Project/Data/Songs";

        private const string NotePngPath = ArtDir + "/Note_Placeholder.png";
        private const string HitBarPngPath = ArtDir + "/HitBar_Placeholder.png";
        private const string WavPath = AudioDir + "/Metronome_120BPM.wav";
        private const string NotePrefabPath = PrefabDir + "/Note.prefab";
        private const string SongPath = SongDir + "/TestSong_120BPM.asset";

        // Parámetros del chart de prueba (deben coincidir entre WAV y SongData).
        private const float Bpm = 120f;
        private const float FirstBeatSeconds = 2f;
        private const int BeatCount = 64;

        // Geometría: 4 carriles a 1.5 u => la barra cubre 6 u de ancho. PPU 100.
        private const int NotePxW = 120, NotePxH = 40;
        private const int HitBarPxW = 600, HitBarPxH = 12;
        private const float HitLineY = -3.5f;

        [MenuItem("DiosPunk/Rhythm/Build Test Setup")]
        public static void Build()
        {
            EnsureFolders();

            Sprite noteSprite = CreateBorderedSprite(NotePngPath, NotePxW, NotePxH, 4,
                new Color(0.75f, 0.75f, 0.75f, 1f), Color.white);
            Sprite hitBarSprite = CreateBorderedSprite(HitBarPngPath, HitBarPxW, HitBarPxH, 2,
                new Color(1f, 0.85f, 0.1f, 1f), new Color(1f, 1f, 0.7f, 1f));

            AudioClip clip = CreateMetronomeClip();
            SongData song = CreateSongData(clip);
            NoteView notePrefab = CreateNotePrefab(noteSprite);

            BuildScene(song, notePrefab, hitBarSprite, noteSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Rhythm] Test setup listo. Dale Play en SampleScene.");
        }

        // ---------------- Assets ----------------

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(ArtDir);
            Directory.CreateDirectory(AudioDir);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SongDir);
            AssetDatabase.Refresh();
        }

        private static Sprite CreateBorderedSprite(string path, int w, int h, int border, Color fill, Color edge)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isEdge = x < border || y < border || x >= w - border || y >= h - border;
                    pixels[y * w + x] = isEdge ? edge : fill;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static AudioClip CreateMetronomeClip()
        {
            WriteClickTrackWav(WavPath, Bpm, FirstBeatSeconds, BeatCount, 1.5f);
            AssetDatabase.ImportAsset(WavPath, ImportAssetOptions.ForceUpdate);

            // PCM sin compresión: timing exacto y sin latencia de decodificación.
            var importer = (AudioImporter)AssetImporter.GetAtPath(WavPath);
            importer.forceToMono = true;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<AudioClip>(WavPath);
        }

        /// <summary>Clicks a BPM fijo: 1600 Hz en el primer tiempo de cada compás de 4, 1000 Hz en el resto.</summary>
        private static void WriteClickTrackWav(string path, float bpm, double firstBeat, int beats, float tailSeconds)
        {
            const int rate = 44100;
            double beat = 60.0 / bpm;
            int total = (int)((firstBeat + beats * beat + tailSeconds) * rate);
            var samples = new short[total];
            int clickLen = (int)(0.05 * rate);

            for (int b = 0; b < beats; b++)
            {
                int start = (int)Math.Round((firstBeat + b * beat) * rate);
                float freq = (b % 4 == 0) ? 1600f : 1000f;
                for (int n = 0; n < clickLen && start + n < total; n++)
                {
                    float t = n / (float)rate;
                    float env = Mathf.Exp(-t * 80f);
                    float s = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.8f;
                    samples[start + n] = (short)(s * short.MaxValue);
                }
            }

            int dataLen = total * 2;
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(new[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataLen);
                bw.Write(new[] { 'W', 'A', 'V', 'E' });
                bw.Write(new[] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1);       // PCM
                bw.Write((short)1);       // mono
                bw.Write(rate);
                bw.Write(rate * 2);       // byte rate
                bw.Write((short)2);       // block align
                bw.Write((short)16);      // bits
                bw.Write(new[] { 'd', 'a', 't', 'a' });
                bw.Write(dataLen);
                for (int i = 0; i < samples.Length; i++) bw.Write(samples[i]);
            }
        }

        private static SongData CreateSongData(AudioClip clip)
        {
            var song = AssetDatabase.LoadAssetAtPath<SongData>(SongPath);
            if (song == null)
            {
                song = ScriptableObject.CreateInstance<SongData>();
                AssetDatabase.CreateAsset(song, SongPath);
            }

            var so = new SerializedObject(song);
            so.FindProperty("_title").stringValue = "Test Song (Metronome 120 BPM)";
            so.FindProperty("_audioClip").objectReferenceValue = clip;
            so.FindProperty("_bpm").floatValue = Bpm;
            so.FindProperty("_testFirstBeatSeconds").floatValue = FirstBeatSeconds;
            so.FindProperty("_testBeatCount").intValue = BeatCount;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Reutiliza el generador del ContextMenu de SongData (1 nota por beat, 4 carriles).
            var method = typeof(SongData).GetMethod("GenerateTestChart",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException("SongData.GenerateTestChart no encontrado.");
            method.Invoke(song, null);

            EditorUtility.SetDirty(song);
            AssetDatabase.SaveAssetIfDirty(song);
            return song;
        }

        private static NoteView CreateNotePrefab(Sprite noteSprite)
        {
            var go = new GameObject("Note");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = noteSprite;
            sr.sortingOrder = 10;
            var view = go.AddComponent<NoteView>();

            var so = new SerializedObject(view);
            so.FindProperty("_renderer").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, NotePrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return saved.GetComponent<NoteView>();
        }

        // ---------------- Escena ----------------

        private static void BuildScene(SongData song, NoteView notePrefab, Sprite hitBarSprite, Sprite padSprite)
        {
            // --- Track ---
            GameObject track = FindOrCreate("RhythmTrack");
            track.transform.position = Vector3.zero;

            var clock = GetOrAdd<SongClock>(track);
            var input = GetOrAdd<RhythmInputReader>(track);
            var controller = GetOrAdd<RhythmTrackController>(track);
            var audioSource = track.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;

            SetRefs(clock, ("_audioSource", audioSource));
            SetRefs(controller,
                ("_clock", clock),
                ("_input", input),
                ("_song", song),
                ("_notePrefab", notePrefab));

            // --- Hit bar ---
            Transform existingBar = track.transform.Find("HitBar");
            GameObject bar = existingBar != null ? existingBar.gameObject : new GameObject("HitBar");
            bar.transform.SetParent(track.transform, false);
            bar.transform.localPosition = new Vector3(0f, HitLineY, 0f);
            var barRenderer = GetOrAdd<SpriteRenderer>(bar);
            barRenderer.sprite = hitBarSprite;
            barRenderer.sortingOrder = 5;

            // --- Pads de feedback por carril (se tiñen Verde/Amarillo/Rojo al juzgar) ---
            var pads = new SpriteRenderer[RhythmInputReader.LaneCount];
            for (int lane = 0; lane < pads.Length; lane++)
            {
                string padName = "LanePad_" + lane;
                Transform existingPad = track.transform.Find(padName);
                GameObject pad = existingPad != null ? existingPad.gameObject : new GameObject(padName);
                pad.transform.SetParent(track.transform, false);
                pad.transform.localPosition = new Vector3((lane - (pads.Length - 1) * 0.5f) * 1.5f, HitLineY, 0f);
                pad.transform.localScale = new Vector3(1.15f, 1.3f, 1f);
                var padRenderer = GetOrAdd<SpriteRenderer>(pad);
                padRenderer.sprite = padSprite;
                padRenderer.sortingOrder = 6;
                padRenderer.color = Color.clear;
                pads[lane] = padRenderer;
            }

            var feedback = GetOrAdd<RhythmHitFeedback>(track);
            SetRefs(feedback, ("_track", controller), ("_input", input));
            SetRefArray(feedback, "_lanePads", pads);

            var autoPlayer = GetOrAdd<RhythmDebugAutoPlayer>(track);
            SetRefs(autoPlayer, ("_track", controller), ("_input", input));

            // --- Cámara ---
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1f);

            // --- HUD ---
            GameObject canvasGo = FindOrCreate("DebugCanvas");
            var canvas = GetOrAdd<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetOrAdd<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            TMP_Text judgement = FindOrCreateText(canvasGo.transform, "JudgementText", 64f,
                TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 90f));
            TMP_Text stats = FindOrCreateText(canvasGo.transform, "StatsText", 36f,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(700f, 200f));
            TMP_Text metrics = FindOrCreateText(canvasGo.transform, "MetricsText", 30f,
                TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(30f, -150f), new Vector2(700f, 50f));
            TMP_Text status = FindOrCreateText(canvasGo.transform, "StatusText", 28f,
                TextAlignmentOptions.TopRight, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(900f, 150f));
            TMP_Text gc = FindOrCreateText(canvasGo.transform, "GcText", 26f,
                TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(1000f, 50f));
            judgement.text = "-";

            GameObject hudGo = FindOrCreate("RhythmDebugHUD");
            var hud = GetOrAdd<RhythmDebugHUD>(hudGo);
            SetRefs(hud, ("_track", controller), ("_judgementText", judgement), ("_statsText", stats),
                ("_metricsText", metrics));

            var tools = GetOrAdd<RhythmDebugTools>(hudGo);
            SetRefs(tools, ("_track", controller), ("_autoPlayer", autoPlayer), ("_statusText", status),
                ("_gcText", gc));

            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject FindOrCreate(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            }
            return go;
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        private static TMP_Text FindOrCreateText(Transform parent, string name, float size,
            TextAlignmentOptions align, Vector2 anchor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
            }

            var text = GetOrAdd<TextMeshProUGUI>(go);
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return text;
        }

        private static void SetRefArray(UnityEngine.Object target, string prop, UnityEngine.Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(prop);
            if (p == null) throw new MissingFieldException(target.GetType().Name, prop);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefs(UnityEngine.Object target, params (string prop, UnityEngine.Object value)[] refs)
        {
            var so = new SerializedObject(target);
            for (int i = 0; i < refs.Length; i++)
            {
                SerializedProperty p = so.FindProperty(refs[i].prop);
                if (p == null) throw new MissingFieldException(target.GetType().Name, refs[i].prop);
                p.objectReferenceValue = refs[i].value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
