using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>Colores de carril compartidos por la pista y el tutorial: 0 rojo, 1 amarillo, 2 verde, 3 azul.</summary>
    public static class LaneStyle
    {
        public static Color[] CreateDefaultColors()
        {
            return new[]
            {
                new Color(0.92f, 0.20f, 0.20f, 1f),
                new Color(0.97f, 0.82f, 0.15f, 1f),
                new Color(0.20f, 0.72f, 0.30f, 1f),
                new Color(0.20f, 0.38f, 0.90f, 1f)
            };
        }
    }
}
