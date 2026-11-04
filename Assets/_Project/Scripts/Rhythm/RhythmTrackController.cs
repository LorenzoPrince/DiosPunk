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

        [Header("Juicio")]
        [SerializeField] private HitWindows _windows = HitWindows.Default;

        [Header("Pool")]
        [SerializeField, Min(1)] private int _poolPrewarm = 32;
        [SerializeField, Min(1)] private int _poolMaxSize = 128;

        [SerializeField] private bool _playOnStart = true;

        private ObjectPool<NoteView> _pool;
        private NoteData[] _notes = Array.Empty<NoteData>();
        private readonly List<NoteView> _active = new List<NoteView>(64);
        private int _nextSpawn;
        private double _lookaheadSeconds;
        private double _goodWindowSeconds;
        private bool _running;

        /// <summary>Resultado de un juicio: tipo, carril y delta en ms (negativo = temprano, positivo = tarde).</summary>
        public event Action<HitType, int, float> NoteJudged;

        /// <summary>Se dispara cuando se spawnearon y resolvieron todas las notas.</summary>
        public event Action SongFinished;

        public bool IsRunning => _running;
        public SongClock Clock => _clock;

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

        public void StartSong()
        {
            ReleaseAllActive();
            _nextSpawn = 0;
            _running = true;
            _clock.Play(_song.AudioClip);
        }

        public void StopSong()
        {
            _running = false;
            _clock.Stop();
            ReleaseAllActive();
        }

        private void Update()
        {
            if (!_running) return;

            // SongClock tiene execution order -100: SongTime/JudgementTime ya están refrescados.
            double songTime = _clock.SongTime;

            SpawnDueNotes(songTime);
            ProcessLateMisses(_clock.JudgementTime);
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
                view.Init(data.laneIndex, data.targetTimeDSP, data.isDebuffNote);
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
                NoteJudged?.Invoke(HitType.Miss, lane, (float)(late * 1000.0));
            }
        }

        private void OnLanePressed(int lane, double inputRealtime)
        {
            if (!_running) return;

            // Primera nota (la más próxima en el tiempo) de ese carril.
            int index = -1;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].LaneIndex == lane) { index = i; break; }
            }
            if (index < 0) return;

            NoteView view = _active[index];
            double deltaMs = (_clock.InputRealtimeToSongTime(inputRealtime) - view.TargetTime) * 1000.0;

            HitType result = _windows.Judge(deltaMs);
            if (result == HitType.None) return;

            _active.RemoveAt(index);
            _pool.Release(view);
            NoteJudged?.Invoke(result, lane, (float)deltaMs);
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
        }

        private void RecomputeDerived()
        {
            _lookaheadSeconds = (_spawnY - _hitLineY) / Mathf.Max(0.01f, _noteSpeed);
            _goodWindowSeconds = _windows.goodMs * 0.001;
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
            Gizmos.color = Color.cyan;
            float half = (RhythmInputReader.LaneCount - 1) * 0.5f;
            for (int lane = 0; lane < RhythmInputReader.LaneCount; lane++)
            {
                float x = (lane - half) * _laneSpacing;
                Gizmos.DrawLine(transform.TransformPoint(new Vector3(x, _hitLineY, 0f)),
                                transform.TransformPoint(new Vector3(x, _spawnY, 0f)));
            }

            Gizmos.color = Color.yellow;
            float w = half * _laneSpacing + _laneSpacing * 0.5f;
            Gizmos.DrawLine(transform.TransformPoint(new Vector3(-w, _hitLineY, 0f)),
                            transform.TransformPoint(new Vector3(w, _hitLineY, 0f)));
        }
#endif
    }
}
