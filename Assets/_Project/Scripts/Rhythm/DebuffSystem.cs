using System;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Debuffs periódicos: cada Interval segundos de canción (por defecto 25) se dispara uno. Si el absorbedor
    /// (el escudo del Modo Punk) lo absorbe, la pista sigue normal; si no, el debuff dura Duration segundos
    /// (por defecto 5). Lógica pura, testeable en EditMode; usa tiempo de canción, así que pausa y reinicio
    /// se comportan solos (el tiempo se congela / vuelve a empezar).
    /// </summary>
    public sealed class DebuffSystem
    {
        private double _nextTrigger = 25.0;
        private double _endTime;
        private Func<bool> _absorber;

        public float Interval { get; private set; } = 25f;
        public float Duration { get; private set; } = 5f;
        public bool IsActive { get; private set; }

        /// <summary>duración en segundos.</summary>
        public event Action<float> Started;
        public event Action Ended;
        public event Action Absorbed;

        public void Configure(float interval, float duration)
        {
            Interval = Math.Max(0.1f, interval);
            Duration = Math.Max(0.1f, duration);
            _nextTrigger = Interval;
        }

        /// <summary>Función que intenta absorber el debuff (devuelve true si lo absorbió y consumió la protección).</summary>
        public void SetAbsorber(Func<bool> absorber) => _absorber = absorber;

        public void Reset()
        {
            if (IsActive)
            {
                IsActive = false;
                Ended?.Invoke();
            }
            _nextTrigger = Interval;
            _endTime = 0.0;
        }

        public void Tick(double songTime)
        {
            if (IsActive && songTime >= _endTime)
            {
                IsActive = false;
                Ended?.Invoke();
            }

            if (songTime >= _nextTrigger)
            {
                Trigger(songTime);
                while (_nextTrigger <= songTime) _nextTrigger += Interval;
            }
        }

        /// <summary>Solo para pruebas: dispara un debuff ahora (respeta la absorción del escudo).</summary>
        public void TriggerNow(double songTime) => Trigger(songTime);

        private void Trigger(double songTime)
        {
            if (_absorber != null && _absorber())
            {
                Absorbed?.Invoke();
                return;
            }

            _endTime = songTime + Duration;
            if (!IsActive)
            {
                IsActive = true;
                Started?.Invoke(Duration);
            }
        }
    }
}
