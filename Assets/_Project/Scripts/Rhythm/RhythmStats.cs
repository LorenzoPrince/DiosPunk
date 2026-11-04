using System;
using System.Text;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Métricas de una pasada: conteo de juicios, combo y delta (media y desvío estándar, algoritmo de
    /// Welford online: sin listas ni allocations). La media/desvío consideran solo Perfect y Good.
    /// </summary>
    public sealed class RhythmStats
    {
        private int _n;
        private double _mean;
        private double _m2;

        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }

        public int Judged => Perfect + Good + Miss;
        public double MeanDeltaMs => _n > 0 ? _mean : 0.0;
        public double StdDevDeltaMs => _n > 1 ? Math.Sqrt(_m2 / (_n - 1)) : 0.0;

        public void Reset()
        {
            _n = 0; _mean = 0.0; _m2 = 0.0;
            Perfect = Good = Miss = Combo = MaxCombo = 0;
        }

        public void Record(HitType type, float deltaMs)
        {
            switch (type)
            {
                case HitType.Perfect: Perfect++; AddDelta(deltaMs); Combo++; break;
                case HitType.Good: Good++; AddDelta(deltaMs); Combo++; break;
                case HitType.Miss: Miss++; Combo = 0; break;
                default: return;
            }
            if (Combo > MaxCombo) MaxCombo = Combo;
        }

        private void AddDelta(float deltaMs)
        {
            _n++;
            double d = deltaMs - _mean;
            _mean += d / _n;
            _m2 += d * (deltaMs - _mean);
        }

        /// <summary>Texto para consola. Aloca: usar solo en eventos (fin de canción / pausa), nunca por frame.</summary>
        public string BuildSummary(string title, int totalNotes)
        {
            var sb = new StringBuilder(256);
            sb.Append("[Rhythm] ").Append(title).Append('\n');
            sb.Append("  Notas totales: ").Append(totalNotes)
              .Append("  | Juzgadas: ").Append(Judged).Append('\n');
            sb.Append("  Perfect: ").Append(Perfect)
              .Append("  Good: ").Append(Good)
              .Append("  Miss: ").Append(Miss).Append('\n');
            sb.Append("  Combo max: ").Append(MaxCombo).Append('\n');
            sb.Append("  Delta (Perfect+Good): media ").Append(MeanDeltaMs.ToString("F2"))
              .Append(" ms, desvio std ").Append(StdDevDeltaMs.ToString("F2")).Append(" ms");
            return sb.ToString();
        }
    }
}
