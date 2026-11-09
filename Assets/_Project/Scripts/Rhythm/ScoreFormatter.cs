using System;

namespace DiosPunk.Rhythm
{
    /// <summary>Formatea enteros con separador de miles "." (ej. 293000 -> "293.000") sin generar garbage.</summary>
    public static class ScoreFormatter
    {
        public const int BufferSize = 16;

        /// <summary>Escribe el número al inicio de buffer (mín. BufferSize) y devuelve la cantidad de caracteres.</summary>
        public static int Format(int value, char[] buffer)
        {
            int pos = buffer.Length;
            if (value <= 0)
            {
                buffer[--pos] = '0';
            }
            else
            {
                int group = 0;
                while (value > 0)
                {
                    if (group == 3)
                    {
                        buffer[--pos] = '.';
                        group = 0;
                    }
                    buffer[--pos] = (char)('0' + value % 10);
                    value /= 10;
                    group++;
                }
            }

            int length = buffer.Length - pos;
            Array.Copy(buffer, pos, buffer, 0, length);
            return length;
        }
    }
}
