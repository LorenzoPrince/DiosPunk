using System;
using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Pegamento entre la pista y las reglas de juego: puntaje, vida, Modo Punk y debuffs. Escucha los eventos
    /// del RhythmTrackController y alimenta los sistemas puros; no conoce la UI (ver RhythmHudPresenter y
    /// RhythmDebuffVisuals). Los sistemas se crean en el inicializador de campo para que existan aunque otro
    /// componente se suscriba en OnEnable antes del Awake de este.
    /// </summary>
    public sealed class RhythmSession : MonoBehaviour
    {
        [SerializeField] private RhythmTrackController _track;

        [Header("Puntaje")]
        [SerializeField, Min(0)] private int _perfectPoints = 100;
        [SerializeField, Min(0)] private int _goodPoints = 50;
        [Tooltip("Aciertos seguidos necesarios para subir un escalón de multiplicador (x1 -> x2 -> x3 -> x4).")]
        [SerializeField, Min(1)] private int _comboPerMultiplierStep = 10;
        [SerializeField, Min(1)] private int _maxMultiplier = 4;

        [Header("Vida")]
        [SerializeField, Min(1f)] private float _maxHealth = 100f;
        [SerializeField, Min(0f)] private float _missDamage = 15f;
        [SerializeField, Min(0f)] private float _perfectHeal = 2f;
        [SerializeField, Min(0f)] private float _goodHeal = 1f;

        [Header("Modo Punk")]
        [Tooltip("Cuánto suma a la barra cada Miss (0.15 = 15 %).")]
        [SerializeField, Range(0f, 1f)] private float _punkMissCharge = 0.15f;
        [Tooltip("Cuánto suma cada racha de aciertos consecutivos (0.05 = 5 %).")]
        [SerializeField, Range(0f, 1f)] private float _punkStreakCharge = 0.05f;
        [SerializeField, Min(1)] private int _punkStreakSize = 10;
        [Tooltip("Segundos que tarda en vaciarse la barra una vez activo (8 a 10).")]
        [SerializeField, Range(1f, 30f)] private float _punkActiveSeconds = 9f;

        [Header("Debuffs")]
        [SerializeField, Min(1f)] private float _debuffInterval = 25f;
        [SerializeField, Min(0.5f)] private float _debuffDuration = 5f;

        public ScoreSystem Score { get; } = new ScoreSystem();
        public HealthSystem Health { get; } = new HealthSystem();
        public PunkModeSystem Punk { get; } = new PunkModeSystem();
        public DebuffSystem Debuffs { get; } = new DebuffSystem();

        public bool IsPaused => _track != null && _track.IsPaused;

        /// <summary>Señal para el Game Over: la vida llegó a 0.</summary>
        public event Action HealthDepleted;

        private void Awake()
        {
            Score.Configure(_perfectPoints, _goodPoints, _comboPerMultiplierStep, _maxMultiplier);
            Health.Configure(_maxHealth, _missDamage, _perfectHeal, _goodHeal);
            Punk.Configure(_punkMissCharge, _punkStreakCharge, _punkStreakSize, _punkActiveSeconds);
            Debuffs.Configure(_debuffInterval, _debuffDuration);
            Debuffs.SetAbsorber(Punk.TryAbsorbDebuff);
            ResetAll();
        }

        private void OnEnable()
        {
            _track.NoteJudged += OnNoteJudged;
            _track.SongStarted += ResetAll;
            _track.SongStopped += ResetEffects;
            Health.Depleted += OnDepleted;
            Punk.Activated += OnPunkActivated;
            Punk.Deactivated += OnPunkDeactivated;
        }

        private void OnDisable()
        {
            _track.NoteJudged -= OnNoteJudged;
            _track.SongStarted -= ResetAll;
            _track.SongStopped -= ResetEffects;
            Health.Depleted -= OnDepleted;
            Punk.Activated -= OnPunkActivated;
            Punk.Deactivated -= OnPunkDeactivated;
        }

        private void Update()
        {
            if (!_track.IsRunning || _track.IsPaused) return;

            // Tiempo de canción (dspTime): al pausar se congela y los sistemas no avanzan.
            double songTime = _track.Clock.SongTime;
            Punk.Tick(songTime);
            Debuffs.Tick(songTime);
        }

        private void ResetAll()
        {
            Score.Reset();
            Health.Reset();
            ResetEffects();
        }

        /// <summary>Al detener la canción se apagan los efectos (Modo Punk, debuffs); puntaje y vida quedan visibles hasta la próxima pasada.</summary>
        private void ResetEffects()
        {
            Punk.Reset();
            Debuffs.Reset();
            _track.SetPermissiveWindows(false);
        }

        private void OnNoteJudged(HitType type, int lane, float deltaMs)
        {
            Score.Register(type);
            Health.Apply(type);
            Punk.RegisterHit(type);
        }

        private void OnPunkActivated() => _track.SetPermissiveWindows(true);

        private void OnPunkDeactivated(bool shieldConsumed) => _track.SetPermissiveWindows(false);

        private void OnDepleted()
        {
            Debug.Log("[Rhythm] HealthDepleted: la vida llego a 0.");
            HealthDepleted?.Invoke();
        }
    }
}
