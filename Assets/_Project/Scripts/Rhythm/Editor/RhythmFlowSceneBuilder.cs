using System.Collections.Generic;
using System.IO;
using DiosPunk.Flow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>
    /// Genera la arquitectura multiescena: MainMenu.unity (menú, selector, opciones y créditos) y
    /// Gameplay.unity (pista, HUD, narrativa pre-nivel, preparación, pausa, Game Over y resultados),
    /// más los assets de datos (niveles, catálogo, canción en Resources) y los Build Settings.
    /// Menú: DiosPunk/Scenes/Build MainMenu + Gameplay. Idempotente: regenera el contenido de ambas escenas.
    /// </summary>
    public static partial class RhythmTestSetupBuilder
    {
        private const string LevelDir = "Assets/_Project/Data/Levels";
        private const string SceneDir = "Assets/_Project/Scenes";
        private const string MainMenuScenePath = SceneDir + "/MainMenu.unity";
        private const string GameplayScenePath = SceneDir + "/Gameplay.unity";
        private const string LegacyScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CatalogPath = LevelDir + "/LevelCatalog.asset";
        private const string LegacySongPath = "Assets/_Project/Data/Songs/TestSong_120BPM.asset";

        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Color Red = new Color(0.85f, 0.1f, 0.1f, 1f);
        private static readonly Color Dark = new Color(0.12f, 0.12f, 0.12f, 1f);

        internal sealed class BuiltAssets
        {
            public Sprite Note, HitBar, Panel, White;
            public SongData Song;
            public NoteView NotePrefab;
        }

        [MenuItem("DiosPunk/Scenes/Build MainMenu + Gameplay")]
        public static void BuildAllScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Scenes] Sali de Play Mode antes de generar las escenas.");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            Directory.CreateDirectory(LevelDir);
            Directory.CreateDirectory(SceneDir);
            AssetDatabase.Refresh();

            MigrateSongAsset();
            BuiltAssets assets = BuildAssets();
            LevelData[] levels = CreateLevels();
            LevelCatalog catalog = CreateCatalog(levels);

            BuildGameplayScene(assets, catalog);
            BuildMainMenuScene(assets, catalog);
            SetBuildSettings();

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            Debug.Log("[Scenes] MainMenu.unity y Gameplay.unity listas. Dale Play desde MainMenu.");
        }

        // ---------------- Datos ----------------

        private static void MigrateSongAsset()
        {
            if (File.Exists(LegacySongPath) && !File.Exists(SongPath))
            {
                Directory.CreateDirectory(SongDir);
                AssetDatabase.Refresh();
                string error = AssetDatabase.MoveAsset(LegacySongPath, SongPath);
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning("[Scenes] No se pudo mover el SongData: " + error);
            }
        }

        private static LevelData[] CreateLevels()
        {
            string[] titles =
            {
                "Los comienzos en la musica", "Ensayos con amigos", "Recitales con Suenos Punk Rock",
                "Tocando en la peatonal", "Contra los propios demonios"
            };
            string[] stages = { "El cuarto", "La sala de ensayo", "El escenario", "La peatonal", "La noche" };
            string[] difficulties = { "Facil", "Facil", "Media", "Media", "Dificil" };

            var levels = new LevelData[titles.Length];
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
                so.FindProperty("_title").stringValue = titles[i];
                so.FindProperty("_stage").stringValue = stages[i];
                so.FindProperty("_difficulty").stringValue = difficulties[i];
                so.FindProperty("_songResource").stringValue = Path.GetFileNameWithoutExtension(SongPath);

                SerializedProperty dialogue = so.FindProperty("_dialogue");
                dialogue.arraySize = 3;
                SetLine(dialogue, 0, "Narrador", "Escena de prueba del nivel " + (i + 1) + ". El guion todavia no esta definido.");
                SetLine(dialogue, 1, "Dios Punk", "Linea de dialogo de ejemplo.");
                SetLine(dialogue, 2, "Amigo", "Otra linea de ejemplo antes de la cancion.");
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(level);
                levels[i] = level;
            }
            return levels;
        }

        private static LevelCatalog CreateCatalog(LevelData[] levels)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            SetRefArray(catalog, "_levels", levels);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void SetLine(SerializedProperty array, int index, string speaker, string text)
        {
            SerializedProperty line = array.GetArrayElementAtIndex(index);
            line.FindPropertyRelative("speaker").stringValue = speaker;
            line.FindPropertyRelative("text").stringValue = text;
        }

        // ---------------- Escena Gameplay ----------------

        private static void BuildGameplayScene(BuiltAssets a, LevelCatalog catalog)
        {
            // La escena por defecto se renombra a Gameplay (conserva su GUID y su cámara/luz de la plantilla URP 2D).
            if (!File.Exists(GameplayScenePath) && File.Exists(LegacyScenePath))
            {
                string error = AssetDatabase.MoveAsset(LegacyScenePath, GameplayScenePath);
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning("[Scenes] No se pudo renombrar SampleScene: " + error);
            }

            Scene scene;
            if (File.Exists(GameplayScenePath))
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, GameplayScenePath);
            }

            // Abrir/crear una escena descarga assets sin referencias: se vuelven a cargar para no asignar objetos destruidos.
            a = ReloadAssets(a);
            catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

            // Regenerar: se borra lo generado por versiones anteriores (incluido el objeto de prueba vacío).
            RemoveRoots(scene, "RhythmTrack", "HudCanvas", "DebugCanvas", "RhythmHudPresenter", "RhythmDebugHUD",
                "GameFlow", "GameplayFlow", "EventSystem", "prueba claude");

            BuildTrackAndHud(a.Song, a.NotePrefab, a.HitBar, a.Note, a.Panel, a.White);

            GameObject track = GameObject.Find("RhythmTrack");
            var controller = track.GetComponent<RhythmTrackController>();
            var session = track.GetComponent<RhythmSession>();

            // El flujo decide cuándo arranca la canción (no al darle Play).
            var so = new SerializedObject(controller);
            so.FindProperty("_playOnStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            Transform ui = GameObject.Find("HudCanvas").transform;
            EnsureEventSystem();

            RectTransform preRoot = CreateScreenRoot(ui, "PreLevelScreen", Dark, a.White);
            var preLevel = BuildPreLevel(preRoot, a.Panel);
            RectTransform prepRoot = CreateScreenRoot(ui, "SongPreparationScreen", Dark, a.White);
            var preparation = BuildPreparation(prepRoot, a.Panel);
            RectTransform optionsRoot = CreateScreenRoot(ui, "OptionsScreen", new Color(0.12f, 0.12f, 0.12f, 0.92f), a.White);
            var options = BuildOptions(optionsRoot, a.Panel, a.White);
            RectTransform pauseRoot = CreateScreenRoot(ui, "PauseMenuScreen", new Color(0f, 0f, 0f, 0.55f), a.White);
            var pause = BuildPause(pauseRoot, a.Panel);
            RectTransform overRoot = CreateScreenRoot(ui, "GameOverScreen", new Color(0.35f, 0f, 0f, 0.7f), a.White);
            var gameOver = BuildGameOver(overRoot, a.Panel);
            RectTransform resultsRoot = CreateScreenRoot(ui, "ResultsScreenPanel", Dark, a.White);
            var results = BuildResults(resultsRoot, a.Panel);

            GameObject flowGo = new GameObject("GameplayFlow");
            var flow = flowGo.AddComponent<GameplayFlowController>();
            var screenManager = flowGo.AddComponent<ScreenManager>();
            SetRefs(flow, ("_track", controller), ("_session", session), ("_catalog", catalog), ("_preLevel", preLevel),
                ("_preparation", preparation), ("_options", options), ("_pause", pause), ("_gameOver", gameOver),
                ("_results", results));

            var panels = new List<ScreenPanel>
            {
                MakePanel(preRoot.gameObject, false, GameState.PreLevel),
                MakePanel(prepRoot.gameObject, false, GameState.SongPreparation),
                MakePanel(optionsRoot.gameObject, false, GameState.Options),
                MakePanel(pauseRoot.gameObject, false, GameState.PauseMenu),
                MakePanel(overRoot.gameObject, false, GameState.GameOver),
                MakePanel(resultsRoot.gameObject, false, GameState.ResultsScreen)
            };

            // Mundo de juego y HUD: visibles en Gameplay / Pausa / Game Over (y en Opciones abierta desde la pausa).
            var gameplayStates = new[] { GameState.Gameplay, GameState.PauseMenu, GameState.GameOver };
            panels.Add(MakePanel(track, true, gameplayStates));
            foreach (string hudName in new[] { "PunkPanel", "ScorePanel", "HealthBar", "JudgementText", "StatsText",
                         "MetricsText", "GcText", "StatusText" })
            {
                Transform t = ui.Find(hudName);
                if (t != null) panels.Add(MakePanel(t.gameObject, true, gameplayStates));
            }

            SetRefs(screenManager, ("_flow", flow));
            SetRefArray(screenManager, "_panels", panels.ToArray());

            foreach (RectTransform r in new[] { preRoot, prepRoot, optionsRoot, pauseRoot, overRoot, resultsRoot })
            {
                r.gameObject.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, GameplayScenePath);
        }

        // ---------------- Escena MainMenu ----------------

        private static void BuildMainMenuScene(BuiltAssets a, LevelCatalog catalog)
        {
            Scene scene;
            if (File.Exists(MainMenuScenePath))
            {
                scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            }

            a = ReloadAssets(a);
            catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

            RemoveRoots(scene, "Main Camera", "MenuCanvas", "MenuFlow", "EventSystem");

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Dark;

            var canvasGo = new GameObject("MenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            Transform ui = canvasGo.transform;

            EnsureEventSystem();

            RectTransform mainRoot = CreateScreenRoot(ui, "MainMenuScreen", Dark, a.White);
            var mainMenu = BuildMainMenu(mainRoot, a.Panel);
            RectTransform selectRoot = CreateScreenRoot(ui, "LevelSelectScreen", Dark, a.White);
            var levelSelect = BuildLevelSelect(selectRoot, a.Panel);
            RectTransform optionsRoot = CreateScreenRoot(ui, "OptionsScreen", Dark, a.White);
            var options = BuildOptions(optionsRoot, a.Panel, a.White);
            RectTransform creditsRoot = CreateScreenRoot(ui, "CreditsScreen", Dark, a.White);
            var credits = BuildCredits(creditsRoot, a.Panel);

            var flowGo = new GameObject("MenuFlow");
            var flow = flowGo.AddComponent<MenuFlowController>();
            var screenManager = flowGo.AddComponent<ScreenManager>();
            SetRefs(flow, ("_catalog", catalog), ("_mainMenu", mainMenu), ("_levelSelect", levelSelect),
                ("_options", options), ("_credits", credits));

            var panels = new[]
            {
                MakePanel(mainRoot.gameObject, false, GameState.MainMenu),
                MakePanel(selectRoot.gameObject, false, GameState.LevelSelect),
                MakePanel(optionsRoot.gameObject, false, GameState.Options),
                MakePanel(creditsRoot.gameObject, false, GameState.Credits)
            };
            SetRefs(screenManager, ("_flow", flow));
            SetRefArray(screenManager, "_panels", panels);

            foreach (RectTransform r in new[] { mainRoot, selectRoot, optionsRoot, creditsRoot })
            {
                r.gameObject.SetActive(false);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
        }

        private static BuiltAssets ReloadAssets(BuiltAssets old)
        {
            return new BuiltAssets
            {
                Note = AssetDatabase.LoadAssetAtPath<Sprite>(NotePngPath),
                HitBar = AssetDatabase.LoadAssetAtPath<Sprite>(HitBarPngPath),
                Panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPngPath),
                White = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePngPath),
                Song = AssetDatabase.LoadAssetAtPath<SongData>(SongPath),
                NotePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NotePrefabPath).GetComponent<NoteView>()
            };
        }

        private static void SetBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(GameplayScenePath, true)
            };
        }

        private static void RemoveRoots(Scene scene, params string[] names)
        {
            var set = new HashSet<string>(names);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (set.Contains(root.name)) Object.DestroyImmediate(root);
            }
        }

        // ---------------- Pantallas ----------------

        private static MainMenuScreen BuildMainMenu(RectTransform root, Sprite panel)
        {
            FindOrCreateText(root, "Title", "DIOS PUNK", 160f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 270f), new Vector2(1400f, 220f), Red);
            Button play = CreateButton(root, "PlayButton", "JUGAR", new Vector2(0f, 70f), new Vector2(460f, 100f), panel);
            Button options = CreateButton(root, "OptionsButton", "OPCIONES", new Vector2(0f, -50f), new Vector2(460f, 100f), panel);
            Button credits = CreateButton(root, "CreditsButton", "CREDITOS", new Vector2(0f, -170f), new Vector2(460f, 100f), panel);
            Button quit = CreateButton(root, "QuitButton", "SALIR", new Vector2(0f, -290f), new Vector2(460f, 100f), panel);

            var screen = GetOrAdd<MainMenuScreen>(root.gameObject);
            SetRefs(screen, ("_play", play), ("_options", options), ("_credits", credits), ("_quit", quit));
            return screen;
        }

        private static CreditsScreen BuildCredits(RectTransform root, Sprite panel)
        {
            Image paper = FindOrCreateImage(root, "Paper", panel, Color.white, Center, Center,
                Vector2.zero, new Vector2(1200f, 760f), Image.Type.Sliced);
            RectTransform p = paper.rectTransform;

            FindOrCreateText(p, "Title", "CREDITOS", 90f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 300f), new Vector2(1000f, 120f), Red);
            FindOrCreateText(p, "Body",
                "Dios Punk - UNR GameLab 2026\n\nGame Design: Elias\nArte: Lourdes\nAudio: Fede, Cristian\nProgramacion: Lolo\n\nHomenaje a Javier Messina, el Dios Punk de Rosario.",
                42f, TextAlignmentOptions.Center, Center, Center, new Vector2(0f, 20f), new Vector2(1000f, 500f), Color.black);
            Button back = CreateButton(p, "BackButton", "VOLVER", new Vector2(0f, -320f), new Vector2(380f, 80f), panel);

            var screen = GetOrAdd<CreditsScreen>(root.gameObject);
            SetRefs(screen, ("_back", back));
            return screen;
        }

        private static LevelSelectScreen BuildLevelSelect(RectTransform root, Sprite panel)
        {
            FindOrCreateText(root, "Title", "SELECTOR DE NIVELES", 90f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 330f), new Vector2(1400f, 130f), Color.white);

            var buttons = new Button[5];
            var labels = new TMP_Text[5];
            for (int i = 0; i < 5; i++)
            {
                buttons[i] = CreateButton(root, "Level" + (i + 1) + "Button", "NIVEL " + (i + 1),
                    new Vector2((i - 2) * 350f, 20f), new Vector2(330f, 220f), panel);
                labels[i] = buttons[i].transform.Find("Label").GetComponent<TMP_Text>();
                labels[i].fontSize = 34f;
            }
            Button back = CreateButton(root, "BackButton", "VOLVER", new Vector2(0f, -280f), new Vector2(420f, 90f), panel);

            var screen = GetOrAdd<LevelSelectScreen>(root.gameObject);
            SetRefArray(screen, "_levelButtons", buttons);
            SetRefArray(screen, "_levelLabels", labels);
            SetRefs(screen, ("_back", back));
            return screen;
        }

        private static OptionsScreen BuildOptions(RectTransform root, Sprite panel, Sprite white)
        {
            FindOrCreateText(root, "Title", "OPCIONES", 90f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 400f), new Vector2(1000f, 120f), Color.white);

            Image paper = FindOrCreateImage(root, "Paper", panel, Color.white, Center, Center,
                new Vector2(0f, -20f), new Vector2(1200f, 760f), Image.Type.Sliced);
            RectTransform paperRt = paper.rectTransform;

            string[] tabNames = { "AUDIO", "VIDEO", "CONTROLES", "ACCESIBILIDAD" };
            var tabs = new Button[4];
            for (int i = 0; i < 4; i++)
            {
                tabs[i] = CreateButton(paperRt, "Tab" + (i + 1), tabNames[i],
                    new Vector2((i - 1.5f) * 285f, 300f), new Vector2(270f, 70f), panel);
                tabs[i].transform.Find("Label").GetComponent<TMP_Text>().fontSize = 30f;
            }

            var pages = new GameObject[4];
            string[] pageNames = { "Page_Audio", "Page_Video", "Page_Controles", "Page_Accesibilidad" };
            for (int i = 0; i < 4; i++) pages[i] = FindOrCreateRect(paperRt, pageNames[i], new Vector2(0f, -30f), new Vector2(1100f, 480f));

            FindOrCreateText(pages[0].transform, "VolumeLabel", "Volumen general", 44f, TextAlignmentOptions.Left,
                Center, Center, new Vector2(-300f, 60f), new Vector2(500f, 70f), Color.black);
            Slider volume = FindOrCreateSlider(pages[0].transform, "VolumeSlider", new Vector2(0f, -40f), new Vector2(700f, 36f));

            Toggle fullscreen = FindOrCreateToggle(pages[1].transform, "FullscreenToggle", "Pantalla completa",
                new Vector2(0f, 40f), panel, white);

            FindOrCreateText(pages[2].transform, "ControlsText",
                "Carriles: D  F  J  K  o  Flechas\nPausa: Escape\nDebug: F1 Auto-Play, F2 reiniciar, F3 pausa, F4 modo, +/- latencia",
                38f, TextAlignmentOptions.Center, Center, Center, Vector2.zero, new Vector2(1000f, 300f), Color.black);

            Toggle reduce = FindOrCreateToggle(pages[3].transform, "ReduceEffectsToggle",
                "Reducir intensidad de efectos visuales", new Vector2(0f, 40f), panel, white);

            Button back = CreateButton(paperRt, "BackButton", "VOLVER", new Vector2(0f, -320f), new Vector2(380f, 80f), panel);

            var screen = GetOrAdd<OptionsScreen>(root.gameObject);
            SetRefArray(screen, "_tabButtons", tabs);
            SetRefArray(screen, "_tabPages", pages);
            SetRefs(screen, ("_volume", volume), ("_fullscreen", fullscreen), ("_reduceEffects", reduce), ("_back", back));
            return screen;
        }

        private static PreLevelScreen BuildPreLevel(RectTransform root, Sprite panel)
        {
            Image paper = FindOrCreateImage(root, "Paper", panel, Color.white, Center, Center,
                Vector2.zero, new Vector2(1400f, 760f), Image.Type.Sliced);
            RectTransform p = paper.rectTransform;

            TMP_Text title = FindOrCreateText(p, "TitleText", "Titulo", 64f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 290f), new Vector2(1200f, 100f), Red);
            TMP_Text speaker = FindOrCreateText(p, "SpeakerText", "", 48f, TextAlignmentOptions.Left, Center, Center,
                new Vector2(0f, 170f), new Vector2(1200f, 70f), Color.black);
            TMP_Text body = FindOrCreateText(p, "BodyText", "", 44f, TextAlignmentOptions.TopLeft, Center, Center,
                new Vector2(0f, -10f), new Vector2(1200f, 300f), Color.black);
            Button skip = CreateButton(p, "SkipButton", "SALTAR", new Vector2(-300f, -300f), new Vector2(360f, 90f), panel);
            Button advance = CreateButton(p, "AdvanceButton", "AVANZAR", new Vector2(300f, -300f), new Vector2(360f, 90f), panel);

            var screen = GetOrAdd<PreLevelScreen>(root.gameObject);
            SetRefs(screen, ("_titleText", title), ("_speakerText", speaker), ("_bodyText", body),
                ("_advance", advance), ("_skip", skip));
            return screen;
        }

        private static SongPreparationScreen BuildPreparation(RectTransform root, Sprite panel)
        {
            Image paper = FindOrCreateImage(root, "Paper", panel, Color.white, Center, Center,
                Vector2.zero, new Vector2(1200f, 720f), Image.Type.Sliced);
            RectTransform p = paper.rectTransform;

            TMP_Text title = FindOrCreateText(p, "TitleText", "Titulo", 64f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 240f), new Vector2(1100f, 110f), Red);
            TMP_Text stage = FindOrCreateText(p, "StageText", "", 50f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 90f), new Vector2(1100f, 80f), Color.black);
            TMP_Text difficulty = FindOrCreateText(p, "DifficultyText", "", 50f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, -10f), new Vector2(1100f, 80f), Color.black);
            Button back = CreateButton(p, "BackButton", "VOLVER", new Vector2(-280f, -250f), new Vector2(360f, 90f), panel);
            Button start = CreateButton(p, "StartButton", "COMENZAR", new Vector2(280f, -250f), new Vector2(360f, 90f), panel);

            var screen = GetOrAdd<SongPreparationScreen>(root.gameObject);
            SetRefs(screen, ("_titleText", title), ("_stageText", stage), ("_difficultyText", difficulty),
                ("_start", start), ("_back", back));
            return screen;
        }

        private static PauseMenuScreen BuildPause(RectTransform root, Sprite panel)
        {
            Image paper = FindOrCreateImage(root, "Paper", panel, new Color(0.97f, 0.97f, 0.93f, 1f), Center, Center,
                Vector2.zero, new Vector2(620f, 760f), Image.Type.Sliced);
            RectTransform p = paper.rectTransform;

            FindOrCreateText(p, "Title", "PAUSA", 110f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 280f), new Vector2(560f, 140f), Color.black);
            Button resume = CreateButton(p, "ResumeButton", "Reanudar", new Vector2(0f, 120f), new Vector2(460f, 90f), panel);
            Button restart = CreateButton(p, "RestartButton", "Reiniciar", new Vector2(0f, 0f), new Vector2(460f, 90f), panel);
            Button options = CreateButton(p, "OptionsButton", "Opciones", new Vector2(0f, -120f), new Vector2(460f, 90f), panel);
            Button exit = CreateButton(p, "ExitButton", "Salir", new Vector2(0f, -240f), new Vector2(460f, 90f), panel);

            var screen = GetOrAdd<PauseMenuScreen>(root.gameObject);
            SetRefs(screen, ("_resume", resume), ("_restart", restart), ("_options", options), ("_exit", exit));
            return screen;
        }

        private static GameOverScreen BuildGameOver(RectTransform root, Sprite panel)
        {
            FindOrCreateText(root, "Title", "GAME OVER", 150f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 240f), new Vector2(1400f, 200f), Color.white);
            Button retry = CreateButton(root, "RetryButton", "REINTENTAR", new Vector2(0f, 40f), new Vector2(460f, 100f), panel);
            Button select = CreateButton(root, "LevelSelectButton", "SELECTOR", new Vector2(0f, -90f), new Vector2(460f, 100f), panel);
            Button menu = CreateButton(root, "MainMenuButton", "MENU", new Vector2(0f, -220f), new Vector2(460f, 100f), panel);

            var screen = GetOrAdd<GameOverScreen>(root.gameObject);
            SetRefs(screen, ("_retry", retry), ("_levelSelect", select), ("_mainMenu", menu));
            return screen;
        }

        private static ResultsScreen BuildResults(RectTransform root, Sprite panel)
        {
            Image paper = FindOrCreateImage(root, "Paper", panel, Color.white, Center, Center,
                Vector2.zero, new Vector2(1300f, 860f), Image.Type.Sliced);
            RectTransform p = paper.rectTransform;

            FindOrCreateText(p, "Title", "RESULTADOS", 90f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 360f), new Vector2(1100f, 120f), Red);
            TMP_Text score = FindOrCreateText(p, "ScoreText", "", 56f, TextAlignmentOptions.Left, Center, Center,
                new Vector2(0f, 240f), new Vector2(1100f, 80f), Color.black);
            TMP_Text accuracy = FindOrCreateText(p, "AccuracyText", "", 46f, TextAlignmentOptions.Left, Center, Center,
                new Vector2(0f, 160f), new Vector2(1100f, 70f), Color.black);
            TMP_Text combo = FindOrCreateText(p, "ComboText", "", 46f, TextAlignmentOptions.Left, Center, Center,
                new Vector2(0f, 90f), new Vector2(1100f, 70f), Color.black);
            TMP_Text punk = FindOrCreateText(p, "PunkText", "", 46f, TextAlignmentOptions.Left, Center, Center,
                new Vector2(0f, 20f), new Vector2(1100f, 70f), Color.black);
            TMP_Text counts = FindOrCreateText(p, "CountsText", "", 40f, TextAlignmentOptions.Left, Center, Center,
                new Vector2(0f, -50f), new Vector2(1100f, 60f), Color.black);
            TMP_Text evaluation = FindOrCreateText(p, "EvaluationText", "", 46f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, -140f), new Vector2(1100f, 130f), Red);

            Button retry = CreateButton(p, "RetryButton", "REINTENTAR", new Vector2(-480f, -330f), new Vector2(290f, 80f), panel);
            Button next = CreateButton(p, "NextButton", "CONTINUAR", new Vector2(-160f, -330f), new Vector2(290f, 80f), panel);
            Button select = CreateButton(p, "LevelSelectButton", "SELECTOR", new Vector2(160f, -330f), new Vector2(290f, 80f), panel);
            Button menu = CreateButton(p, "MainMenuButton", "MENU", new Vector2(480f, -330f), new Vector2(290f, 80f), panel);

            var screen = GetOrAdd<ResultsScreen>(root.gameObject);
            SetRefs(screen, ("_scoreText", score), ("_accuracyText", accuracy), ("_comboText", combo), ("_punkText", punk),
                ("_countsText", counts), ("_evaluationText", evaluation), ("_retry", retry), ("_next", next),
                ("_levelSelect", select), ("_mainMenu", menu));
            return screen;
        }

        // ---------------- Helpers de UI ----------------

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>(); // New Input System, nunca StandaloneInputModule
            module.AssignDefaultActions();
        }

        private static RectTransform CreateScreenRoot(Transform parent, string name, Color background, Sprite white)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
            }

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = GetOrAdd<Image>(go);
            image.sprite = white;
            image.color = background;
            image.raycastTarget = false; // el fondo no es interactivo: no debe bloquear ni capturar clics
            return rt;
        }

        private static GameObject FindOrCreateRect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
            }

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Center;
            rt.anchorMax = Center;
            rt.pivot = Center;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Sprite panel)
        {
            Image image = FindOrCreateImage(parent, name, panel, Color.white, Center, Center, pos, size, Image.Type.Sliced);
            image.raycastTarget = true; // los botones sí reciben clics

            var button = GetOrAdd<Button>(image.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            button.colors = colors;

            FindOrCreateText(image.transform, "Label", label, 44f, TextAlignmentOptions.Center, Center, Center,
                Vector2.zero, size, Color.black);
            return button;
        }

        private static Slider FindOrCreateSlider(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null) go = existing.gameObject;
            else
            {
                var resources = new DefaultControls.Resources
                {
                    standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                    background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                    knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd")
                };
                go = DefaultControls.CreateSlider(resources);
                go.name = name;
                go.transform.SetParent(parent, false);
            }

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Center;
            rt.anchorMax = Center;
            rt.pivot = Center;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private static Toggle FindOrCreateToggle(Transform parent, string name, string label, Vector2 pos, Sprite panel, Sprite white)
        {
            Transform t = parent.Find(name);
            GameObject go;
            if (t != null) go = t.gameObject;
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                go.transform.SetParent(parent, false);
            }

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Center;
            rt.anchorMax = Center;
            rt.pivot = Center;
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(900f, 70f);

            Image box = FindOrCreateImage(go.transform, "Box", panel, Color.white,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(60f, 60f), Image.Type.Sliced);
            box.raycastTarget = true;
            Image check = FindOrCreateImage(box.transform, "Check", white, Color.black,
                Center, Center, Vector2.zero, new Vector2(34f, 34f), Image.Type.Simple);
            FindOrCreateText(go.transform, "Label", label, 40f, TextAlignmentOptions.Left,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(800f, 70f), Color.black);

            var toggle = GetOrAdd<Toggle>(go);
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        private static ScreenPanel MakePanel(GameObject go, bool keepWhenOptionsFromPause, params GameState[] states)
        {
            var panel = GetOrAdd<ScreenPanel>(go);
            var so = new SerializedObject(panel);

            SerializedProperty visible = so.FindProperty("_visibleIn");
            visible.arraySize = states.Length;
            for (int i = 0; i < states.Length; i++) visible.GetArrayElementAtIndex(i).intValue = (int)states[i];

            so.FindProperty("_keepWhenOptionsFromPause").boolValue = keepWhenOptionsFromPause;
            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }
    }
}
