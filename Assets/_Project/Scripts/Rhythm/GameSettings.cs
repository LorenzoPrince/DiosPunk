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
        public static float LatencyOffsetMs
        {
            get => s_latencyOffsetMs;
            set
            {
                value = value < -300f ? -300f : (value > 300f ? 300f : value);
                if (s_latencyOffsetMs == value) return;
                s_latencyOffsetMs = value;
                LatencyOffsetMsChanged?.Invoke(value);
            }
        }

        private static float s_latencyOffsetMs = DefaultLatencyOffsetMs;

        /// <summary>Se dispara al cambiar la latencia (calibración, ajuste fino en Opciones): SongClock la toma en vivo.</summary>
        public static event System.Action<float> LatencyOffsetMsChanged;

        private static ControlScheme s_controlScheme = ControlScheme.All;

        /// <summary>Esquema de teclas activo. Al cambiarlo se notifica por evento (input y rótulos se actualizan solos).</summary>
        public static ControlScheme ControlScheme
        {
            get => s_controlScheme;
            set
            {
                if (s_controlScheme == value) return;
                s_controlScheme = value;
                ControlSchemeChanged?.Invoke(value);
            }
        }

        public static event System.Action<ControlScheme> ControlSchemeChanged;

        private static bool s_immortalMode;

        /// <summary>Modo Inmortal / No Fail: la vida no baja de 1 HP (sin Game Over). Se activa en Opciones o con V (debug).</summary>
        public static bool ImmortalMode
        {
            get => s_immortalMode;
            set
            {
                if (s_immortalMode == value) return;
                s_immortalMode = value;
                ImmortalModeChanged?.Invoke(value);
            }
        }

        public static event System.Action<bool> ImmortalModeChanged;

        /// <summary>Volumen de efectos de sonido (0..1), independiente de la música. Lo ajusta el slider de Opciones.</summary>
        public static float SfxVolume = 1f;

        // Con "Enter Play Mode" sin recarga de dominio los estáticos sobreviven entre ejecuciones.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ReduceVisualEffects = false;
            s_latencyOffsetMs = DefaultLatencyOffsetMs;
            LatencyOffsetMsChanged = null;
            SfxVolume = 1f;
            s_controlScheme = ControlScheme.All;
            ControlSchemeChanged = null;
            s_immortalMode = false;
            ImmortalModeChanged = null;
        }
    }
}
