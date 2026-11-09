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
    ///
    /// Doble pista (GDD, Audio Pipeline): además de la mezcla base puede reproducir una segunda mezcla
    /// (guitarra distorsionada, "punkStem") en un segundo AudioSource, programada con PlayScheduled sobre el
    /// mismo dspTime que la base, así que ambas arrancan sample-accurate. El volumen del stem punk queda en 0
    /// salvo mientras el Modo Punk está activo: SetPunkStemActive hace un crossfade rápido entre ambas mezclas.
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

        [Tooltip("Segundo AudioSource para la mezcla del Modo Punk. Si falta se crea en Awake.")]
        [SerializeField] private AudioSource _punkSource;

        [Tooltip("Segundos entre pedir Play y el primer sample del audio (lead-in para ver caer las primeras notas).")]
        [SerializeField, Min(0.1f)] private float _startDelaySeconds = 2f;

        [Tooltip("Calibración de latencia. Se resta al tiempo de los inputs. Valor sugerido = promedio de Delta mostrado en el HUD.")]
        [SerializeField, Range(-300f, 300f)] private float _latencyOffsetMs;

        [Tooltip("Duración del crossfade entre la mezcla base y la mezcla Punk (segundos).")]
        [SerializeField, Range(0.02f, 2f)] private float _punkCrossfadeSeconds = 0.25f;

        private double _songStartDsp;
        private double _dspMinusRealtime;
        private double _lastRealtime;
        private bool _offsetInitialized;
        private bool _isPlaying;
        private bool _isPaused;
        private double _pausedSongTime;
        private bool _hasFloor;
        private double _floor;

        private bool _hasPunkStem;
        private float _punkBlend;
        private float _punkBlendTarget;

        /// <summary>Segundos desde el primer sample del audio (negativo durante el lead-in). Se refresca en Update.</summary>
        public double SongTime { get; private set; }

        /// <summary>SongTime menos la calibración de latencia: el tiempo contra el que se juzga y se detectan Miss.</summary>
        public double JudgementTime { get; private set; }

        public bool IsPlaying => _isPlaying;
        public bool IsPaused => _isPaused;

        /// <summary>true si la canción en curso tiene una segunda mezcla (stem del Modo Punk).</summary>
        public bool HasPunkStem => _hasPunkStem;

        /// <summary>Mezcla actual: 0 = solo base, 1 = solo stem punk.</summary>
        public float PunkBlend => _punkBlend;

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

            if (_punkSource == null || _punkSource == _audioSource)
            {
                _punkSource = null;
                AudioSource[] sources = GetComponents<AudioSource>();
                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] != _audioSource) { _punkSource = sources[i]; break; }
                }
                if (_punkSource == null) _punkSource = gameObject.AddComponent<AudioSource>();
            }
            _punkSource.playOnAwake = false;
            _punkSource.loop = false;
            _punkSource.spatialBlend = 0f;
            _punkSource.outputAudioMixerGroup = _audioSource.outputAudioMixerGroup;
            _punkSource.volume = 0f;

            // Latencia calibrada por el jugador (si la hay); el ajuste en vivo del debug sigue pudiendo modificarla.
            if (GameSettings.LatencyOffsetMs != 0f) LatencyOffsetMs = GameSettings.LatencyOffsetMs;
        }

        /// <summary>
        /// Programa el audio con PlayScheduled y arranca el reloj. clip puede ser null (reloj mudo).
        /// punkStem es opcional: si se pasa, suena en sincronía con clip pero con volumen 0 hasta SetPunkStemActive(true).
        /// </summary>
        public void Play(AudioClip clip, AudioClip punkStem = null)
        {
            _audioSource.Stop();
            _punkSource.Stop();

            _isPaused = false;
            _hasFloor = false;
            _offsetInitialized = false;
            SampleOffset(Time.realtimeSinceStartupAsDouble);

            _songStartDsp = AudioSettings.dspTime + _startDelaySeconds;

            _hasPunkStem = clip != null && punkStem != null;
            _punkBlend = 0f;
            _punkBlendTarget = 0f;
            ApplyMix();

            if (clip != null)
            {
                _audioSource.clip = clip;
                _audioSource.timeSamples = 0;
                _audioSource.PlayScheduled(_songStartDsp);
            }

            if (_hasPunkStem)
            {
                WarnIfStemMismatch(clip, punkStem);
                _punkSource.clip = punkStem;
                _punkSource.timeSamples = 0;
                _punkSource.PlayScheduled(_songStartDsp); // mismo dspTime que la base: arranque sample-accurate
            }
            else
            {
                _punkSource.clip = null;
            }

            _isPlaying = true;
            RefreshTimes(Time.realtimeSinceStartupAsDouble);
        }

        public void Stop()
        {
            _isPlaying = false;
            _isPaused = false;
            _audioSource.Stop();
            _punkSource.Stop();
            _punkBlend = 0f;
            _punkBlendTarget = 0f;
            ApplyMix();
        }

        /// <summary>Congela SongTime y detiene el audio.</summary>
        public void Pause()
        {
            if (!_isPlaying || _isPaused) return;

            RefreshTimes(Time.realtimeSinceStartupAsDouble);
            _pausedSongTime = SongTime;
            _isPaused = true;
            _audioSource.Stop();
            _punkSource.Stop();
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

            // Ambas pistas se reprograman desde la misma posición y con el mismo instante dsp.
            ScheduleResume(_audioSource, dspNow);
            if (_hasPunkStem) ScheduleResume(_punkSource, dspNow);

            _floor = _pausedSongTime;
            _hasFloor = true;
            _isPaused = false;
            RefreshTimes(realtimeNow);
        }

        private void ScheduleResume(AudioSource source, double dspNow)
        {
            AudioClip clip = source.clip;
            if (clip == null) return;

            if (_pausedSongTime >= 0.0)
            {
                if (_pausedSongTime < clip.length)
                {
                    source.timeSamples = Mathf.Clamp((int)(_pausedSongTime * clip.frequency), 0, clip.samples - 1);
                    source.PlayScheduled(dspNow + ResumeDelaySeconds);
                }
            }
            else
            {
                source.timeSamples = 0;
                source.PlayScheduled(_songStartDsp);
            }
        }

        /// <summary>
        /// Activa o desactiva la mezcla del Modo Punk: crossfade rápido hacia el stem distorsionado (true) o de
        /// vuelta a la mezcla base (false). Sin stem cargado no hace nada audible.
        /// </summary>
        public void SetPunkStemActive(bool active)
        {
            _punkBlendTarget = active ? 1f : 0f;
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

        /// <summary>
        /// Convierte un timestamp de input (línea de tiempo de Time.realtimeSinceStartup) a tiempo DSP absoluto
        /// (AudioSettings.dspTime), sin referirse a ninguna canción. Lo usa la calibración de latencia.
        /// </summary>
        public double InputRealtimeToDsp(double inputRealtime)
        {
            return inputRealtime + _dspMinusRealtime;
        }

        /// <summary>Refresca la estimación del offset dsp-realtime (útil antes de convertir si no hay canción sonando).</summary>
        public void SampleNow()
        {
            SampleOffset(Time.realtimeSinceStartupAsDouble);
        }

        /// <summary>Descarta la estimación actual del offset y la vuelve a tomar desde cero.</summary>
        public void ResetTimebase()
        {
            _offsetInitialized = false;
            SampleOffset(Time.realtimeSinceStartupAsDouble);
        }

        private void Update()
        {
            double realtime = Time.realtimeSinceStartupAsDouble;

            // Con canción en curso se refrescan los tiempos; sin ella (menús, calibración) solo se mantiene el offset.
            if (_isPlaying && !_isPaused) RefreshTimes(realtime);
            else SampleOffset(realtime);

            // Crossfade entre mezclas: solo trabaja mientras haya una transición en curso (cero garbage).
            if (_punkBlend != _punkBlendTarget)
            {
                _punkBlend = Mathf.MoveTowards(_punkBlend, _punkBlendTarget, Time.unscaledDeltaTime / _punkCrossfadeSeconds);
                ApplyMix();
            }
        }

        /// <summary>Con stem: base = 1 - mezcla, punk = mezcla (el stem punk vale 0 con el Modo Punk apagado). Sin stem: base a pleno.</summary>
        private void ApplyMix()
        {
            if (!_hasPunkStem)
            {
                _audioSource.volume = 1f;
                _punkSource.volume = 0f;
                return;
            }

            _audioSource.volume = 1f - _punkBlend;
            _punkSource.volume = _punkBlend;
        }

        private static void WarnIfStemMismatch(AudioClip baseClip, AudioClip stem)
        {
            // Solo ocurre con un asset mal exportado (camino de error, una vez por Play).
            if (baseClip.frequency != stem.frequency || Mathf.Abs(baseClip.length - stem.length) > 0.05f)
            {
                Debug.LogWarning("[Audio] El stem punk '" + stem.name + "' no coincide con la pista base '" + baseClip.name +
                                 "' (frecuencia o duracion distintas): se perdera la sincronia.");
            }
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
