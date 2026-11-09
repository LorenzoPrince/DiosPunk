using System;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Modo Punk: barra dual. Carga rápida (salvavidas) con cada Miss y lenta (recompensa) con cada racha de
    /// N aciertos consecutivos. Al llegar al 100 % se activa solo: otorga 1 escudo (absorbe el próximo debuff),
    /// las ventanas de impacto se vuelven más permisivas (lo aplica quien escuche Activated/Deactivated) y la
    /// barra se drena con el tiempo de canción hasta apagarse. Lógica pura, testeable en EditMode.
    /// Mientras está activo no se carga la barra. El tiempo avanza con Tick(songTime): al pausar la canción el
    /// tiempo se congela y el drenaje también.
    /// </summary>
    public sealed class PunkModeSystem
    {
        private int _hitStreak;
        private double _lastTime;
        private bool _hasLastTime;

        public float MissCharge { get; private set; } = 0.15f;
        public float StreakCharge { get; private set; } = 0.05f;
        public int StreakSize { get; private set; } = 10;
        public float ActiveSeconds { get; private set; } = 9f;

        /// <summary>Barra normalizada 0..1.</summary>
        public float Bar { get; private set; }
        public bool IsActive { get; private set; }
        public int ShieldCharges { get; private set; }

        /// <summary>Veces que se activó en esta pasada.</summary>
        public int UseCount { get; private set; }

        /// <summary>bar (0..1), activo.</summary>
        public event Action<float, bool> BarChanged;

        public event Action Activated;

        /// <summary>true si terminó porque el escudo absorbió un debuff; false si se agotó el tiempo o se reinició.</summary>
        public event Action<bool> Deactivated;

        public void Configure(float missCharge, float streakCharge, int streakSize, float activeSeconds)
        {
            MissCharge = missCharge;
            StreakCharge = streakCharge;
            StreakSize = Math.Max(1, streakSize);
            ActiveSeconds = Math.Max(0.1f, activeSeconds);
        }

        public void Reset()
        {
            if (IsActive) Deactivate(false);

            Bar = 0f;
            ShieldCharges = 0;
            UseCount = 0;
            _hitStreak = 0;
            _hasLastTime = false;
            BarChanged?.Invoke(Bar, false);
        }

        public void RegisterHit(HitType type)
        {
            switch (type)
            {
                case HitType.Miss:
                    _hitStreak = 0;
                    if (!IsActive) AddCharge(MissCharge);
                    break;
                case HitType.Perfect:
                case HitType.Good:
                    _hitStreak++;
                    if (!IsActive && _hitStreak % StreakSize == 0) AddCharge(StreakCharge);
                    break;
            }
        }

        /// <summary>Avanza el drenaje con el tiempo de canción (segundos). Si el tiempo no avanza (pausa), no drena.</summary>
        public void Tick(double songTime)
        {
            if (!_hasLastTime)
            {
                _lastTime = songTime;
                _hasLastTime = true;
                return;
            }

            double dt = songTime - _lastTime;
            _lastTime = songTime;
            if (dt <= 0.0 || !IsActive) return;

            Bar -= (float)(dt / ActiveSeconds);
            if (Bar <= 0f) Deactivate(false);
            else BarChanged?.Invoke(Bar, true);
        }

        /// <summary>Lo llama el DebuffSystem: si el Modo Punk está activo con escudo, lo consume y devuelve true.</summary>
        public bool TryAbsorbDebuff()
        {
            if (!IsActive || ShieldCharges <= 0) return false;

            ShieldCharges--;
            Deactivate(true);
            return true;
        }

        /// <summary>Solo para pruebas: llena la barra y activa el modo.</summary>
        public void ForceActivate()
        {
            if (IsActive) return;
            Bar = 1f;
            Activate();
        }

        private void AddCharge(float amount)
        {
            Bar = Bar + amount > 1f ? 1f : Bar + amount;
            if (Bar >= 1f) Activate();
            else BarChanged?.Invoke(Bar, false);
        }

        private void Activate()
        {
            IsActive = true;
            Bar = 1f;
            ShieldCharges = 1;
            UseCount++;
            Activated?.Invoke();
            BarChanged?.Invoke(Bar, true);
        }

        private void Deactivate(bool shieldConsumed)
        {
            IsActive = false;
            ShieldCharges = 0;
            Bar = 0f;
            Deactivated?.Invoke(shieldConsumed);
            BarChanged?.Invoke(Bar, false);
        }
    }
}
