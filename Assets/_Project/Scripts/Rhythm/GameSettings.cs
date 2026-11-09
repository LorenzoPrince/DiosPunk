using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Ajustes de runtime compartidos. El panel de Opciones y la calibración escriben aquí; SongClock y los
    /// efectos (p. ej. RhythmDebuffVisuals) lo leen. La latencia calibrada se persiste en progress.json.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>Accesibilidad: mitiga/anula la distorsión gráfica de los debuffs.</summary>
        public static bool ReduceVisualEffects;

        /// <summary>Calibración de latencia (ms) que SongClock toma al iniciar. Positivo = el jugador pulsa tarde.</summary>
        public static float LatencyOffsetMs;

        // Con "Enter Play Mode" sin recarga de dominio los estáticos sobreviven entre ejecuciones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ReduceVisualEffects = false;
            LatencyOffsetMs = 0f;
        }
    }
}
