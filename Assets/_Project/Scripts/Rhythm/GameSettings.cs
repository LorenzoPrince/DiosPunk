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

        /// <summary>Volumen de efectos de sonido (0..1), independiente de la música. Lo ajusta el slider de Opciones.</summary>
        public static float SfxVolume = 1f;

        // Con "Enter Play Mode" sin recarga de dominio los estáticos sobreviven entre ejecuciones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ReduceVisualEffects = false;
            LatencyOffsetMs = 0f;
            SfxVolume = 1f;
        }
    }
}
