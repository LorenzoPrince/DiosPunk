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
                "Es tu primera partida.\nVamos a calibrar la latencia de tu equipo:\npulsa cualquier tecla al ritmo del metronomo.\nDespues practicaras los 4 carriles.",
                46f, TextAlignmentOptions.Center, Center, Center, new Vector2(0f, 100f), new Vector2(1400f, 360f), Color.white);
            Button startCalibration = CreateButton(intro.transform, "StartButton", "EMPEZAR CALIBRACION",
                new Vector2(0f, -170f), new Vector2(680f, 100f), panel);

            // --- Calibrando: pulso visual + contador ---
            GameObject calibrating = FindOrCreateRect(root, "CalibratingPanel", phasePos, phaseSize);
            Image pulse = FindOrCreateImage(calibrating.transform, "Pulse", fx.Shield, Color.white, Center, Center,
                new Vector2(0f, 60f), new Vector2(300f, 300f), Image.Type.Simple);
            TMP_Text progress = FindOrCreateText(calibrating.transform, "ProgressText", "", 44f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, -200f), new Vector2(1400f, 80f), Color.white);

            // --- Resultado ---
            GameObject result = FindOrCreateRect(root, "ResultPanel", phasePos, phaseSize);
            TMP_Text resultText = FindOrCreateText(result.transform, "ResultText", "", 48f, TextAlignmentOptions.Center,
                Center, Center, new Vector2(0f, 90f), new Vector2(1400f, 300f), Color.white);
            Button retry = CreateButton(result.transform, "RetryButton", "REPETIR", new Vector2(-260f, -170f), new Vector2(440f, 100f), panel);
            Button accept = CreateButton(result.transform, "AcceptButton", "ACEPTAR", new Vector2(260f, -170f), new Vector2(440f, 100f), panel);
            TMP_Text acceptLabel = accept.transform.Find("Label").GetComponent<TMP_Text>();

            // --- Tutorial de controles: 4 carriles con su color ---
            GameObject tutorial = FindOrCreateRect(root, "TutorialPanel", phasePos, phaseSize);
            Color[] laneColors = LaneStyle.CreateDefaultColors();
            var boxes = new Image[RhythmInputReader.LaneCount];
            for (int i = 0; i < boxes.Length; i++)
            {
                Color c = laneColors[i];
                c.a = 0.35f;
                boxes[i] = FindOrCreateImage(tutorial.transform, "Lane" + i, panel, c, Center, Center,
                    new Vector2((i - 1.5f) * 260f, 100f), new Vector2(220f, 220f), Image.Type.Sliced);
                FindOrCreateText(boxes[i].transform, "Key", KeyLabels[i], 50f, TextAlignmentOptions.Center, Center, Center,
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
            SetRefArray(screen, "_laneBoxes", boxes);

            calibrating.SetActive(false);
            result.SetActive(false);
            tutorial.SetActive(false);
            return screen;
        }
    }
}
