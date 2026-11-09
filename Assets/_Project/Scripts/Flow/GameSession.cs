using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Estado que sobrevive al cambio de escena: nivel elegido, bandera para abrir el selector al volver
    /// al menú y el progreso cargado de progress.json (que además se guarda en disco ante cada cambio).
    /// No guarda referencias a assets (SongData/AudioClip), para que puedan liberarse al salir del gameplay.
    /// </summary>
    public static class GameSession
    {
        private static ProgressStore s_progress;
        private static int s_levelCount;

        /// <summary>Índice base 0 del nivel a jugar en la escena Gameplay.</summary>
        public static int SelectedLevelIndex { get; set; }

        /// <summary>Si es true, el menú abre directamente el selector de niveles (se consume al leerla).</summary>
        public static bool OpenLevelSelectOnMenu { get; set; }

        public static ProgressStore GetProgress(int levelCount)
        {
            if (s_progress == null || s_levelCount != levelCount)
            {
                s_progress = ProgressStore.Load(levelCount);
                s_levelCount = levelCount;
            }
            return s_progress;
        }

        public static bool ConsumeOpenLevelSelect()
        {
            bool value = OpenLevelSelectOnMenu;
            OpenLevelSelectOnMenu = false;
            return value;
        }

        // Con "Enter Play Mode Options" (sin recarga de dominio) los estáticos sobreviven entre ejecuciones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_progress = null;
            s_levelCount = 0;
            SelectedLevelIndex = 0;
            OpenLevelSelectOnMenu = false;
        }
    }
}
