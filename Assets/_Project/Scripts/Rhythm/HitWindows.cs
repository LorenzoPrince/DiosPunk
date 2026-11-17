using System;
using UnityEngine.Serialization;

namespace DiosPunk.Rhythm
{
    public enum HitType : byte
    {
        /// <summary>El input no corresponde a ninguna nota (se ignora).</summary>
        None = 0,
        Perfect = 1,
        Good = 2,
        /// <summary>Nota que pasó de largo sin pulsarse: fallo completo.</summary>
        Miss = 3,
        /// <summary>Pulsación fuera de tiempo pero cerca (entre Good y Bad): rompe el combo pero casi no resta vida.</summary>
        Bad = 4
    }

    /// <summary>
    /// Ventanas de juicio en milisegundos (simétricas). Lógica pura, sin dependencias de MonoBehaviour (testeable en EditMode).
    /// |delta| ≤ perfectMs → Perfect; ≤ goodMs → Good; ≤ badMs → Bad (temprano o tarde); más allá no hay input válido
    /// y una nota que pasa de badMs sin pulsarse es Miss.
    /// </summary>
    [Serializable]
    public struct HitWindows
    {
        public float perfectMs;
        public float goodMs;

        /// <summary>
        /// Borde exterior de la ventana "Bad": pulsar entre goodMs y badMs consume la nota con un juicio Bad
        /// (penalización leve). Un input más temprano se ignora (sin castigo por "tap fantasma") y una nota
        /// sin pulsar se declara Miss cuando pasa de badMs.
        /// </summary>
        [FormerlySerializedAs("earlyMissMs")] public float badMs;

        /// <summary>Estándar: Perfect ±55, Good ±115, Bad ±155.</summary>
        public static HitWindows Default => new HitWindows
        {
            perfectMs = 55f,
            goodMs = 115f,
            badMs = 155f
        };

        /// <summary>Con Modo Punk activo: Perfect ±75, Good ±145, Bad ±185.</summary>
        public static HitWindows Punk => new HitWindows
        {
            perfectMs = 75f,
            goodMs = 145f,
            badMs = 185f
        };

        /// <param name="deltaMs">Input menos objetivo, en ms. Negativo = temprano, positivo = tarde.</param>
        public readonly HitType Judge(double deltaMs)
        {
            double abs = deltaMs < 0.0 ? -deltaMs : deltaMs;

            if (abs <= perfectMs) return HitType.Perfect;
            if (abs <= goodMs) return HitType.Good;
            if (abs <= badMs) return HitType.Bad;
            return HitType.None;
        }
    }
}
