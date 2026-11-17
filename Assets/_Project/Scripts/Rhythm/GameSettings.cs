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

        /// <summary>
        /// Latencia por defecto (ms) para quien omite la calibración: típica de audio + input en PC/Windows.
        /// Positivo = el jugador pulsa tarde por la latencia, y se compensa restándola al tiempo del input.
        /// </summary>
        public const float DefaultLatencyOffsetMs = 25f;

        /// <summary>Calibración de latencia (ms) que SongClock toma al iniciar. Positivo = el jugador pulsa tarde.</summary>
        public static float LatencyOffsetMs = DefaultLatencyOffsetMs;

        /// <summary>Volumen de efectos de sonido (0..1), independiente de la música. Lo ajusta el slider de Opciones.</summary>
        public static float SfxVolume = 1f;

        // Con "Enter Play Mode" sin recarga de dominio los estáticos sobreviven entre ejecuciones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ReduceVisualEffects = false;
            LatencyOffsetMs = DefaultLatencyOffsetMs;
            SfxVolume = 1f;
        }
    }
}
