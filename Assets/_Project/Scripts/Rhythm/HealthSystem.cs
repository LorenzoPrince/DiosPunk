using System;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Barra de vida. Miss resta, Perfect y Good curan, con tope en Max. Al llegar a 0 emite Depleted
    /// una sola vez por pasada (señal para el futuro flujo de Game Over). Lógica pura, testeable en EditMode.
    /// </summary>
    public sealed class HealthSystem
    {
        private bool _depletedRaised;

        public float Max { get; private set; } = 100f;
        public float Current { get; private set; } = 100f;
        public float MissDamage { get; private set; } = 10f;
        public float PerfectHeal { get; private set; } = 3f;
        public float GoodHeal { get; private set; } = 1.5f;

        /// <summary>Daño de una pulsación fuera de tiempo (juicio Bad): leve, para no castigar el casi-acierto.</summary>
        public float BadDamage { get; private set; } = 5f;

        public float Normalized => Max > 0f ? Current / Max : 0f;
        public bool IsDepleted => Current <= 0f;

        /// <summary>current, max.</summary>
        public event Action<float, float> Changed;

        /// <summary>Se emite al llegar a 0 HP (una vez hasta el próximo Reset).</summary>
        public event Action Depleted;

        public void Configure(float max, float missDamage, float perfectHeal, float goodHeal, float badDamage = 5f)
        {
            Max = max > 1f ? max : 1f;
            MissDamage = missDamage;
            BadDamage = badDamage;
            PerfectHeal = perfectHeal;
            GoodHeal = goodHeal;
        }

        public void Reset()
        {
            Current = Max;
            _depletedRaised = false;
            Changed?.Invoke(Current, Max);
        }

        public void Apply(HitType type)
        {
            float delta;
            switch (type)
            {
                case HitType.Perfect: delta = PerfectHeal; break;
                case HitType.Good: delta = GoodHeal; break;
                case HitType.Miss: delta = -MissDamage; break;
                case HitType.Bad: delta = -BadDamage; break;
                default: return;
            }

            float next = Current + delta;
            if (next > Max) next = Max;
            if (next < 0f) next = 0f;
            if (next == Current) return;

            Current = next;
            Changed?.Invoke(Current, Max);

            if (Current <= 0f && !_depletedRaised)
            {
                _depletedRaised = true;
                Depleted?.Invoke();
            }
        }
    }
}
