using System;

namespace DiosPunk.Rhythm
{
    public enum HitType : byte
    {
        /// <summary>El input no corresponde a ninguna nota (se ignora).</summary>
        None = 0,
        Perfect = 1,
        Good = 2,
        Miss = 3
    }

    /// <summary>
    /// Ventanas de juicio en milisegundos. Lógica pura, sin dependencias de MonoBehaviour (testeable en EditMode).
    /// </summary>
    [Serializable]
    public struct HitWindows
    {
        public float perfectMs;
        public float goodMs;

        /// <summary>
        /// Un input anterior a la nota por más de goodMs pero dentro de esta ventana cuenta como Miss
        /// (consume la nota). Más temprano que esto se ignora (no hay penalización por "tap fantasma").
        /// </summary>
        public float earlyMissMs;

        public static HitWindows Default => new HitWindows
        {
            perfectMs = 30f,
            goodMs = 70f,
            earlyMissMs = 120f
        };

        /// <param name="deltaMs">Input menos objetivo, en ms. Negativo = temprano, positivo = tarde.</param>
        public readonly HitType Judge(double deltaMs)
        {
            double abs = deltaMs < 0.0 ? -deltaMs : deltaMs;

            if (abs <= perfectMs) return HitType.Perfect;
            if (abs <= goodMs) return HitType.Good;
            if (deltaMs < 0.0 && abs <= earlyMissMs) return HitType.Miss;
            return HitType.None;
        }
    }
}
