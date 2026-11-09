namespace DiosPunk.Rhythm
{
    /// <summary>Formatea tiempos como "mm:ss" sin generar garbage (escribe en un char[] preasignado).</summary>
    public static class TimeFormatter
    {
        /// <summary>Tamaño mínimo del buffer para "mm:ss / mm:ss".</summary>
        public const int BufferSize = 16;

        /// <summary>Escribe "mm:ss / mm:ss" (transcurrido / total) y devuelve la cantidad de caracteres.</summary>
        public static int FormatProgress(int elapsedSeconds, int totalSeconds, char[] buffer)
        {
            int i = WriteMinutesSeconds(elapsedSeconds, buffer, 0);
            buffer[i++] = ' ';
            buffer[i++] = '/';
            buffer[i++] = ' ';
            return WriteMinutesSeconds(totalSeconds, buffer, i);
        }

        private static int WriteMinutesSeconds(int totalSeconds, char[] buffer, int index)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            if (minutes > 99) minutes = 99;

            buffer[index++] = (char)('0' + minutes / 10);
            buffer[index++] = (char)('0' + minutes % 10);
            buffer[index++] = ':';
            buffer[index++] = (char)('0' + seconds / 10);
            buffer[index++] = (char)('0' + seconds % 10);
            return index;
        }
    }
}
