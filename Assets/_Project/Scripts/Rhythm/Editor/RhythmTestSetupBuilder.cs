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
    /// Genera placeholders (sprites, WAV de metrónomo, prefab, SongData) y arma la escena de prueba
    /// siguiendo los bocetos del GDD: highway de 4 carriles de colores, receptáculos translúcidos,
    /// HUD con medidor de Modo Punk (arriba izquierda) y puntaje + multiplicador (arriba derecha).
    /// Idempotente: se puede ejecutar varias veces; reutiliza lo que ya exista.
    /// Menú: DiosPunk/Rhythm/Build Test Setup.
    /// </summary>
    public static partial class RhythmTestSetupBuilder
    {
        private const string ArtDir = "Assets/_Project/Art/Placeholders";
        private const string AudioDir = "Assets/_Project/Audio/Placeholders";
        private const string PrefabDir = "Assets/_Project/Prefabs";
        // La canción vive en Resources/Songs para cargarse bajo demanda (el menú no debe cargar el audio).
        private const string SongDir = "Assets/_Project/Resources/Songs";

        private const string NotePngPath = ArtDir + "/Note_Placeholder.png";
        private const string HitBarPngPath = ArtDir + "/HitBar_Placeholder.png";
        private const string PanelPngPath = ArtDir + "/Panel_Placeholder.png";
        private const string WhitePngPath = ArtDir + "/White_Placeholder.png";
        private const string WavPath = AudioDir + "/Metronome_120BPM.wav";
        private const string NotePrefabPath = PrefabDir + "/Note.prefab";
        private const string SongPath = SongDir + "/TestSong_120BPM.asset";

        // Parámetros del chart de prueba (deben coincidir entre WAV y SongData).
        private const float Bpm = 120f;
        private const float FirstBeatSeconds = 2f;
        private const int BeatCount = 64;

        // Geometría: 4 carriles a 1.5 u => highway de 6 u de ancho. PPU 100 (1 u en White = 16 px a PPU 16).
        private const int NotePxW = 156, NotePxH = 52;   // 1.56 x 0.52 u (+30 % sobre 1.2 x 0.4)
        private const int HitBarPxW = 760, HitBarPxH = 12;   // 4 carriles x 1.9 u
        private const float HitLineY = -3.5f;
        private const float LaneSpacing = 1.9f;

        internal static BuiltAssets BuildAssets()
        {
            EnsureFolders();

            // Texturas blancas con borde negro: el color de carril se aplica por tinte (multiplicación).
            Sprite noteSprite = CreateSprite(NotePngPath, NotePxW, NotePxH, 5, Color.white, Color.black, 100f, 0);
            Sprite hitBarSprite = CreateSprite(HitBarPngPath, HitBarPxW, HitBarPxH, 2, Color.white, Color.black, 100f, 0);
            Sprite panelSprite = CreateSprite(PanelPngPath, 64, 64, 6, Color.white, Color.black, 100f, 6);
            Sprite whiteSprite = CreateSprite(WhitePngPath, 16, 16, 0, Color.white, Color.white, 16f, 0);

            AudioClip clip = CreateMetronomeClip();
            SongData song = CreateSongData(clip);
            NoteView notePrefab = CreateNotePrefab(noteSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new BuiltAssets
            {
                Note = noteSprite,
                HitBar = hitBarSprite,
                Panel = panelSprite,
                White = whiteSprite,
                Song = song,
                NotePrefab = notePrefab
            };
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

        private static Sprite CreateSprite(string path, int w, int h, int border, Color fill, Color edge,
            float pixelsPerUnit, int sliceBorder)
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
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = new Vector4(sliceBorder, sliceBorder, sliceBorder, sliceBorder);
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

        /// <summary>Crea en la escena activa la pista (RhythmTrack), la cámara, el HudCanvas con el HUD y las herramientas de debug.</summary>
        private static void BuildTrackAndHud(SongData song, NoteView notePrefab, Sprite hitBarSprite, Sprite noteSprite,
            Sprite panelSprite, Sprite whiteSprite)
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

            // --- Highway (fondo blanco + 5 líneas negras) ---
            float highwayWidth = 4 * LaneSpacing;
            SpriteRenderer highway = FindOrCreateSprite(track.transform, "Highway", whiteSprite,
                new Color(0.93f, 0.93f, 0.90f, 1f), new Vector3(0f, 0.5f, 0f), new Vector3(highwayWidth, 11f, 1f), -10);
            highway.gameObject.name = "Highway";
            for (int i = 0; i < 5; i++)
            {
                FindOrCreateSprite(track.transform, "LaneLine_" + i, whiteSprite, Color.black,
                    new Vector3((i - 2f) * LaneSpacing, 0.5f, 0f), new Vector3(0.07f, 11f, 1f), -9);
            }

            // --- Barra de impacto (línea translúcida) ---
            FindOrCreateSprite(track.transform, "HitBar", hitBarSprite, new Color(0f, 0f, 0f, 0.45f),
                new Vector3(0f, HitLineY, 0f), Vector3.one, 2);

            // --- Receptáculos (forma de la nota, color del carril, translúcidos) y pads de feedback ---
            var pads = new SpriteRenderer[RhythmInputReader.LaneCount];
            for (int lane = 0; lane < pads.Length; lane++)
            {
                float x = (lane - (pads.Length - 1) * 0.5f) * LaneSpacing;
                Color laneColor = controller.GetLaneColor(lane);

                Color receptacle = laneColor;
                receptacle.a = 0.45f;
                FindOrCreateSprite(track.transform, "LaneReceptacle_" + lane, noteSprite, receptacle,
                    new Vector3(x, HitLineY, 0f), Vector3.one, 3);

                pads[lane] = FindOrCreateSprite(track.transform, "LanePad_" + lane, noteSprite, Color.clear,
                    new Vector3(x, HitLineY, 0f), new Vector3(1.15f, 1.3f, 1f), 6);

                // Indicación de las teclas bajo el receptáculo (teclado A S D F / flechas)
                FindOrCreateWorldLabel(track.transform, "KeyLabel_" + lane, KeyLabels[lane],
                    new Vector3(x, HitLineY - 0.62f, 0f));
            }

            var feedback = GetOrAdd<RhythmHitFeedback>(track);
            SetRefs(feedback, ("_track", controller), ("_input", input));
            SetRefArray(feedback, "_lanePads", pads);

            var autoPlayer = GetOrAdd<RhythmDebugAutoPlayer>(track);
            SetRefs(autoPlayer, ("_track", controller), ("_input", input));

            var session = GetOrAdd<RhythmSession>(track);
            SetRefs(session, ("_track", controller));

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
            cam.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);

            // --- Canvas (HUD de juego + debug) ---
            GameObject canvasGo = GameObject.Find("HudCanvas");
            if (canvasGo == null)
            {
                canvasGo = GameObject.Find("DebugCanvas"); // nombre de la versión anterior
                if (canvasGo != null) canvasGo.name = "HudCanvas";
            }
            if (canvasGo == null) canvasGo = FindOrCreate("HudCanvas");

            var canvas = GetOrAdd<Canvas>(canvasGo);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetOrAdd<CanvasScaler>(canvasGo);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            GetOrAdd<GraphicRaycaster>(canvasGo); // sin esto los botones no reciben clics
            Transform ui = canvasGo.transform;

            // Capa del debuff (detrás del HUD): se activa por script mientras dura un debuff
            Image debuffOverlay = FindOrCreateImage(ui, "DebuffOverlay", whiteSprite, new Color(0.75f, 0.1f, 0.85f, 0.35f),
                Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Image.Type.Simple);
            debuffOverlay.rectTransform.anchorMin = Vector2.zero;
            debuffOverlay.rectTransform.anchorMax = Vector2.one;
            debuffOverlay.rectTransform.offsetMin = Vector2.zero;
            debuffOverlay.rectTransform.offsetMax = Vector2.zero;
            debuffOverlay.raycastTarget = false;
            debuffOverlay.enabled = false;

            // Modo Punk (arriba izquierda): panel + rótulo + barra (la actualiza RhythmHudPresenter por eventos)
            RectTransform punkPanel = FindOrCreateImage(ui, "PunkPanel", panelSprite, Color.white,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(520f, 140f), Image.Type.Sliced).rectTransform;
            TMP_Text punkLabel = FindOrCreateText(punkPanel, "PunkLabel", "MODO PUNK", 54f, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -10f), new Vector2(460f, 70f), new Color(0.85f, 0.1f, 0.1f, 1f));
            FindOrCreateImage(punkPanel, "PunkBarBackground", panelSprite, Color.white,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 18f), new Vector2(472f, 36f), Image.Type.Sliced);
            Image punkFill = FindOrCreateImage(punkPanel, "PunkBarFill", whiteSprite, new Color(0.85f, 0.1f, 0.1f, 1f),
                new Vector2(0f, 0f), new Vector2(0f, 0.5f), new Vector2(30f, 36f), new Vector2(460f, 24f), Image.Type.Filled);
            ConfigureFill(punkFill, 0f);

            // Puntaje + multiplicador (arriba derecha)
            RectTransform scorePanel = FindOrCreateImage(ui, "ScorePanel", panelSprite, Color.white,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(560f, 150f), Image.Type.Sliced).rectTransform;
            TMP_Text scoreText = FindOrCreateText(scorePanel, "ScoreText", "0", 80f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(520f, 90f), Color.black);
            TMP_Text multiplierText = FindOrCreateText(scorePanel, "MultiplierText", "x1", 56f, TextAlignmentOptions.BottomRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 8f), new Vector2(200f, 60f), Color.black);
            TMP_Text accuracyText = FindOrCreateText(scorePanel, "AccuracyText", "100.0%", 40f, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 12f), new Vector2(260f, 50f), Color.black);

            // Barra de vida (arriba centro)
            RectTransform healthBar = FindOrCreateImage(ui, "HealthBar", panelSprite, Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(520f, 44f), Image.Type.Sliced).rectTransform;
            Image healthFill = FindOrCreateImage(healthBar, "HealthFill", whiteSprite, new Color(0.15f, 0.7f, 0.25f, 1f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(508f, 30f), Image.Type.Filled);
            ConfigureFill(healthFill, 1f);

            // Debug (abajo / centro): no compiten con el HUD de juego
            TMP_Text judgement = FindOrCreateText(ui, "JudgementText", "-", 56f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(900f, 80f), Color.white);
            TMP_Text debuffText = FindOrCreateText(ui, "DebuffText", "DEBUFF!", 64f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1100f, 90f),
                new Color(0.85f, 0.2f, 0.95f, 1f));
            debuffText.enabled = false;
            TMP_Text stats = FindOrCreateText(ui, "StatsText", null, 30f, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 100f), new Vector2(700f, 90f), Color.white);
            TMP_Text metrics = FindOrCreateText(ui, "MetricsText", null, 26f, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 60f), new Vector2(700f, 40f), Color.white);
            TMP_Text gc = FindOrCreateText(ui, "GcText", null, 22f, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 20f), new Vector2(1000f, 36f), Color.white);
            TMP_Text status = FindOrCreateText(ui, "StatusText", null, 24f, TextAlignmentOptions.BottomRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-30f, 20f), new Vector2(900f, 120f), Color.white);

            // --- Presenter + debug ---
            GameObject presenterGo = FindOrCreate("RhythmHudPresenter");
            var presenter = GetOrAdd<RhythmHudPresenter>(presenterGo);
            SetRefs(presenter, ("_session", session), ("_scoreText", scoreText), ("_multiplierText", multiplierText),
                ("_healthFill", healthFill), ("_accuracyText", accuracyText),
                ("_punkFill", punkFill), ("_punkLabel", punkLabel));

            var debuffVisuals = GetOrAdd<RhythmDebuffVisuals>(track);
            SetRefs(debuffVisuals, ("_session", session), ("_shakeTarget", track.transform),
                ("_overlay", debuffOverlay), ("_statusText", debuffText));

            GameObject hudGo = FindOrCreate("RhythmDebugHUD");
            var hud = GetOrAdd<RhythmDebugHUD>(hudGo);
            SetRefs(hud, ("_track", controller), ("_judgementText", judgement), ("_statsText", stats),
                ("_metricsText", metrics));

            var tools = GetOrAdd<RhythmDebugTools>(hudGo);
            SetRefs(tools, ("_track", controller), ("_autoPlayer", autoPlayer), ("_session", session),
                ("_statusText", status), ("_gcText", gc));

        }

        // ---------------- Helpers ----------------

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

        // Teclas por carril: 0 rojo, 1 amarillo, 2 verde, 3 azul (letra / flecha)
        private static readonly string[] KeyLabels = { "A / ←", "S / ↓", "D / ↑", "F / →" };

        private static TextMeshPro FindOrCreateWorldLabel(Transform parent, string name, string text, Vector3 localPosition)
        {
            Transform t = parent.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var tmp = GetOrAdd<TextMeshPro>(go);
            tmp.text = text;
            tmp.fontSize = 4f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.rectTransform.sizeDelta = new Vector2(1.4f, 0.5f);
            tmp.sortingOrder = 12;
            return tmp;
        }

        private static SpriteRenderer FindOrCreateSprite(Transform parent, string name, Sprite sprite, Color color,
            Vector3 localPosition, Vector3 localScale, int sortingOrder)
        {
            Transform t = parent.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            var sr = GetOrAdd<SpriteRenderer>(go);
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private static Image FindOrCreateImage(Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size, Image.Type type)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
            }

            var image = GetOrAdd<Image>(go);
            image.sprite = sprite;
            image.color = color;
            image.type = type;
            image.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return image;
        }

        private static void ConfigureFill(Image image, float amount)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = amount;
        }

        private static TMP_Text FindOrCreateText(Transform parent, string name, string initialText, float size,
            TextAlignmentOptions align, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta, Color color)
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
            text.color = color;
            text.raycastTarget = false;
            if (initialText != null) text.text = initialText;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
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
