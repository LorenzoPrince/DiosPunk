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

        private bool _incomingFired;

        public float Interval { get; private set; } = 25f;
        public float Duration { get; private set; } = 5f;

        /// <summary>Segundos de aviso previo al impacto (el NPC arroja el proyectil en ese tiempo).</summary>
        public float Lead { get; private set; } = 1.5f;
        public bool IsActive { get; private set; }

        /// <summary>Se avisa Lead segundos antes del disparo; el argumento es esa anticipación en segundos.</summary>
        public event Action<float> Incoming;

        /// <summary>duración en segundos.</summary>
        public event Action<float> Started;
        public event Action Ended;
        public event Action Absorbed;

        public void Configure(float interval, float duration, float lead = 1.5f)
        {
            Interval = Math.Max(0.1f, interval);
            Duration = Math.Max(0.1f, duration);
            Lead = Math.Clamp(lead, 0.1f, Interval * 0.5f);
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
            _incomingFired = false;
        }

        public void Tick(double songTime)
        {
            if (IsActive && songTime >= _endTime)
            {
                IsActive = false;
                Ended?.Invoke();
            }

            if (!_incomingFired && songTime >= _nextTrigger - Lead)
            {
                _incomingFired = true;
                Incoming?.Invoke(Lead);
            }

            if (songTime >= _nextTrigger)
            {
                Trigger(songTime);
                while (_nextTrigger <= songTime) _nextTrigger += Interval;
                _incomingFired = false;
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
