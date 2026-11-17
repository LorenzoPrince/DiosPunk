using DiosPunk.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>Arma la pantalla de calibración + tutorial de controles de la primera partida (escena MainMenu).</summary>
    public static partial class RhythmTestSetupBuilder
    {
        private static CalibrationScreen BuildCalibration(RectTransform root, Sprite panel, Sprite white, FxAssets fx)
        {
            FindOrCreateText(root, "Title", "CALIBRACION Y TUTORIAL", 80f, TextAlignmentOptions.Center, Center, Center,
                new Vector2(0f, 400f), new Vector2(1400f, 110f), Red);

            Vector2 phasePos = new Vector2(0f, 40f);
            Vector2 phaseSize = new Vector2(1500f, 600f);

            // --- Introducción ---
            GameObject intro = FindOrCreateRect(root, "IntroPanel", phasePos, phaseSize);
            FindOrCreateText(intro.transform, "Text",
                "Es tu primera partida.\nVamos a calibrar la latencia de tu equipo:\npulsa una tecla (o la Barra Espaciadora) cuando la barra toque la linea.\nDespues practicaras los 4 carriles.",
                46f, TextAlignmentOptions.Center, Center, Center, new Vector2(0f, 100f), new Vector2(1400f, 360f), Color.white);
            Button startCalibration = CreateButton(intro.transform, "StartButton", "EMPEZAR CALIBRACION",
                new Vector2(0f, -170f), new Vector2(680f, 100f), panel);

            // --- Calibrando: pulso visual + contador ---
            GameObject calibrating = FindOrCreateRect(root, "CalibratingPanel", phasePos, phaseSize);
            // Pista rítmica: el marcador viaja de izquierda a derecha y toca la línea roja justo con cada clic.
            FindOrCreateImage(calibrating.transform, "RhythmTrack", white, new Color(0.9f, 0.9f, 0.86f, 1f), Center, Center,
                new Vector2(-60f, 110f), new Vector2(900f, 150f), Image.Type.Simple);
            Image pulse = FindOrCreateImage(calibrating.transform, "Pulse", fx.Shield, new Color(1f, 0.85f, 0.1f, 1f), Center, Center,
                new Vector2(300f, 110f), new Vector2(210f, 210f), Image.Type.Simple);
            FindOrCreateImage(calibrating.transform, "ImpactLine", white, new Color(0.85f, 0.1f, 0.1f, 1f), Center, Center,
                new Vector2(300f, 110f), new Vector2(12f, 190f), Image.Type.Simple);
            Image marker = FindOrCreateImage(calibrating.transform, "Marker", panel, new Color(0.1f, 0.1f, 0.1f, 1f), Center, Center,
                new Vector2(-420f, 110f), new Vector2(34f, 130f), Image.Type.Sliced);
            FindOrCreateText(calibrating.transform, "Hint", "Pulsa cuando la barra toque la linea roja", 40f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, -40f), new Vector2(1400f, 60f), Color.white);
            FindOrCreateText(calibrating.transform, "KeysHint", "Cualquier tecla de carril de tu esquema, o Barra Espaciadora", 30f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, -100f), new Vector2(1400f, 50f), new Color(0.75f, 0.75f, 0.75f, 1f));
            TMP_Text progress = FindOrCreateText(calibrating.transform, "ProgressText", "", 44f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, -190f), new Vector2(1400f, 80f), Color.white);

            // --- Resultado: texto, ajuste fino -5 / +5 ms con el valor actual, restaurar y aceptar/repetir ---
            GameObject result = FindOrCreateRect(root, "ResultPanel", phasePos, phaseSize);
            TMP_Text resultText = FindOrCreateText(result.transform, "ResultText", "", 42f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, 150f), new Vector2(1400f, 280f), Color.white);
            Button offsetMinus = CreateButton(result.transform, "OffsetMinusButton", "-5", new Vector2(-300f, -40f), new Vector2(150f, 80f), panel);
            Button offsetPlus = CreateButton(result.transform, "OffsetPlusButton", "+5", new Vector2(300f, -40f), new Vector2(150f, 80f), panel);
            TMP_Text offsetText = FindOrCreateText(result.transform, "OffsetText", "Latencia: +25 ms", 56f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, -40f), new Vector2(520f, 80f), new Color(1f, 0.85f, 0.1f, 1f));
            Button offsetRestore = CreateButton(result.transform, "OffsetRestoreButton", "Restaurar +25 ms", new Vector2(0f, -130f), new Vector2(520f, 70f), panel);
            offsetRestore.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 30f;
            Button retry = CreateButton(result.transform, "RetryButton", "REPETIR", new Vector2(-260f, -230f), new Vector2(440f, 90f), panel);
            Button accept = CreateButton(result.transform, "AcceptButton", "ACEPTAR", new Vector2(260f, -230f), new Vector2(440f, 90f), panel);
            TMP_Text acceptLabel = accept.transform.Find("Label").GetComponent<TMP_Text>();

            // --- Tutorial de controles: 4 carriles con su color ---
            GameObject tutorial = FindOrCreateRect(root, "TutorialPanel", phasePos, phaseSize);
            Color[] laneColors = LaneStyle.CreateDefaultColors();
            var boxes = new Image[RhythmInputReader.LaneCount];
            var keyTexts = new TMP_Text[RhythmInputReader.LaneCount];
            for (int i = 0; i < boxes.Length; i++)
            {
                Color c = laneColors[i];
                c.a = 0.35f;
                boxes[i] = FindOrCreateImage(tutorial.transform, "Lane" + i, panel, c, Center, Center,
                    new Vector2((i - 1.5f) * 260f, 100f), new Vector2(220f, 220f), Image.Type.Sliced);
                keyTexts[i] = FindOrCreateText(boxes[i].transform, "Key", ControlSchemes.GetLaneLabel(ControlScheme.All, i), 50f, TextAlignmentOptions.Center, Center, Center,
                    Vector2.zero, new Vector2(220f, 220f), Color.black);
            }
            TMP_Text status = FindOrCreateText(tutorial.transform, "StatusText", "", 44f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, -70f), new Vector2(1400f, 80f), Color.white);
            Button finish = CreateButton(tutorial.transform, "FinishButton", "COMENZAR NIVEL 1",
                new Vector2(0f, -200f), new Vector2(640f, 100f), panel);

            // --- Botones persistentes ---
            Button skip = CreateButton(root, "SkipButton", "OMITIR CALIBRACION", new Vector2(380f, -430f), new Vector2(620f, 80f), panel);
            Button back = CreateButton(root, "BackButton", "VOLVER", new Vector2(-380f, -430f), new Vector2(360f, 80f), panel);

            // --- Audio del metrónomo (dos fuentes alternadas) ---
            AudioSource[] sources = root.GetComponents<AudioSource>();
            while (sources.Length < 2)
            {
                root.gameObject.AddComponent<AudioSource>();
                sources = root.GetComponents<AudioSource>();
            }
            foreach (AudioSource s in sources) s.playOnAwake = false;

            // Reloj propio (SongClock) que provee la conversión de timestamps de input a tiempo DSP.
            Transform clockTransform = root.Find("CalibrationClock");
            GameObject clockGo = clockTransform != null ? clockTransform.gameObject : new GameObject("CalibrationClock");
            clockGo.transform.SetParent(root, false);
            var clock = GetOrAdd<SongClock>(clockGo);

            var screen = GetOrAdd<CalibrationScreen>(root.gameObject);
            SetRefs(screen, ("_clock", clock), ("_acceptLabel", acceptLabel), ("_introPanel", intro), ("_calibratingPanel", calibrating), ("_resultPanel", result),
                ("_tutorialPanel", tutorial), ("_startCalibration", startCalibration), ("_retry", retry),
                ("_accept", accept), ("_finishTutorial", finish), ("_skip", skip), ("_back", back), ("_pulse", pulse),
                ("_progressText", progress), ("_resultText", resultText), ("_clickSourceA", sources[0]),
                ("_clickSourceB", sources[1]), ("_clickClip", fx.Click), ("_tutorialStatus", status));
            SetRefs(screen, ("_marker", marker.rectTransform), ("_offsetText", offsetText), ("_offsetMinus", offsetMinus),
                ("_offsetPlus", offsetPlus), ("_offsetRestore", offsetRestore));
            SetRefArray(screen, "_laneBoxes", boxes);
            SetRefArray(screen, "_laneKeyLabels", keyTexts);

            calibrating.SetActive(false);
            result.SetActive(false);
            tutorial.SetActive(false);
            return screen;
        }
    }
}
