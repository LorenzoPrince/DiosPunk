using System;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Modo Punk: barra dual de carga continua. Cada acierto suma una porción pequeña pero visible
    /// (Perfect +1 %, Good +0,5 %) y cada Miss suma una porción grande de rescate (+15 %), así la barra
    /// responde de inmediato tanto si el jugador toca bien como si se equivoca. Al llegar al 100 % se activa
    /// solo: otorga 1 escudo (absorbe el próximo debuff), las ventanas de impacto se vuelven más permisivas
    /// (lo aplica quien escuche Activated/Deactivated) y la barra se drena con el tiempo de canción hasta
    /// apagarse. Lógica pura, testeable en EditMode.
    /// Mientras está activo no se carga la barra. El tiempo avanza con Tick(songTime): al pausar la canción el
    /// tiempo se congela y el drenaje también.
    /// </summary>
    public sealed class PunkModeSystem
    {
        private const float FullTolerance = 0.0001f;

        private double _lastTime;
        private bool _hasLastTime;

        /// <summary>Carga de rescate por Miss (fracción: 0.15 = 15 %).</summary>
        public float MissCharge { get; private set; } = 0.15f;

        /// <summary>Carga por Perfect (0.01 = 1 %).</summary>
        public float PerfectCharge { get; private set; } = 0.01f;

        /// <summary>Carga por Good (0.005 = 0,5 %).</summary>
        public float GoodCharge { get; private set; } = 0.005f;

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

        public void Configure(float missCharge, float perfectCharge, float goodCharge, float activeSeconds)
        {
            MissCharge = missCharge;
            PerfectCharge = perfectCharge;
            GoodCharge = goodCharge;
            ActiveSeconds = Math.Max(0.1f, activeSeconds);
        }

        public void Reset()
        {
            if (IsActive) Deactivate(false);

            Bar = 0f;
            ShieldCharges = 0;
            UseCount = 0;
            _hasLastTime = false;
            BarChanged?.Invoke(Bar, false);
        }

        public void RegisterHit(HitType type)
        {
            if (IsActive) return; // activo: la barra solo se drena

            switch (type)
            {
                case HitType.Miss: AddCharge(MissCharge); break;
                case HitType.Perfect: AddCharge(PerfectCharge); break;
                case HitType.Good: AddCharge(GoodCharge); break;
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
            if (amount <= 0f) return;

            Bar += amount;

            // Tolerancia: 100 x 0.01f en float queda en 0.99999x y la barra no llegaría nunca al 100 %.
            if (Bar >= 1f - FullTolerance) Activate();
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
            BarChanged?.Invoke(0f, false);
        }
    }
}
