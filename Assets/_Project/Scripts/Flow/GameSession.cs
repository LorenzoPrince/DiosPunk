using DiosPunk.Rhythm;
using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Estado que sobrevive al cambio de escena: nivel elegido, bandera para abrir el selector al volver
    /// al menú, variables de las respuestas narrativas y el progreso cargado de progress.json (que además se
    /// guarda en disco ante cada cambio). No guarda referencias a assets (SongData/AudioClip), para que
    /// puedan liberarse al salir del gameplay.
    /// </summary>
    public static class GameSession
    {
        private const float SpeedPerDifficultyStep = 0.15f;

        private static ProgressStore s_progress;
        private static int s_levelCount;

        /// <summary>Índice base 0 del nivel a jugar en la escena Gameplay.</summary>
        public static int SelectedLevelIndex { get; set; }

        /// <summary>Si es true, el menú abre directamente el selector de niveles (se consume al leerla).</summary>
        public static bool OpenLevelSelectOnMenu { get; set; }

        /// <summary>Ajuste de dificultad elegido en la escena narrativa: -1 más fácil, 0 normal, +1 más difícil.</summary>
        public static int DifficultyModifier { get; private set; }

        /// <summary>Variante de escenario elegida en la escena narrativa (0 = base).</summary>
        public static int ScenarioVariant { get; private set; }

        /// <summary>Multiplicador de velocidad de caída derivado de la dificultad.</summary>
        public static float NoteSpeedMultiplier => 1f + SpeedPerDifficultyStep * DifficultyModifier;

        /// <summary>true si ya existe progress.json (no es la primera partida).</summary>
        public static bool HasSavedProgress => ProgressStore.FileExists;

        public static ProgressStore GetProgress(int levelCount)
        {
            if (s_progress == null || s_levelCount != levelCount)
            {
                s_progress = ProgressStore.Load(levelCount);
                s_levelCount = levelCount;

                // La latencia calibrada se aplica apenas se conoce el progreso.
                if (s_progress.Calibrated) GameSettings.LatencyOffsetMs = s_progress.LatencyOffsetMs;
            }
            return s_progress;
        }

        public static bool ConsumeOpenLevelSelect()
        {
            bool value = OpenLevelSelectOnMenu;
            OpenLevelSelectOnMenu = false;
            return value;
        }

        public static void ResetNarrativeChoices()
        {
            DifficultyModifier = 0;
            ScenarioVariant = 0;
        }

        public static void ApplyChoice(ChoiceVariable variable, int value)
        {
            switch (variable)
            {
                case ChoiceVariable.Difficulty: DifficultyModifier = Mathf.Clamp(value, -2, 2); break;
                case ChoiceVariable.ScenarioVariant: ScenarioVariant = Mathf.Max(0, value); break;
            }
        }

        // Con "Enter Play Mode Options" (sin recarga de dominio) los estáticos sobreviven entre ejecuciones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_progress = null;
            s_levelCount = 0;
            SelectedLevelIndex = 0;
            OpenLevelSelectOnMenu = false;
            DifficultyModifier = 0;
            ScenarioVariant = 0;
        }
    }
}
