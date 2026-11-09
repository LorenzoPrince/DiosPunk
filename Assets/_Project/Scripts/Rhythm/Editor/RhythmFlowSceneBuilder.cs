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
            FxAssets fx = BuildFxAssets();
            CreateLevelSongs();                                  // 5 canciones (audio placeholder + chart) en Resources/Songs
            DialogueData[] dialogues = CreateStoryDialogues(fx.Portrait);
            CreateEpilogue(fx.Portrait);
            LevelData[] levels = CreateLevels(dialogues);
            LevelCatalog catalog = CreateCatalog(levels);
            ValidateLevels(catalog);

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
            // La canción se carga por LoadSong desde Resources: la escena no debe referenciar el audio.
            so.FindProperty("_song").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            Transform ui = GameObject.Find("HudCanvas").transform;
            EnsureEventSystem();
            BuildSabotage(track, session, ui, ReloadFx(), a.Panel, a.White);

            RectTransform preRoot = CreateScreenRoot(ui, "PreLevelScreen", Dark, a.White);
            var preLevel = BuildPreLevel(preRoot, a.Panel);
            RectTransform prepRoot = CreateScreenRoot(ui, "SongPreparationScreen", Dark, a.White);
            var preparation = BuildPreparation(prepRoot, a.Panel);
            RectTransform optionsRoot = CreateScreenRoot(ui, "OptionsScreen", new Color(0.12f, 0.12f, 0.12f, 0.92f), a.White);
            var options = BuildOptions(optionsRoot, a.Panel, a.White, false);
            RectTransform pauseRoot = CreateScreenRoot(ui, "PauseMenuScreen", new Color(0f, 0f, 0f, 0.55f), a.White);
            var pause = BuildPause(pauseRoot, a.Panel);
            RectTransform overRoot = CreateScreenRoot(ui, "GameOverScreen", new Color(0.35f, 0f, 0f, 0.7f), a.White);
            var gameOver = BuildGameOver(overRoot, a.Panel);
            RectTransform resultsRoot = CreateScreenRoot(ui, "ResultsScreenPanel", Dark, a.White);
            var results = BuildResults(resultsRoot, a.Panel);

            // Cierre del juego: epílogo narrativo (mismo presentador de diálogos) y créditos.
            RectTransform epilogueRoot = CreateScreenRoot(ui, "EpilogueScreen", Dark, a.White);
            var epilogue = BuildPreLevel(epilogueRoot, a.Panel);
            RectTransform creditsRoot = CreateScreenRoot(ui, "CreditsScreen", Dark, a.White);
            var credits = BuildCredits(creditsRoot, a.Panel);
            var epilogueData = AssetDatabase.LoadAssetAtPath<DialogueData>(EpiloguePath);

            GameObject flowGo = new GameObject("GameplayFlow");
            var flow = flowGo.AddComponent<GameplayFlowController>();
            var screenManager = flowGo.AddComponent<ScreenManager>();
            SetRefs(flow, ("_track", controller), ("_session", session), ("_catalog", catalog), ("_preLevel", preLevel),
                ("_preparation", preparation), ("_options", options), ("_pause", pause), ("_gameOver", gameOver),
                ("_results", results), ("_epilogue", epilogue), ("_epilogueData", epilogueData), ("_credits", credits));

            var panels = new List<ScreenPanel>
            {
                MakePanel(preRoot.gameObject, false, GameState.PreLevel),
                MakePanel(prepRoot.gameObject, false, GameState.SongPreparation),
                MakePanel(optionsRoot.gameObject, false, GameState.Options),
                MakePanel(pauseRoot.gameObject, false, GameState.PauseMenu),
                MakePanel(overRoot.gameObject, false, GameState.GameOver),
                MakePanel(resultsRoot.gameObject, false, GameState.ResultsScreen),
                MakePanel(epilogueRoot.gameObject, false, GameState.Epilogue),
                MakePanel(creditsRoot.gameObject, false, GameState.Credits)
            };

            // Mundo de juego y HUD: visibles en Gameplay / Pausa / Game Over (y en Opciones abierta desde la pausa).
            var gameplayStates = new[] { GameState.Gameplay, GameState.PauseMenu, GameState.GameOver };
            panels.Add(MakePanel(track, true, gameplayStates));
            foreach (string hudName in new[] { "PunkPanel", "ScorePanel", "HealthBar", "SongProgressPanel", "JudgementText", "StatsText",
                         "MetricsText", "GcText", "StatusText" })
            {
                Transform t = ui.Find(hudName);
                if (t != null) panels.Add(MakePanel(t.gameObject, true, gameplayStates));
            }

            SetRefs(screenManager, ("_flow", flow));
            SetRefArray(screenManager, "_panels", panels.ToArray());

            foreach (RectTransform r in new[] { preRoot, prepRoot, optionsRoot, pauseRoot, overRoot, resultsRoot, epilogueRoot, creditsRoot })
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
            var mainMenu = BuildMainMenu(mainRoot, a.Panel, a.White);
            RectTransform selectRoot = CreateScreenRoot(ui, "LevelSelectScreen", Dark, a.White);
            var levelSelect = BuildLevelSelect(selectRoot, a.Panel);
            RectTransform optionsRoot = CreateScreenRoot(ui, "OptionsScreen", Dark, a.White);
            var options = BuildOptions(optionsRoot, a.Panel, a.White, true);
            RectTransform creditsRoot = CreateScreenRoot(ui, "CreditsScreen", Dark, a.White);
            var credits = BuildCredits(creditsRoot, a.Panel);
            RectTransform calibrationRoot = CreateScreenRoot(ui, "CalibrationScreen", Dark, a.White);
            var calibration = BuildCalibration(calibrationRoot, a.Panel, a.White, ReloadFx());

            var flowGo = new GameObject("MenuFlow");
            var flow = flowGo.AddComponent<MenuFlowController>();
            var screenManager = flowGo.AddComponent<ScreenManager>();
            SetRefs(flow, ("_catalog", catalog), ("_mainMenu", mainMenu), ("_levelSelect", levelSelect),
                ("_options", options), ("_credits", credits), ("_calibration", calibration));

            var panels = new[]
            {
                MakePanel(mainRoot.gameObject, false, GameState.MainMenu),
                MakePanel(selectRoot.gameObject, false, GameState.LevelSelect),
                MakePanel(optionsRoot.gameObject, false, GameState.Options),
                MakePanel(creditsRoot.gameObject, false, GameState.Credits),
                MakePanel(calibrationRoot.gameObject, false, GameState.Calibration)
            };
            SetRefs(screenManager, ("_flow", flow));
            SetRefArray(screenManager, "_panels", panels);

            foreach (RectTransform r in new[] { mainRoot, selectRoot, optionsRoot, creditsRoot, calibrationRoot })
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

        private static MainMenuScreen BuildMainMenu(RectTransform root, Sprite panel, Sprite white)
        {
            FindOrCreateText(root, "Title", "DIOS PUNK", 160f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 270f), new Vector2(1400f, 220f), Red);
            Button play = CreateButton(root, "PlayButton", "JUGAR", new Vector2(0f, 90f), new Vector2(460f, 90f), panel);
            TMP_Text playLabel = play.transform.Find("Label").GetComponent<TMP_Text>();
            Button newGame = CreateButton(root, "NewGameButton", "PARTIDA NUEVA", new Vector2(0f, -20f), new Vector2(460f, 90f), panel);
            Button options = CreateButton(root, "OptionsButton", "OPCIONES", new Vector2(0f, -130f), new Vector2(460f, 90f), panel);
            Button credits = CreateButton(root, "CreditsButton", "CREDITOS", new Vector2(0f, -240f), new Vector2(460f, 90f), panel);
            Button quit = CreateButton(root, "QuitButton", "SALIR", new Vector2(0f, -350f), new Vector2(460f, 90f), panel);

            // Confirmación antes de reemplazar un progreso existente (se muestra por script).
            RectTransform confirm = CreateScreenRoot(root, "ConfirmPanel", new Color(0f, 0f, 0f, 0.8f), white);
            confirm.GetComponent<Image>().raycastTarget = true; // bloquea los botones de atrás mientras está abierto
            Image confirmPaper = FindOrCreateImage(confirm, "Paper", panel, Color.white, Center, Center,
                Vector2.zero, new Vector2(1000f, 480f), Image.Type.Sliced);
            FindOrCreateText(confirmPaper.rectTransform, "Message",
                "Ya hay una partida guardada.\nEmpezar una nueva reemplaza tu progreso.\n\nContinuar?",
                44f, TextAlignmentOptions.Center, Center, Center, new Vector2(0f, 90f), new Vector2(900f, 260f), Color.black);
            Button yes = CreateButton(confirmPaper.rectTransform, "YesButton", "SI, EMPEZAR", new Vector2(-250f, -170f), new Vector2(400f, 90f), panel);
            Button no = CreateButton(confirmPaper.rectTransform, "NoButton", "CANCELAR", new Vector2(250f, -170f), new Vector2(400f, 90f), panel);
            confirm.gameObject.SetActive(false);

            var screen = GetOrAdd<MainMenuScreen>(root.gameObject);
            SetRefs(screen, ("_play", play), ("_playLabel", playLabel), ("_newGame", newGame), ("_options", options),
                ("_credits", credits), ("_quit", quit), ("_confirmPanel", confirm.gameObject),
                ("_confirmYes", yes), ("_confirmNo", no));
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

        private static OptionsScreen BuildOptions(RectTransform root, Sprite panel, Sprite white, bool includeRecalibrate)
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

            // Recalibrar latencia: solo en la escena MainMenu (desde el gameplay habría que salir de la canción).
            Button recalibrate = null;
            TMP_Text latencyLabel = null;
            if (includeRecalibrate)
            {
                latencyLabel = FindOrCreateText(pages[0].transform, "LatencyLabel", "Latencia actual: 0 ms", 40f,
                    TextAlignmentOptions.Left, Center, Center, new Vector2(-250f, -130f), new Vector2(560f, 70f), Color.black);
                recalibrate = CreateButton(pages[0].transform, "RecalibrateButton", "RECALIBRAR LATENCIA",
                    new Vector2(0f, -220f), new Vector2(680f, 90f), panel);
            }

            Toggle fullscreen = FindOrCreateToggle(pages[1].transform, "FullscreenToggle", "Pantalla completa",
                new Vector2(0f, 40f), panel, white);

            FindOrCreateText(pages[2].transform, "ControlsText",
                "Carriles: A  S  D  F  o  Flechas (Izq, Abajo, Arriba, Der)\nPausa: Escape\nDebug: F1 Auto-Play, F2 reiniciar, F3 pausa, F4 modo, F5 Modo Punk, F6 debuff, +/- latencia",
                38f, TextAlignmentOptions.Center, Center, Center, Vector2.zero, new Vector2(1000f, 300f), Color.black);

            Toggle reduce = FindOrCreateToggle(pages[3].transform, "ReduceEffectsToggle",
                "Reducir intensidad de efectos visuales", new Vector2(0f, 40f), panel, white);

            Button back = CreateButton(paperRt, "BackButton", "VOLVER", new Vector2(0f, -320f), new Vector2(380f, 80f), panel);

            var screen = GetOrAdd<OptionsScreen>(root.gameObject);
            SetRefArray(screen, "_tabButtons", tabs);
            SetRefArray(screen, "_tabPages", pages);
            SetRefs(screen, ("_volume", volume), ("_fullscreen", fullscreen), ("_reduceEffects", reduce), ("_back", back),
                ("_recalibrate", recalibrate), ("_latencyLabel", latencyLabel));
            return screen;
        }

        private static PreLevelDialoguePresenter BuildPreLevel(RectTransform root, Sprite panel)
        {
            Image paper = FindOrCreateImage(root, "Paper", panel, Color.white, Center, Center,
                Vector2.zero, new Vector2(1500f, 800f), Image.Type.Sliced);
            RectTransform p = paper.rectTransform;

            TMP_Text title = FindOrCreateText(p, "TitleText", "Titulo", 56f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 330f), new Vector2(1300f, 90f), Red);

            // Retrato (opcional, estilo fotocopia) a la izquierda; texto a la derecha.
            Image portrait = FindOrCreateImage(p, "Portrait", null, Color.white, Center, Center,
                new Vector2(-540f, 90f), new Vector2(300f, 300f), Image.Type.Simple);
            portrait.preserveAspect = true;
            portrait.enabled = false;

            TMP_Text speaker = FindOrCreateText(p, "SpeakerText", "", 46f, TextAlignmentOptions.Left, Center, Center,
                new Vector2(160f, 210f), new Vector2(1000f, 70f), Red);
            TMP_Text body = FindOrCreateText(p, "BodyText", "", 42f, TextAlignmentOptions.TopLeft, Center, Center,
                new Vector2(160f, 40f), new Vector2(1000f, 300f), Color.black);

            // Respuestas cortas (nodo de decisión), hasta 3, ocultas hasta el final de la escena.
            GameObject choicesRoot = FindOrCreateRect(p, "Choices", new Vector2(160f, -110f), new Vector2(1000f, 280f));
            var choiceButtons = new Button[3];
            var choiceLabels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                choiceButtons[i] = CreateButton(choicesRoot.transform, "Choice" + (i + 1), "Respuesta",
                    new Vector2(0f, 90f - i * 90f), new Vector2(940f, 76f), panel);
                choiceLabels[i] = choiceButtons[i].transform.Find("Label").GetComponent<TMP_Text>();
                choiceLabels[i].fontSize = 36f;
            }
            choicesRoot.SetActive(false);

            Button skip = CreateButton(p, "SkipButton", "SALTAR NARRATIVA", new Vector2(-330f, -340f), new Vector2(440f, 90f), panel);
            skip.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 34f;
            Button advance = CreateButton(p, "AdvanceButton", "SIGUIENTE", new Vector2(420f, -340f), new Vector2(400f, 90f), panel);
            TMP_Text advanceLabel = advance.transform.Find("Label").GetComponent<TMP_Text>();

            var screen = GetOrAdd<PreLevelDialoguePresenter>(root.gameObject);
            SetRefs(screen, ("_titleText", title), ("_speakerText", speaker), ("_bodyText", body), ("_portrait", portrait),
                ("_advance", advance), ("_advanceLabel", advanceLabel), ("_skip", skip), ("_choicesRoot", choicesRoot));
            SetRefArray(screen, "_choiceButtons", choiceButtons);
            SetRefArray(screen, "_choiceLabels", choiceLabels);
            return screen;
        }

        private const string NpcPlaceholderText = "PLACEHOLDER\n/\nREEMPLAZAR\nPOR\nDIBUJO\nREAL";

        private static void FindOrCreateNpcLabel(Transform npc)
        {
            Transform t = npc.Find("PlaceholderLabel");
            GameObject go = t != null ? t.gameObject : new GameObject("PlaceholderLabel");
            go.transform.SetParent(npc, false);
            go.transform.localPosition = new Vector3(0f, 0.08f, 0f); // centro del torso en el espacio del sprite
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var label = GetOrAdd<TextMeshPro>(go);
            label.text = NpcPlaceholderText;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMin = 0.3f;
            label.fontSizeMax = 1f;
            label.rectTransform.sizeDelta = new Vector2(0.66f, 0.95f); // ancho del torso (0.7 u de sprite)
            label.sortingOrder = 6;
        }

        /// <summary>NPC saboteadores, proyectil, escudo Punk y marco inferior dentado (parte visual de los debuffs).</summary>
        private static void BuildSabotage(GameObject track, RhythmSession session, Transform ui, FxAssets fx,
            Sprite panel, Sprite white)
        {
            SpriteRenderer left = FindOrCreateSprite(track.transform, "NpcLeft", fx.Npc1, Color.white,
                new Vector3(-6.4f, -0.4f, 0f), new Vector3(1.9f, 1.9f, 1f), 4);
            // El NPC derecho se espeja con flipX (no con escala negativa) para que su rótulo no salga invertido.
            SpriteRenderer right = FindOrCreateSprite(track.transform, "NpcRight", fx.Npc2, Color.white,
                new Vector3(6.4f, -0.4f, 0f), new Vector3(1.9f, 1.9f, 1f), 4);
            right.flipX = true;

            // Rótulo de aclaración sobre el torso. Es hijo del NPC: acompaña el desplazamiento del lanzamiento y,
            // como el movimiento es solo traslación (sin rotación), mantiene siempre su orientación legible.
            FindOrCreateNpcLabel(left.transform);
            FindOrCreateNpcLabel(right.transform);

            SpriteRenderer projectile = FindOrCreateSprite(track.transform, "SabotageProjectile", fx.Projectile, Color.white,
                Vector3.zero, new Vector3(1.3f, 1.3f, 1f), 15);
            projectile.enabled = false;

            SpriteRenderer shield = FindOrCreateSprite(track.transform, "PunkShield", fx.Shield, new Color(1f, 0.85f, 0.1f, 1f),
                new Vector3(0f, -4.0f, 0f), new Vector3(3.6f, 1.6f, 1f), 20);
            shield.enabled = false;

            Image frame = FindOrCreateImage(ui, "DebuffFrame", fx.Frame, new Color(1f, 0.1f, 0.65f, 0.9f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 150f), Image.Type.Simple);
            frame.rectTransform.anchorMin = new Vector2(0f, 0f);
            frame.rectTransform.anchorMax = new Vector2(1f, 0f);
            frame.rectTransform.sizeDelta = new Vector2(0f, 150f);
            frame.raycastTarget = false;
            frame.enabled = false;

            // Mancha de tomate en el centro de la pantalla (detrás del HUD, delante del mundo de juego).
            Image splat = FindOrCreateImage(ui, "DebuffSplat", fx.Splat, Color.white, Center, Center,
                Vector2.zero, new Vector2(980f, 980f), Image.Type.Simple);
            splat.raycastTarget = false;
            splat.enabled = false;
            splat.transform.SetSiblingIndex(1);

            // Destello de pantalla completa cuando el escudo absorbe el debuff.
            Image flash = FindOrCreateImage(ui, "ShieldFlash", white, new Color(1f, 0.85f, 0.1f, 0.55f),
                Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Image.Type.Simple);
            flash.rectTransform.anchorMin = Vector2.zero;
            flash.rectTransform.anchorMax = Vector2.one;
            flash.rectTransform.offsetMin = Vector2.zero;
            flash.rectTransform.offsetMax = Vector2.zero;
            flash.raycastTarget = false;
            flash.enabled = false;
            flash.transform.SetSiblingIndex(2);

            // Cartel estilo fanzine: papel amarillo torcido con "¡DEBUFF ABSORBIDO!".
            Image bannerPaper = FindOrCreateImage(ui, "AbsorbedBanner", panel, new Color(1f, 0.92f, 0.25f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 160f), new Vector2(1180f, 250f), Image.Type.Sliced);
            bannerPaper.raycastTarget = false;
            bannerPaper.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            TMP_Text bannerTitle = FindOrCreateText(bannerPaper.rectTransform, "Title", "¡DEBUFF ABSORBIDO!", 80f,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 25f),
                new Vector2(1100f, 130f), Color.black);
            bannerTitle.fontStyle = FontStyles.Bold;
            bannerTitle.textWrappingMode = TextWrappingModes.NoWrap;
            FindOrCreateText(bannerPaper.rectTransform, "Subtitle", "El Escudo Punk te salvó", 42f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -72f), new Vector2(1000f, 60f), Red);
            bannerPaper.gameObject.SetActive(false);

            var sabotage = GetOrAdd<NpcSabotageController>(track);
            SetRefs(sabotage, ("_session", session), ("_leftNpc", left.transform), ("_rightNpc", right.transform),
                ("_projectile", projectile), ("_shield", shield), ("_frame", frame), ("_splat", splat),
                ("_absorbedBanner", bannerPaper.rectTransform), ("_shieldFlash", flash));
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
            Button retry = CreateButton(root, "RetryButton", "REINTENTAR", new Vector2(0f, 90f), new Vector2(460f, 100f), panel);
            Button options = CreateButton(root, "OptionsButton", "OPCIONES", new Vector2(0f, -30f), new Vector2(460f, 100f), panel);
            Button select = CreateButton(root, "LevelSelectButton", "SELECTOR", new Vector2(0f, -150f), new Vector2(460f, 100f), panel);
            Button menu = CreateButton(root, "MainMenuButton", "MENU", new Vector2(0f, -270f), new Vector2(460f, 100f), panel);

            var screen = GetOrAdd<GameOverScreen>(root.gameObject);
            SetRefs(screen, ("_retry", retry), ("_options", options), ("_levelSelect", select), ("_mainMenu", menu));
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
            TMP_Text nextLabel = next.transform.Find("Label").GetComponent<TMP_Text>();
            Button select = CreateButton(p, "LevelSelectButton", "SELECTOR", new Vector2(160f, -330f), new Vector2(290f, 80f), panel);
            Button menu = CreateButton(p, "MainMenuButton", "MENU", new Vector2(480f, -330f), new Vector2(290f, 80f), panel);

            var screen = GetOrAdd<ResultsScreen>(root.gameObject);
            SetRefs(screen, ("_scoreText", score), ("_accuracyText", accuracy), ("_comboText", combo), ("_punkText", punk),
                ("_countsText", counts), ("_evaluationText", evaluation), ("_retry", retry), ("_next", next), ("_nextLabel", nextLabel),
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
