using System;
using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Pegamento entre la pista y las reglas de puntaje/vida: escucha NoteJudged y SongStarted del
    /// RhythmTrackController y alimenta ScoreSystem y HealthSystem. No conoce la UI (ver RhythmHudPresenter).
    /// Los sistemas se crean en el inicializador de campo para que existan aunque otro componente
    /// se suscriba en OnEnable antes del Awake de este.
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

        public ScoreSystem Score { get; } = new ScoreSystem();
        public HealthSystem Health { get; } = new HealthSystem();

        /// <summary>Señal para el futuro Game Over: la vida llegó a 0.</summary>
        public event Action HealthDepleted;

        private void Awake()
        {
            Score.Configure(_perfectPoints, _goodPoints, _comboPerMultiplierStep, _maxMultiplier);
            Health.Configure(_maxHealth, _missDamage, _perfectHeal, _goodHeal);
            Score.Reset();
            Health.Reset();
        }

        private void OnEnable()
        {
            _track.NoteJudged += OnNoteJudged;
            _track.SongStarted += OnSongStarted;
            Health.Depleted += OnDepleted;
        }

        private void OnDisable()
        {
            _track.NoteJudged -= OnNoteJudged;
            _track.SongStarted -= OnSongStarted;
            Health.Depleted -= OnDepleted;
        }

        private void OnSongStarted()
        {
            Score.Reset();
            Health.Reset();
        }

        private void OnNoteJudged(HitType type, int lane, float deltaMs)
        {
            Score.Register(type);
            Health.Apply(type);
        }

        private void OnDepleted()
        {
            Debug.Log("[Rhythm] HealthDepleted: la vida llego a 0 (flujo de Game Over pendiente).");
            HealthDepleted?.Invoke();
        }
    }
}
