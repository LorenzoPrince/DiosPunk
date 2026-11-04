using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Reloj maestro de la canción. La fuente de verdad es AudioSettings.dspTime.
    ///
    /// dspTime avanza a saltos (una vez por buffer de audio) y los timestamps de input viven en la
    /// línea de tiempo de Time.realtimeSinceStartup. Para comparar ambos sin error de cuantización se
    /// estima el offset (dspTime - realtime) quedándonos con su máximo reciente: el valor observado
    /// de dspTime nunca está adelantado, solo retrasado, así que el máximo converge al offset real.
    /// Time.time / Time.deltaTime NO se usan para el timing rítmico.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(AudioSource))]
    public sealed class SongClock : MonoBehaviour
    {
        // Pérdida máxima por deriva entre el reloj de audio y el del sistema (s/s). Es chica a propósito.
        private const double OffsetDecayPerSecond = 0.0005;
        private const double OffsetResyncThreshold = 0.1;

        // Al reanudar, el audio se reprograma con este adelanto; SongTime se mantiene congelado hasta alcanzarlo.
        private const double ResumeDelaySeconds = 0.15;

        [SerializeField] private AudioSource _audioSource;

        [Tooltip("Segundos entre pedir Play y el primer sample del audio (lead-in para ver caer las primeras notas).")]
        [SerializeField, Min(0.1f)] private float _startDelaySeconds = 2f;

        [Tooltip("Calibración de latencia. Se resta al tiempo de los inputs. Valor sugerido = promedio de Delta mostrado en el HUD.")]
        [SerializeField, Range(-300f, 300f)] private float _latencyOffsetMs;

        private double _songStartDsp;
        private double _dspMinusRealtime;
        private double _lastRealtime;
        private bool _offsetInitialized;
        private bool _isPlaying;
        private bool _isPaused;
        private double _pausedSongTime;
        private bool _hasFloor;
        private double _floor;

        /// <summary>Segundos desde el primer sample del audio (negativo durante el lead-in). Se refresca en Update.</summary>
        public double SongTime { get; private set; }

        /// <summary>SongTime menos la calibración de latencia: el tiempo contra el que se juzga y se detectan Miss.</summary>
        public double JudgementTime { get; private set; }

        public bool IsPlaying => _isPlaying;
        public bool IsPaused => _isPaused;

        public float LatencyOffsetMs
        {
            get => _latencyOffsetMs;
            set => _latencyOffsetMs = Mathf.Clamp(value, -300f, 300f);
        }

        private void Reset()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        /// <summary>Programa el audio con PlayScheduled y arranca el reloj. clip puede ser null (reloj mudo).</summary>
        public void Play(AudioClip clip)
        {
            _audioSource.Stop();

            _isPaused = false;
            _hasFloor = false;
            _offsetInitialized = false;
            SampleOffset(Time.realtimeSinceStartupAsDouble);

            _songStartDsp = AudioSettings.dspTime + _startDelaySeconds;

            if (clip != null)
            {
                _audioSource.clip = clip;
                _audioSource.timeSamples = 0;
                _audioSource.PlayScheduled(_songStartDsp);
            }

            _isPlaying = true;
            RefreshTimes(Time.realtimeSinceStartupAsDouble);
        }

        public void Stop()
        {
            _isPlaying = false;
            _isPaused = false;
            _audioSource.Stop();
        }

        /// <summary>Congela SongTime y detiene el audio.</summary>
        public void Pause()
        {
            if (!_isPlaying || _isPaused) return;

            RefreshTimes(Time.realtimeSinceStartupAsDouble);
            _pausedSongTime = SongTime;
            _isPaused = true;
            _audioSource.Stop();
        }

        /// <summary>Reprograma el audio desde la posición congelada y continúa sin saltos de SongTime.</summary>
        public void Resume()
        {
            if (!_isPaused) return;

            double realtimeNow = Time.realtimeSinceStartupAsDouble;
            _offsetInitialized = false;
            SampleOffset(realtimeNow);

            double dspNow = AudioSettings.dspTime;
            _songStartDsp = dspNow + ResumeDelaySeconds - _pausedSongTime;

            AudioClip clip = _audioSource.clip;
            if (clip != null)
            {
                if (_pausedSongTime >= 0.0)
                {
                    if (_pausedSongTime < clip.length)
                    {
                        _audioSource.timeSamples = Mathf.Clamp((int)(_pausedSongTime * clip.frequency), 0, clip.samples - 1);
                        _audioSource.PlayScheduled(dspNow + ResumeDelaySeconds);
                    }
                }
                else
                {
                    _audioSource.timeSamples = 0;
                    _audioSource.PlayScheduled(_songStartDsp);
                }
            }

            _floor = _pausedSongTime;
            _hasFloor = true;
            _isPaused = false;
            RefreshTimes(realtimeNow);
        }

        /// <summary>
        /// Convierte un timestamp de input (InputAction.CallbackContext.time, línea de tiempo de
        /// Time.realtimeSinceStartup) a tiempo de canción, ya compensado por la calibración de latencia.
        /// </summary>
        public double InputRealtimeToSongTime(double inputRealtime)
        {
            return inputRealtime + _dspMinusRealtime - _songStartDsp - _latencyOffsetMs * 0.001;
        }

        /// <summary>
        /// Inversa exacta de InputRealtimeToSongTime: el timestamp de input que, tras la conversión,
        /// resulta en judgedSongTime. Lo usa el bot de depuración para emitir deltas exactos.
        /// </summary>
        public double SongTimeToInputRealtime(double judgedSongTime)
        {
            return judgedSongTime + _latencyOffsetMs * 0.001 + _songStartDsp - _dspMinusRealtime;
        }

        private void Update()
        {
            if (!_isPlaying || _isPaused) return;
            RefreshTimes(Time.realtimeSinceStartupAsDouble);
        }

        private void RefreshTimes(double realtimeNow)
        {
            SampleOffset(realtimeNow);
            double raw = realtimeNow + _dspMinusRealtime - _songStartDsp;

            if (_hasFloor)
            {
                if (raw < _floor) raw = _floor;
                else _hasFloor = false;
            }

            SongTime = raw;
            JudgementTime = raw - _latencyOffsetMs * 0.001;
        }

        private void SampleOffset(double realtimeNow)
        {
            double observed = AudioSettings.dspTime - realtimeNow;

            if (!_offsetInitialized)
            {
                _dspMinusRealtime = observed;
                _lastRealtime = realtimeNow;
                _offsetInitialized = true;
                return;
            }

            double dt = realtimeNow - _lastRealtime;
            _lastRealtime = realtimeNow;

            _dspMinusRealtime -= OffsetDecayPerSecond * dt;

            if (observed > _dspMinusRealtime || _dspMinusRealtime - observed > OffsetResyncThreshold)
            {
                _dspMinusRealtime = observed;
            }
        }
    }
}
