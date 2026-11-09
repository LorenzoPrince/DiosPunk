namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Ajustes de runtime compartidos (no persistentes todavía). El panel de Opciones escribe aquí y los
    /// efectos (p. ej. RhythmDebuffVisuals) lo leen.
    /// </summary>
    public static class GameSettings
    {
        /// <summary>Accesibilidad: mitiga/anula la distorsión gráfica de los debuffs.</summary>
        public static bool ReduceVisualEffects;
    }
}
