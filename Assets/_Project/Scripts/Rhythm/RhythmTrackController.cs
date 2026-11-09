using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Pista de 4 carriles: spawnea notas desde un ObjectPool según el SongClock, las desplaza hacia la
    /// barra de impacto, juzga los inputs (Perfect / Good / Miss) y notifica por eventos.
    /// El transform de este objeto es la raíz de la pista (las notas son hijas suyas).
    /// Sin allocations en Update: listas preasignadas, sin LINQ, sin lambdas ni boxing en el loop.
    /// </summary>
    public sealed class RhythmTrackController : MonoBehaviour
    {
        private static readonly Comparison<NoteData> s_byTime =
            (a, b) => a.targetTimeDSP.CompareTo(b.targetTimeDSP);

        [Header("Dependencias")]
        [SerializeField] private SongClock _clock;
        [SerializeField] private RhythmInputReader _input;
        [SerializeField] private SongData _song;
        [SerializeField] private NoteView _notePrefab;

        [Header("Geometría de pista (espacio local)")]
        [SerializeField] private float _hitLineY = -3.5f;
        [SerializeField] private float _spawnY = 6f;
        [SerializeField] private float _laneSpacing = 1.5f;
        [Tooltip("Velocidad de caída en unidades por segundo.")]
        [SerializeField, Min(0.1f)] private float _noteSpeed = 8f;

        [Header("Colores de carril (0 rojo, 1 amarillo, 2 verde, 3 azul)")]
        [SerializeField] private Color[] _laneColors =
        {
            new Color(0.92f, 0.20f, 0.20f, 1f),
            new Color(0.97f, 0.82f, 0.15f, 1f),
            new Color(0.20f, 0.72f, 0.30f, 1f),
            new Color(0.20f, 0.38f, 0.90f, 1f)
        };

        [Header("Juicio")]
        [SerializeField] private HitWindows _windows = HitWindows.Default;

        [Tooltip("Ventanas más permisivas mientras el Modo Punk está activo.")]
        [SerializeField] private HitWindows _punkWindows = new HitWindows { perfectMs = 45f, goodMs = 90f, earlyMissMs = 130f };

        [Header("Pool")]
        [SerializeField, Min(1)] private int _poolPrewarm = 32;
        [SerializeField, Min(1)] private int _poolMaxSize = 128;

        [SerializeField] private bool _playOnStart = true;

        [Header("Debug")]
        [SerializeField] private bool _drawWindowGizmos = true;

        private ObjectPool<NoteView> _pool;
        private NoteData[] _notes = Array.Empty<NoteData>();
        private readonly List<NoteView> _active = new List<NoteView>(64);
        private int _nextSpawn;
        private double _lookaheadSeconds;
        private double _goodWindowSeconds;
        private bool _running;
        private bool _permissive;
        private SongData _notesBuiltFor;

        /// <summary>Resultado de un juicio: tipo, carril y delta en ms (negativo = temprano, positivo = tarde).</summary>
        public event Action<HitType, int, float> NoteJudged;

        /// <summary>Se dispara cuando se spawnearon y resolvieron todas las notas.</summary>
        public event Action SongFinished;

        /// <summary>Se dispara al iniciar o reiniciar la canción (las estadísticas ya fueron reseteadas).</summary>
        public event Action SongStarted;

        /// <summary>Se dispara al pausar.</summary>
        public event Action SongPaused;

        /// <summary>Se dispara al detener la canción (salida, Game Over o resultados).</summary>
        public event Action SongStopped;

        public bool IsRunning => _running;
        public bool IsPaused => _clock.IsPaused;
        public SongClock Clock => _clock;
        /// <summary>Ventanas vigentes (las permisivas mientras el Modo Punk está activo).</summary>
        public HitWindows Windows => _permissive ? _punkWindows : _windows;
        public RhythmStats Stats { get; } = new RhythmStats();
        public int TotalNotes => _notes.Length;
        public int ActiveNoteCount => _active.Count;
        public NoteView GetActiveNote(int index) => _active[index];

        public Color GetLaneColor(int lane)
        {
            return _laneColors != null && lane >= 0 && lane < _laneColors.Length ? _laneColors[lane] : Color.white;
        }

        private void Awake()
        {
            BuildNoteList();

            _pool = new ObjectPool<NoteView>(
                CreateNote, OnGetNote, OnReleaseNote, OnDestroyNote,
                collectionCheck: false, defaultCapacity: _poolPrewarm, maxSize: _poolMaxSize);

            // Prewarm: evita instanciar durante el gameplay.
            var warm = new NoteView[_poolPrewarm];
            for (int i = 0; i < warm.Length; i++) warm[i] = _pool.Get();
            for (int i = 0; i < warm.Length; i++) _pool.Release(warm[i]);

            RecomputeDerived();
        }

        private void OnEnable()
        {
            _input.LanePressed += OnLanePressed;
        }

        private void OnDisable()
        {
            _input.LanePressed -= OnLanePressed;
        }

        private void Start()
        {
            if (_playOnStart) StartSong();
        }

        private void OnDestroy()
        {
            _pool?.Dispose();
        }

        private void OnValidate()
        {
            RecomputeDerived();
        }

        /// <summary>Cambia la canción (recalcula la lista de notas). Llamar antes de StartSong.</summary>
        public void LoadSong(SongData song)
        {
            // Reintentar la misma canción no reconstruye la lista (evita basura en cada reinicio).
            if (song == _notesBuiltFor && song != null) return;

            _song = song;
            BuildNoteList();
        }

        /// <summary>Activa o desactiva las ventanas de impacto permisivas (Modo Punk).</summary>
        public void SetPermissiveWindows(bool permissive)
        {
            if (_permissive == permissive) return;
            _permissive = permissive;
            RecomputeDerived();
        }

        public void StartSong()
        {
            ReleaseAllActive();
            _nextSpawn = 0;
            Stats.Reset();
            _running = true;
            _clock.Play(_song.AudioClip);
            SongStarted?.Invoke();
        }

        /// <summary>Reinicia la pista desde t = 0 (primer compás).</summary>
        public void RestartSong() => StartSong();

        public void StopSong()
        {
            _running = false;
            _clock.Stop();
            ReleaseAllActive();
            SongStopped?.Invoke();
        }

        public void PauseSong()
        {
            if (!_running || _clock.IsPaused) return;
            _clock.Pause();
            SongPaused?.Invoke();
        }

        public void ResumeSong()
        {
            if (!_running || !_clock.IsPaused) return;
            _clock.Resume();
        }

        public void TogglePause()
        {
            if (_clock.IsPaused) ResumeSong();
            else PauseSong();
        }

        private void Update()
        {
            if (!_running || _clock.IsPaused) return;

            // SongClock tiene execution order -100: SongTime/JudgementTime ya están refrescados.
            double songTime = _clock.SongTime;

            SpawnDueNotes(songTime);
            ProcessLateMisses(_clock.JudgementTime);

            // Un suscriptor (p. ej. Game Over) pudo detener la canción durante el juicio.
            if (!_running) return;

            UpdateNotePositions(songTime);

            if (_nextSpawn >= _notes.Length && _active.Count == 0)
            {
                _running = false;
                SongFinished?.Invoke();
            }
        }

        // ---------- Spawn / movimiento ----------

        private void SpawnDueNotes(double songTime)
        {
            while (_nextSpawn < _notes.Length &&
                   _notes[_nextSpawn].targetTimeDSP - songTime <= _lookaheadSeconds)
            {
                NoteData data = _notes[_nextSpawn];
                NoteView view = _pool.Get();
                view.Init(data.laneIndex, data.targetTimeDSP, data.isDebuffNote, GetLaneColor(data.laneIndex));
                PlaceNote(view, songTime);
                _active.Add(view);
                _nextSpawn++;
            }
        }

        private void UpdateNotePositions(double songTime)
        {
            for (int i = 0; i < _active.Count; i++) PlaceNote(_active[i], songTime);
        }

        private void PlaceNote(NoteView view, double songTime)
        {
            float x = (view.LaneIndex - (RhythmInputReader.LaneCount - 1) * 0.5f) * _laneSpacing;
            float y = _hitLineY + (float)((view.TargetTime - songTime) * _noteSpeed);
            view.SetPosition(x, y);
        }

        // ---------- Juicio ----------

        private void ProcessLateMisses(double judgementTime)
        {
            // _active está ordenada por tiempo objetivo: solo la cabeza puede haberse pasado de la ventana.
            while (_active.Count > 0)
            {
                NoteView head = _active[0];
                double late = judgementTime - head.TargetTime;
                if (late <= _goodWindowSeconds) break;

                _active.RemoveAt(0);
                int lane = head.LaneIndex;
                _pool.Release(head);
                Report(HitType.Miss, lane, (float)(late * 1000.0));
            }
        }

        private void OnLanePressed(int lane, double inputRealtime)
        {
            if (!_running || _clock.IsPaused) return;

            // Primera nota (la más próxima en el tiempo) de ese carril.
            int index = -1;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].LaneIndex == lane) { index = i; break; }
            }
            if (index < 0) return;

            NoteView view = _active[index];
            double deltaMs = (_clock.InputRealtimeToSongTime(inputRealtime) - view.TargetTime) * 1000.0;

            HitType result = Windows.Judge(deltaMs);
            if (result == HitType.None) return;

            _active.RemoveAt(index);
            _pool.Release(view);
            Report(result, lane, (float)deltaMs);
        }

        private void Report(HitType type, int lane, float deltaMs)
        {
            Stats.Record(type, deltaMs);
            NoteJudged?.Invoke(type, lane, deltaMs);
        }

        // ---------- Setup / pool ----------

        private void BuildNoteList()
        {
            NoteData[] source = _song != null ? _song.Notes : Array.Empty<NoteData>();
            var valid = new List<NoteData>(source.Length);

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].laneIndex < 0 || source[i].laneIndex >= RhythmInputReader.LaneCount)
                {
                    Debug.LogWarning($"[Rhythm] Nota {i} con lane {source[i].laneIndex} fuera de rango (0..3). Se ignora.", _song);
                    continue;
                }
                valid.Add(source[i]);
            }

            _notes = valid.ToArray();
            Array.Sort(_notes, s_byTime);
            _notesBuiltFor = _song;
        }

        private void RecomputeDerived()
        {
            _lookaheadSeconds = (_spawnY - _hitLineY) / Mathf.Max(0.01f, _noteSpeed);
            _goodWindowSeconds = Windows.goodMs * 0.001;
        }

        private void ReleaseAllActive()
        {
            for (int i = 0; i < _active.Count; i++) _pool.Release(_active[i]);
            _active.Clear();
        }

        private NoteView CreateNote() => Instantiate(_notePrefab, transform);
        private static void OnGetNote(NoteView view) => view.gameObject.SetActive(true);
        private static void OnReleaseNote(NoteView view) => view.gameObject.SetActive(false);
        private static void OnDestroyNote(NoteView view) { if (view != null) Destroy(view.gameObject); }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            float half = (RhythmInputReader.LaneCount - 1) * 0.5f;
            float halfWidth = half * _laneSpacing + _laneSpacing * 0.5f;

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // Carriles
            Gizmos.color = Color.cyan;
            for (int lane = 0; lane < RhythmInputReader.LaneCount; lane++)
            {
                float x = (lane - half) * _laneSpacing;
                Gizmos.DrawLine(new Vector3(x, _hitLineY, 0f), new Vector3(x, _spawnY, 0f));
            }

            // Barra de impacto
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(-halfWidth, _hitLineY, 0f), new Vector3(halfWidth, _hitLineY, 0f));

            if (_drawWindowGizmos)
            {
                // Distancia espacial = ms * 0.001 * velocidad. Arriba = nota temprana (aún no llegó a la barra).
                float perfectH = _windows.perfectMs * 0.001f * _noteSpeed;
                float goodH = _windows.goodMs * 0.001f * _noteSpeed;
                float earlyH = _windows.earlyMissMs * 0.001f * _noteSpeed;

                // Early Miss (solo por encima de Good)
                DrawBand(halfWidth, _hitLineY + (goodH + earlyH) * 0.5f, earlyH - goodH, new Color(1f, 0.15f, 0.15f, 0.20f));
                // Good (±goodMs)
                DrawBand(halfWidth, _hitLineY, goodH * 2f, new Color(1f, 0.9f, 0.1f, 0.22f));
                // Perfect (±perfectMs)
                DrawBand(halfWidth, _hitLineY, perfectH * 2f, new Color(0.1f, 1f, 0.2f, 0.35f));

                UnityEditor.Handles.color = Color.white;
                UnityEditor.Handles.Label(transform.TransformPoint(new Vector3(halfWidth + 0.2f, _hitLineY + perfectH, 0f)),
                    "Perfect ±" + _windows.perfectMs.ToString("0") + " ms");
                UnityEditor.Handles.Label(transform.TransformPoint(new Vector3(halfWidth + 0.2f, _hitLineY + goodH, 0f)),
                    "Good ±" + _windows.goodMs.ToString("0") + " ms");
                UnityEditor.Handles.Label(transform.TransformPoint(new Vector3(halfWidth + 0.2f, _hitLineY + earlyH, 0f)),
                    "Early Miss hasta -" + _windows.earlyMissMs.ToString("0") + " ms");
            }

            Gizmos.matrix = previous;
        }

        private static void DrawBand(float halfWidth, float centerY, float height, Color color)
        {
            if (height <= 0f) return;
            Gizmos.color = color;
            Gizmos.DrawCube(new Vector3(0f, centerY, 0f), new Vector3(halfWidth * 2f, height, 0.01f));
        }
#endif
    }
}
