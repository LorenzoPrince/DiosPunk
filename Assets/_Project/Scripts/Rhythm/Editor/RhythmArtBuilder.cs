using System;
using System.IO;
using DiosPunk.Flow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>
    /// Integra el arte definitivo de Assets/_Project/Imports/Art: configura el Texture Importer de los sprites, crea el
    /// StageTheme del Nivel 1 (peatonal) con la geometría medida sobre el dibujo y arma el menú principal y la pantalla de
    /// pausa con el arte. Todo es idempotente y lo ejecuta DiosPunk/Scenes/Build MainMenu + Gameplay: regenerar las escenas
    /// no pierde las referencias. Si falta algún archivo, esa pantalla cae a los placeholders.
    /// </summary>
    public static partial class RhythmTestSetupBuilder
    {
        private const string ArtImportDir = "Assets/_Project/Imports/Art";
        private const string StageDir = "Assets/_Project/Resources/Stages";
        private const string Stage01Name = "Stage_01_Peatonal";

        // Subcarpetas que NO son assets del juego: guías de proporciones y placeholders del selector de niveles.
        private static readonly string[] s_artSkip = { "Guías de proporciones", "MENU_JUGAR (PLACEHOLDERS)" };

        private static Sprite Art(string relativePath) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtImportDir + "/" + relativePath);

        // ---------------- Importer ----------------

        /// <summary>
        /// Sprites de UI / 2D: Sprite (2D and UI), filtro Bilinear, sin mipmaps, Mesh Type Full Rect y PPU 100. Las texturas
        /// grandes (fondos e ilustraciones) se comprimen en alta calidad; las chicas (botones, notas) quedan sin comprimir
        /// para no ensuciar los trazos.
        /// </summary>
        private static int ConfigureArtImports()
        {
            if (!Directory.Exists(ArtImportDir)) return 0;

            int changed = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtImportDir });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsSkippedArt(path)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);

                bool large = false;
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                large = Mathf.Max(w, h) >= 1024;
                TextureImporterCompression compression = large ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;

                bool ok = importer.textureType == TextureImporterType.Sprite
                          && importer.spriteImportMode == SpriteImportMode.Single
                          && importer.filterMode == FilterMode.Bilinear
                          && !importer.mipmapEnabled
                          && importer.wrapMode == TextureWrapMode.Clamp
                          && importer.alphaIsTransparency
                          && Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
                          && importer.textureCompression == compression
                          && importer.maxTextureSize >= 2048
                          && settings.spriteMeshType == SpriteMeshType.FullRect;
                if (ok) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.textureCompression = compression;
                importer.maxTextureSize = 2048;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                changed++;
            }
            return changed;
        }

        private static bool IsSkippedArt(string path)
        {
            for (int i = 0; i < s_artSkip.Length; i++)
            {
                if (path.IndexOf(s_artSkip[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        // ---------------- StageTheme del Nivel 1 ----------------

        // Medidas sobre Highway.png (1047 x 709 px): rectas de los centros de carril x(y) = c + s*y (px del dibujo).
        private static readonly float[] s_laneC = { 443.71f, 471.79f, 510.94f, 534.85f };
        private static readonly float[] s_laneS = { -0.4762f, -0.1369f, 0.1999f, 0.5855f };

        private static StageTheme CreateStage01()
        {
            Sprite background = Art("PrimerNivel_Peatonal/Background.png");
            Sprite highway = Art("PrimerNivel_Peatonal/Highway.png");
            if (background == null || highway == null) return null;

            Directory.CreateDirectory(StageDir);
            string path = StageDir + "/" + Stage01Name + ".asset";
            var theme = AssetDatabase.LoadAssetAtPath<StageTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<StageTheme>();
                AssetDatabase.CreateAsset(theme, path);
            }

            // Geometría: 1 px del dibujo = 0,0138 u (artScale 1,38 con PPU 100). Los receptáculos están en y = 590 px y
            // el extremo lejano de los carriles en y = 45 px; el eje X de mundo 0 cae en x = 516 px (promedio de los carriles).
            const float f = 0.0138f;
            const float hitY = -3.5f;
            const float xOrigin = 516f;
            const float yHitPx = 590f, yTopPx = 45f;
            const float highwayCenterXpx = 523.5f, highwayCenterYpx = 354.5f;

            var bottom = new float[4];
            var top = new float[4];
            for (int i = 0; i < 4; i++)
            {
                bottom[i] = (s_laneC[i] + s_laneS[i] * yHitPx - xOrigin) * f;
                top[i] = (s_laneC[i] + s_laneS[i] * yTopPx - xOrigin) * f;
            }
            float spawnY = hitY + (yHitPx - yTopPx) * f;
            // Conserva el tiempo de anticipación del escenario placeholder (9,5 u a 8 u/s = 1,19 s).
            float noteSpeed = (spawnY - hitY) / (9.5f / 8f);

            var so = new SerializedObject(theme);
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_highway").objectReferenceValue = highway;
            so.FindProperty("_highwayPosition").vector2Value =
                new Vector2((highwayCenterXpx - xOrigin) * f, hitY + (yHitPx - highwayCenterYpx) * f);

            // Carriles: 0 rojo, 1 amarillo, 2 verde, 3 azul.
            string[] colors = { "Roja", "Amarilla", "Verde", "Azul" };
            string[] receptacleColors = { "rojo", "amarillo", "verde", "azul" };
            SetSpriteArray(so.FindProperty("_notes"), i => Art("PrimerNivel_Peatonal/Nota_" + colors[i] + ".png"), 4);
            SetSpriteArray(so.FindProperty("_receptacles"), i => Art("PrimerNivel_Peatonal/Receptaculo_" + receptacleColors[i] + ".png"), 4);
            SetFloatArray(so.FindProperty("_laneBottomX"), bottom);
            SetFloatArray(so.FindProperty("_laneTopX"), top);

            so.FindProperty("_artScale").floatValue = f * 100f;
            so.FindProperty("_hitLineY").floatValue = hitY;
            so.FindProperty("_spawnY").floatValue = spawnY;
            so.FindProperty("_noteSpeed").floatValue = noteSpeed;
            so.FindProperty("_noteTopScale").floatValue = 0.22f;
            so.FindProperty("_keyLabelOffsetY").floatValue = 0f; // rótulo centrado sobre el receptáculo

            SetSpriteArray(so.FindProperty("_perfect"), i => Art("PrimerNivel_Peatonal/Perfect_" + (i + 1) + ".png"), 2);
            SetSpriteArray(so.FindProperty("_good"), i => Art("PrimerNivel_Peatonal/Good_" + (i + 1) + ".png"), 2);
            SetSpriteArray(so.FindProperty("_miss"), i => Art("PrimerNivel_Peatonal/Miss_" + (i + 1) + ".png"), 2);
            so.FindProperty("_scorePanel").objectReferenceValue = Art("PrimerNivel_Peatonal/Puntaje+combo.png");
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssetIfDirty(theme);
            Debug.Log("[Art] StageTheme " + Stage01Name + ": spawnY " + spawnY.ToString("F2") + ", noteSpeed " + noteSpeed.ToString("F2") +
                      ", carriles abajo X " + string.Join("/", Array.ConvertAll(bottom, v => v.ToString("F2"))) +
                      ", arriba X " + string.Join("/", Array.ConvertAll(top, v => v.ToString("F2"))) + ".");
            return theme;
        }

        private static void SetSpriteArray(SerializedProperty array, Func<int, Sprite> source, int count)
        {
            array.arraySize = count;
            for (int i = 0; i < count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = source(i);
        }

        private static void SetFloatArray(SerializedProperty array, float[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).floatValue = values[i];
        }

        // ---------------- Helpers de botones con arte ----------------

        /// <summary>Botón cuyo texto viene dibujado en el sprite: imagen Simple a tamaño nativo × scale, con Raycast Target.</summary>
        private static Button CreateArtButton(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, float scale)
        {
            Vector2 size = sprite.rect.size * scale;
            Image image = FindOrCreateImage(parent, name, sprite, Color.white, anchor, Center, position, size, Image.Type.Simple);
            image.raycastTarget = true; // los botones sí reciben clics
            image.preserveAspect = true;

            var button = GetOrAdd<Button>(image.gameObject);
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.93f, 0.93f, 0.93f, 1f); // la marca de selección ya indica el foco
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
            button.colors = colors;
            return button;
        }

        private static void SetIntroStep(SerializedProperty step, RectTransform rect, CanvasGroup group, float delay, float duration,
            Vector2 offset, float startScale)
        {
            step.FindPropertyRelative("rect").objectReferenceValue = rect;
            step.FindPropertyRelative("group").objectReferenceValue = group;
            step.FindPropertyRelative("delay").floatValue = delay;
            step.FindPropertyRelative("duration").floatValue = duration;
            step.FindPropertyRelative("offset").vector2Value = offset;
            step.FindPropertyRelative("startScale").floatValue = startScale;
        }

        private static void AddMarkerTarget(Button button, SelectionMarker marker)
        {
            var target = GetOrAdd<SelectionMarkerTarget>(button.gameObject);
            SetRefs(target, ("_marker", marker));
        }

        // ---------------- Menú principal con arte ----------------

        private static bool MenuArtAvailable()
        {
            return Art("Menu_Principal/Background.png") != null && Art("Menu_Principal/DiosPunk_Ilustracion.png") != null
                   && Art("Menu_Principal/Título_Logo.png") != null && Art("Menu_Principal/Boton_Jugar.png") != null
                   && Art("Menu_Principal/Boton_Opciones.png") != null && Art("Menu_Principal/Boton_Creditos.png") != null
                   && Art("Menu_Principal/Boton_Salir.png") != null && Art("Menu_Principal/Hover_Selección.png") != null;
        }

        /// <summary>
        /// Composición del boceto (MenuPrincipal_Thumbnail, 1920 x 1090): fondo, ilustración a la derecha, banner del logo
        /// arriba y los 4 botones de papel a la izquierda con el cable que marca la opción enfocada. "PARTIDA NUEVA" no tiene
        /// arte: queda como enlace de texto abajo a la derecha. Los botones llevan Raycast Target.
        /// </summary>
        private static MainMenuScreen BuildMainMenuWithArt(RectTransform root, Sprite panel, Sprite white)
        {
            Vector2 topLeft = new Vector2(0f, 1f);
            const float yFix = 1080f / 1090f; // el arte se diseñó a 1090 px de alto

            Image bg = FindOrCreateImage(root, "ArtBackground", Art("Menu_Principal/Background.png"), Color.white, Center, Center,
                Vector2.zero, Vector2.zero, Image.Type.Simple);
            Stretch(bg.rectTransform);

            Sprite illustration = Art("Menu_Principal/DiosPunk_Ilustracion.png");
            Image illustrationImage = FindOrCreateImage(root, "ArtIllustration", illustration, Color.white, new Vector2(1f, 0f), new Vector2(1f, 0f),
                Vector2.zero, illustration.rect.size, Image.Type.Simple);

            Image logo = FindOrCreateImage(root, "ArtLogo", Art("Menu_Principal/Título_Logo.png"), Color.white, new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 413f), Image.Type.Simple);
            logo.rectTransform.anchorMin = new Vector2(0f, 1f);
            logo.rectTransform.anchorMax = new Vector2(1f, 1f);
            logo.rectTransform.sizeDelta = new Vector2(0f, 413f);

            // Cable de selección (Hover_Selección): se dibuja antes que los botones para quedar detrás.
            Sprite cableSprite = Art("Menu_Principal/Hover_Selección.png");
            Image cable = FindOrCreateImage(root, "SelectionCable", cableSprite, Color.white, topLeft, new Vector2(1f, 0.5f),
                Vector2.zero, cableSprite.rect.size, Image.Type.Simple);
            var marker = GetOrAdd<SelectionMarker>(cable.gameObject);

            // Posiciones (centro de cada botón) tomadas del boceto, en px desde la esquina superior izquierda.
            Button play = CreateArtButton(root, "PlayButton", Art("Menu_Principal/Boton_Jugar.png"), topLeft, new Vector2(402f, -442f * yFix), 1f);
            Button options = CreateArtButton(root, "OptionsButton", Art("Menu_Principal/Boton_Opciones.png"), topLeft, new Vector2(288f, -605f * yFix), 1f);
            Button credits = CreateArtButton(root, "CreditsButton", Art("Menu_Principal/Boton_Creditos.png"), topLeft, new Vector2(370f, -785f * yFix), 1f);
            Button quit = CreateArtButton(root, "QuitButton", Art("Menu_Principal/Boton_Salir.png"), topLeft, new Vector2(264f, -957f * yFix), 1f);

            // El texto "JUGAR" viene dibujado: la etiqueta de código (CONTINUAR / JUGAR) queda oculta pero referenciada.
            TMP_Text playLabel = FindOrCreateText(play.transform, "Label", "JUGAR", 10f, TextAlignmentOptions.Center, Center, Center,
                Vector2.zero, Vector2.zero, Color.clear);
            playLabel.enabled = false;

            SetRefs(marker, ("_marker", cable.rectTransform), ("_defaultTarget", (RectTransform)play.transform));
            var markerSo = new SerializedObject(marker);
            markerSo.FindProperty("_placement").enumValueIndex = (int)SelectionMarker.Placement.LeftOfTarget;
            markerSo.FindProperty("_offset").vector2Value = new Vector2(8f, -55f); // el enchufe del cable queda a la altura del texto
            markerSo.ApplyModifiedPropertiesWithoutUndo();
            AddMarkerTarget(play, marker);
            AddMarkerTarget(options, marker);
            AddMarkerTarget(credits, marker);
            AddMarkerTarget(quit, marker);

            // PARTIDA NUEVA: sin arte propio, enlace de texto sobre la ilustración (esquina inferior derecha). UBICACIÓN
            // PROVISIONAL hasta que Arte dibuje su botón; el cable de selección también lo marca.
            Image newGameHit = FindOrCreateImage(root, "NewGameButton", null, Color.clear, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-40f, 40f), new Vector2(330f, 64f), Image.Type.Simple);
            newGameHit.raycastTarget = true;
            TMP_Text newGameLabel = FindOrCreateText(newGameHit.transform, "Label", "PARTIDA NUEVA", 36f, TextAlignmentOptions.Right,
                Center, Center, Vector2.zero, new Vector2(330f, 64f), Color.white);
            newGameLabel.fontStyle = FontStyles.Bold;
            newGameLabel.textWrappingMode = TextWrappingModes.NoWrap; // una sola línea
            newGameLabel.overflowMode = TextOverflowModes.Overflow;
            var newGame = GetOrAdd<Button>(newGameHit.gameObject);
            newGame.targetGraphic = newGameLabel;
            newGame.transition = Selectable.Transition.ColorTint;
            ColorBlock ngColors = newGame.colors;
            ngColors.normalColor = Color.white;
            ngColors.highlightedColor = new Color(1f, 0.85f, 0.1f, 1f);
            ngColors.selectedColor = new Color(1f, 0.85f, 0.1f, 1f);
            ngColors.pressedColor = new Color(0.8f, 0.65f, 0.05f, 1f);
            newGame.colors = ngColors;
            AddMarkerTarget(newGame, marker);

            // Entrada en cascada (orden de Animación_ux.mp4): fondo, ilustración, logo, botones y cable.
            var rootGroup = GetOrAdd<CanvasGroup>(root.gameObject);
            var bgGroup = GetOrAdd<CanvasGroup>(bg.gameObject);
            var illusGroup = GetOrAdd<CanvasGroup>(illustrationImage.gameObject);
            var logoGroup = GetOrAdd<CanvasGroup>(logo.gameObject);
            var cableGroup = GetOrAdd<CanvasGroup>(cable.gameObject);
            var playGroup = GetOrAdd<CanvasGroup>(play.gameObject);
            var optionsGroup = GetOrAdd<CanvasGroup>(options.gameObject);
            var creditsGroup = GetOrAdd<CanvasGroup>(credits.gameObject);
            var quitGroup = GetOrAdd<CanvasGroup>(quit.gameObject);
            var newGameGroup = GetOrAdd<CanvasGroup>(newGameHit.gameObject);

            var intro = GetOrAdd<MenuIntroAnimator>(root.gameObject);
            var introSo = new SerializedObject(intro);
            SerializedProperty steps = introSo.FindProperty("_steps");
            steps.arraySize = 10;
            // rect, group, delay, duration, offset (px desde donde entra), escala inicial
            SetIntroStep(steps.GetArrayElementAtIndex(0), bg.rectTransform, bgGroup, 0.00f, 0.30f, Vector2.zero, 1.04f);
            SetIntroStep(steps.GetArrayElementAtIndex(1), illustrationImage.rectTransform, illusGroup, 0.20f, 0.45f, new Vector2(140f, 0f), 1f);
            SetIntroStep(steps.GetArrayElementAtIndex(2), logo.rectTransform, logoGroup, 0.50f, 0.40f, new Vector2(0f, 90f), 1f);
            SetIntroStep(steps.GetArrayElementAtIndex(3), (RectTransform)play.transform, playGroup, 0.80f, 0.30f, new Vector2(-160f, 0f), 1f);
            SetIntroStep(steps.GetArrayElementAtIndex(4), (RectTransform)options.transform, optionsGroup, 0.90f, 0.30f, new Vector2(-160f, 0f), 1f);
            SetIntroStep(steps.GetArrayElementAtIndex(5), (RectTransform)credits.transform, creditsGroup, 1.00f, 0.30f, new Vector2(-160f, 0f), 1f);
            SetIntroStep(steps.GetArrayElementAtIndex(6), (RectTransform)quit.transform, quitGroup, 1.10f, 0.30f, new Vector2(-160f, 0f), 1f);
            SetIntroStep(steps.GetArrayElementAtIndex(7), (RectTransform)newGame.transform, newGameGroup, 1.20f, 0.30f, new Vector2(0f, -60f), 1f);
            SetIntroStep(steps.GetArrayElementAtIndex(8), cable.rectTransform, cableGroup, 1.40f, 0.25f, Vector2.zero, 1f);
            steps.arraySize = 9;
            introSo.FindProperty("_inputGate").objectReferenceValue = rootGroup;
            introSo.FindProperty("_marker").objectReferenceValue = marker;
            introSo.ApplyModifiedPropertiesWithoutUndo();

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

        // ---------------- Pausa con arte ----------------

        private static bool PauseArtAvailable()
        {
            return Art("Pantalla_Pausa/Pausa_Papel.png") != null && Art("Pantalla_Pausa/Boton_Reanudar.png") != null
                   && Art("Pantalla_Pausa/Boton_Reiniciar.png") != null && Art("Pantalla_Pausa/Boton_Opciones.png") != null
                   && Art("Pantalla_Pausa/Boton_Salir.png") != null && Art("Pantalla_Pausa/Selección_Marca+Flecha.png") != null;
        }

        /// <summary>
        /// Hoja de libreta rasgada con "PAUSA" y la calavera dibujados; los 4 botones (versiones rotadas, como el boceto) van
        /// debajo y la marca "Selección_Marca+Flecha" subraya el botón enfocado. Los botones se agrandan ×1,6 para que el
        /// texto escrito a máquina se lea.
        /// </summary>
        private static PauseMenuScreen BuildPauseWithArt(RectTransform root)
        {
            Sprite paperSprite = Art("Pantalla_Pausa/Pausa_Papel.png");
            Image paper = FindOrCreateImage(root, "Paper", paperSprite, Color.white, Center, Center, Vector2.zero,
                paperSprite.rect.size * 1.2f, Image.Type.Simple);
            RectTransform p = paper.rectTransform;

            Sprite markSprite = Art("Pantalla_Pausa/Selección_Marca+Flecha.png");
            Image mark = FindOrCreateImage(p, "SelectionMark", markSprite, Color.white, Center, new Vector2(0.5f, 1f), Vector2.zero,
                markSprite.rect.size, Image.Type.Simple);
            var marker = GetOrAdd<SelectionMarker>(mark.gameObject);

            const float s = 1.6f;
            Button resume = CreateArtButton(p, "ResumeButton", Art("Pantalla_Pausa/Boton_Reanudar.png"), Center, new Vector2(0f, 30f), s);
            Button restart = CreateArtButton(p, "RestartButton", Art("Pantalla_Pausa/Boton_Reiniciar.png"), Center, new Vector2(0f, -100f), s);
            Button options = CreateArtButton(p, "OptionsButton", Art("Pantalla_Pausa/Boton_Opciones.png"), Center, new Vector2(0f, -230f), s);
            Button exit = CreateArtButton(p, "ExitButton", Art("Pantalla_Pausa/Boton_Salir.png"), Center, new Vector2(0f, -360f), s);

            SetRefs(marker, ("_marker", mark.rectTransform), ("_defaultTarget", (RectTransform)resume.transform));
            var markerSo = new SerializedObject(marker);
            markerSo.FindProperty("_placement").enumValueIndex = (int)SelectionMarker.Placement.BelowTarget;
            markerSo.FindProperty("_offset").vector2Value = new Vector2(0f, 6f);
            markerSo.FindProperty("_widthFactor").floatValue = 1.25f;
            markerSo.ApplyModifiedPropertiesWithoutUndo();
            AddMarkerTarget(resume, marker);
            AddMarkerTarget(restart, marker);
            AddMarkerTarget(options, marker);
            AddMarkerTarget(exit, marker);

            var screen = GetOrAdd<PauseMenuScreen>(root.gameObject);
            SetRefs(screen, ("_resume", resume), ("_restart", restart), ("_options", options), ("_exit", exit));
            return screen;
        }
    }
}
