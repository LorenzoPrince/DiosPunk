namespace DiosPunk.Flow
{
    /// <summary>
    /// Ajustes de runtime compartidos (no persistentes todavía). Los efectos de debuff deberán consultar
    /// ReduceVisualEffects cuando existan (Hito 3).
    /// </summary>
    public static class GameSettings
    {
        public static bool ReduceVisualEffects;
    }
}
