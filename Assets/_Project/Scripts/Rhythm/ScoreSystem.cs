using System;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Puntaje, combo, multiplicador y precisión ponderada. Lógica pura (sin MonoBehaviour), testeable en EditMode.
    /// Perfect y Good puntúan (puntos base × multiplicador vigente ANTES de sumar el combo del propio acierto);
    /// Miss no puntúa y resetea el combo. El multiplicador sube cada ComboPerStep aciertos hasta MaxMultiplier.
    /// Precisión ponderada = (Perfect × 1.0 + Good × 0.5) / juzgadas × 100.
    /// </summary>
    public sealed class ScoreSystem
    {
        private int _perfect, _good, _miss;

        public int PerfectPoints { get; private set; } = 100;
        public int GoodPoints { get; private set; } = 50;
        public int ComboPerStep { get; private set; } = 10;
        public int MaxMultiplier { get; private set; } = 4;

        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Multiplier => MultiplierFor(Combo);

        /// <summary>Precisión ponderada en % (100 si todavía no se juzgó nada).</summary>
        public float AccuracyPercent
        {
            get
            {
                int judged = _perfect + _good + _miss;
                return judged == 0 ? 100f : (_perfect + _good * 0.5f) / judged * 100f;
            }
        }

        /// <summary>score, multiplicador, combo.</summary>
        public event Action<int, int, int> Changed;

        public void Configure(int perfectPoints, int goodPoints, int comboPerStep, int maxMultiplier)
        {
            PerfectPoints = perfectPoints;
            GoodPoints = goodPoints;
            ComboPerStep = Math.Max(1, comboPerStep);
            MaxMultiplier = Math.Max(1, maxMultiplier);
        }

        public void Reset()
        {
            Score = Combo = MaxCombo = 0;
            _perfect = _good = _miss = 0;
            Changed?.Invoke(Score, Multiplier, Combo);
        }

        public void Register(HitType type)
        {
            switch (type)
            {
                case HitType.Perfect:
                    Score += PerfectPoints * Multiplier;
                    _perfect++;
                    Combo++;
                    break;
                case HitType.Good:
                    Score += GoodPoints * Multiplier;
                    _good++;
                    Combo++;
                    break;
                case HitType.Miss:
                    _miss++;
                    Combo = 0;
                    break;
                default:
                    return;
            }

            if (Combo > MaxCombo) MaxCombo = Combo;
            Changed?.Invoke(Score, Multiplier, Combo);
        }

        private int MultiplierFor(int combo)
        {
            int m = 1 + combo / ComboPerStep;
            return m > MaxMultiplier ? MaxMultiplier : m;
        }
    }
}
