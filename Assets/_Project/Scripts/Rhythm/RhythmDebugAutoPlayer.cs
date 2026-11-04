using System;
using UnityEngine;

namespace DiosPunk.Rhythm
{
    public enum AutoPlayMode
    {
        AlwaysPerfect = 0,
        AlwaysGood = 1,
        SimulateMisses = 2,
        HumanRandom = 3
    }

    /// <summary>
    /// Bot de prueba. Para cada nota activa planifica un delta (según el modo) y emite el input por
    /// RhythmInputReader.InjectLanePress con el timestamp EXACTO que, tras la conversión del SongClock,
    /// produce ese delta. Así se valida la lógica de HitWindows / spawn / eventos sin error humano.
    /// Corre antes que el controlador (execution order -50) para no perder notas en el borde de la ventana.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RhythmDebugAutoPlayer : MonoBehaviour
    {
        [SerializeField] private RhythmTrackController _track;
        [SerializeField] private RhythmInputReader _input;

        [SerializeField] private bool _enabled;
        [SerializeField] private AutoPlayMode _mode = AutoPlayMode.AlwaysPerfect;

        [Header("Simular Misses")]
        [SerializeField, Range(0f, 1f)] private float _missChance = 0.25f;

        [Header("Humano aleatorio (gaussiana)")]
        [SerializeField] private float _humanMeanMs = 0f;
        [SerializeField, Min(0f)] private float _humanSigmaMs = 35f;

        [Tooltip("Semilla del generador; 0 = aleatoria en cada ejecución.")]
        [SerializeField] private int _seed = 12345;

        private System.Random _rng;

        /// <summary>Se dispara al cambiar Enabled o Mode (para refrescar UI).</summary>
        public event Action StateChanged;

        public bool Enabled
        {
            get => _enabled;
            set { _enabled = value; StateChanged?.Invoke(); }
        }

        public AutoPlayMode Mode
        {
            get => _mode;
            set { _mode = value; StateChanged?.Invoke(); }
        }

        private void Awake()
        {
            _rng = _seed == 0 ? new System.Random() : new System.Random(_seed);
        }

        private void Update()
        {
            if (!_enabled || !_track.IsRunning) return;

            SongClock clock = _track.Clock;
            if (clock.IsPaused) return;

            // Un frame de adelanto: el input se emite antes de que el controlador evalúe los Miss de este frame.
            double now = clock.JudgementTime + Time.unscaledDeltaTime;

            for (int i = 0; i < _track.ActiveNoteCount; i++)
            {
                NoteView note = _track.GetActiveNote(i);
                if (note.AutoFired) continue;

                if (!note.AutoPlanned) Plan(note);
                if (note.AutoFired) continue; // el plan decidió no presionar

                double fireAt = note.TargetTime + note.AutoPlanMs * 0.001;
                if (now < fireAt) continue;

                note.AutoFired = true;
                _input.InjectLanePress(note.LaneIndex, clock.SongTimeToInputRealtime(fireAt));

                // El juicio pudo quitar la nota de la lista: reiniciar el recorrido (AutoFired evita repeticiones).
                i = -1;
            }
        }

        private void Plan(NoteView note)
        {
            note.AutoPlanned = true;
            HitWindows w = _track.Windows;

            switch (_mode)
            {
                case AutoPlayMode.AlwaysPerfect:
                    note.AutoPlanMs = 0f;
                    break;

                case AutoPlayMode.AlwaysGood:
                {
                    // Entre el borde de Perfect y el de Good, con margen, signo aleatorio.
                    float lo = w.perfectMs + 5f;
                    float hi = Mathf.Max(lo, w.goodMs - 5f);
                    float magnitude = lo + (hi - lo) * (float)_rng.NextDouble();
                    note.AutoPlanMs = _rng.Next(2) == 0 ? -magnitude : magnitude;
                    break;
                }

                case AutoPlayMode.SimulateMisses:
                    if (_rng.NextDouble() < _missChance)
                    {
                        if (_rng.Next(2) == 0)
                        {
                            note.AutoFired = true; // no presiona: Miss por nota que pasa la barra
                        }
                        else
                        {
                            // Presión temprana dentro de la ventana de Early Miss.
                            note.AutoPlanMs = -(w.goodMs + w.earlyMissMs) * 0.5f;
                        }
                    }
                    else
                    {
                        note.AutoPlanMs = 0f;
                    }
                    break;

                case AutoPlayMode.HumanRandom:
                {
                    float d = _humanMeanMs + _humanSigmaMs * NextGaussian();
                    if (d > w.goodMs) note.AutoFired = true; // demasiado tarde: la nota se pierde
                    else note.AutoPlanMs = d;
                    break;
                }
            }
        }

        /// <summary>Normal estándar por Box-Muller (sin allocations).</summary>
        private float NextGaussian()
        {
            double u1 = 1.0 - _rng.NextDouble(); // (0,1]
            double u2 = _rng.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
    }
}
