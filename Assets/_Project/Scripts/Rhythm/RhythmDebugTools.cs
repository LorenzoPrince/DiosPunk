using System;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Hotkeys de testeo, resumen en consola y monitoreo de GC.
    ///   F1 Auto-Play on/off · F2 reiniciar · F3 pausa/reanudar · F4 cambiar modo del bot ·
    ///   +/- (numpad o fila numérica) LatencyOffsetMs en pasos de 5 ms.
    /// Resumen en consola al terminar la canción y al pausar.
    /// GC: usa ProfilerRecorder("GC Allocated In Frame"). En el Editor puede incluir allocations del propio
    /// editor (Inspector/Game view); para la cifra definitiva usar el Profiler o un Development Build.
    /// </summary>
    public sealed class RhythmDebugTools : MonoBehaviour
    {
        private const int WarmupFrames = 120;
        private const float GcTextRefreshSeconds = 0.25f;

        [SerializeField] private RhythmTrackController _track;
        [SerializeField] private RhythmDebugAutoPlayer _autoPlayer;
        [Tooltip("Opcional: agrega puntaje, precisión y vida al resumen.")]
        [SerializeField] private RhythmSession _session;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private TMP_Text _gcText;
        [SerializeField, Min(1f)] private float _latencyStepMs = 5f;

        private InputAction[] _actions;
        private ProfilerRecorder _gcRecorder;
        private int _framesSinceReset;
        private int _skipFrames;
        private long _lastBytes;
        private long _maxBytes;
        private int _sampledFrames;
        private int _allocFrames;
        private float _nextGcTextTime;

        private void Awake()
        {
            _actions = new[]
            {
                CreateAction("<Keyboard>/f1", null, ToggleAutoPlay),
                CreateAction("<Keyboard>/f2", null, Restart),
                CreateAction("<Keyboard>/f3", null, TogglePause),
                CreateAction("<Keyboard>/f4", null, CycleMode),
                CreateAction("<Keyboard>/numpadPlus", "<Keyboard>/equals", () => AdjustLatency(_latencyStepMs)),
                CreateAction("<Keyboard>/numpadMinus", "<Keyboard>/minus", () => AdjustLatency(-_latencyStepMs)),
                CreateAction("<Keyboard>/f5", null, ForcePunk),
                CreateAction("<Keyboard>/f6", null, ForceDebuff),
            };
        }

        private static InputAction CreateAction(string bindingA, string bindingB, Action callback)
        {
            var action = new InputAction(type: InputActionType.Button);
            action.AddBinding(bindingA);
            if (bindingB != null) action.AddBinding(bindingB);
            action.performed += _ => callback();
            return action;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _actions.Length; i++) _actions[i].Enable();

            _track.SongFinished += OnSongFinished;
            _track.SongPaused += OnSongPaused;
            _track.SongStarted += OnSongStarted;
            _autoPlayer.StateChanged += RefreshStatus;

            _gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            ResetGcCounters();
            RefreshStatus();
        }

        private void OnDisable()
        {
            for (int i = 0; i < _actions.Length; i++) _actions[i].Disable();

            _track.SongFinished -= OnSongFinished;
            _track.SongPaused -= OnSongPaused;
            _track.SongStarted -= OnSongStarted;
            _autoPlayer.StateChanged -= RefreshStatus;

            if (_gcRecorder.Valid) _gcRecorder.Dispose();
        }

        private void OnDestroy()
        {
            if (_actions == null) return;
            for (int i = 0; i < _actions.Length; i++) _actions[i].Dispose();
        }

        // ---------- Hotkeys ----------

        private void ToggleAutoPlay() => _autoPlayer.Enabled = !_autoPlayer.Enabled;

        private void Restart()
        {
            if (_track.isActiveAndEnabled) _track.RestartSong();
        }

        private void TogglePause()
        {
            if (!_track.isActiveAndEnabled) return;
            _track.TogglePause();
            RefreshStatus();
        }

        private void CycleMode()
        {
            int count = Enum.GetValues(typeof(AutoPlayMode)).Length;
            _autoPlayer.Mode = (AutoPlayMode)(((int)_autoPlayer.Mode + 1) % count);
        }

        /// <summary>F5: llena la barra y activa el Modo Punk.</summary>
        private void ForcePunk()
        {
            if (_session != null && _track.IsRunning) _session.Punk.ForceActivate();
        }

        /// <summary>F6: dispara un debuff ahora (lo absorbe el escudo si el Modo Punk está activo).</summary>
        private void ForceDebuff()
        {
            if (_session != null && _track.IsRunning) _session.Debuffs.TriggerNow(_track.Clock.SongTime);
        }

        private void AdjustLatency(float deltaMs)
        {
            _track.Clock.LatencyOffsetMs += deltaMs;
            RefreshStatus();
        }

        // ---------- Eventos de la pista ----------

        private void OnSongStarted()
        {
            ResetGcCounters();
            RefreshStatus();
        }

        private void OnSongFinished() => LogSummary("Cancion finalizada");

        private void OnSongPaused()
        {
            LogSummary("Cancion en pausa");
            RefreshStatus();
        }

        private void LogSummary(string title)
        {
            string gc = _gcRecorder.Valid
                ? $"\n  GC Alloc/frame: ultimo {_lastBytes} B, max {_maxBytes} B, frames con alloc {_allocFrames}/{_sampledFrames}"
                : "\n  GC Alloc/frame: no disponible en este build";
            string game = _session == null ? string.Empty
                : $"\n  Puntaje {_session.Score.Score}, precision ponderada {_session.Score.AccuracyPercent:F1}%, vida {_session.Health.Current:F0}/{_session.Health.Max:F0}";
            Debug.Log(_track.Stats.BuildSummary(title, _track.TotalNotes) + game + gc);
            _skipFrames = 2; // el propio log y el string alocan: no contarlos
        }

        private void RefreshStatus()
        {
            if (_statusText == null) return;

            string auto = _autoPlayer.Enabled ? "ON (" + _autoPlayer.Mode + ")" : "OFF";
            string paused = _track.IsPaused ? "  [PAUSADO]" : string.Empty;
            _statusText.text =
                "AutoPlay: " + auto + "  [F1 on/off, F4 modo]\n" +
                "Latency offset: " + _track.Clock.LatencyOffsetMs.ToString("0") + " ms  [+/-]" + paused + "\n" +
                "[F2 reiniciar, F3 pausa, F5 Modo Punk, F6 debuff]";
            _skipFrames = 2;
        }

        // ---------- Monitoreo de GC ----------

        private void ResetGcCounters()
        {
            _framesSinceReset = 0;
            _skipFrames = 0;
            _lastBytes = 0;
            _maxBytes = 0;
            _sampledFrames = 0;
            _allocFrames = 0;
        }

        private void LateUpdate()
        {
            if (!_gcRecorder.Valid) return;

            _framesSinceReset++;
            if (_framesSinceReset <= WarmupFrames) return;

            if (_skipFrames > 0)
            {
                _skipFrames--;
                return;
            }

            long bytes = _gcRecorder.LastValue;
            _lastBytes = bytes;
            if (bytes > _maxBytes) _maxBytes = bytes;
            if (bytes > 0) _allocFrames++;
            _sampledFrames++;

            if (_gcText != null && Time.unscaledTime >= _nextGcTextTime)
            {
                _nextGcTextTime = Time.unscaledTime + GcTextRefreshSeconds;
                _gcText.SetText("GC Alloc/frame: last {0:0} B  max {1:0} B  frames>0: {2:0}/{3:0}",
                    _lastBytes, _maxBytes, _allocFrames, _sampledFrames);
            }
        }
    }
}
