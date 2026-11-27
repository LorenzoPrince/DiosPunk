using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Música de fondo en loop para los menús y la narrativa (menú principal #35, pre-nivel #36). Un AudioSource propio
    /// creado en Awake. Play / Pause / Resume / Stop hacen una rampa de volumen (fade-in o fade-out, por defecto 0,4 s)
    /// en vez de cortar en seco: Pause y Stop recién detienen el audio cuando el fade llega a 0. La rampa corre en
    /// Update con tiempo sin escalar, solo mientras hay un fade en curso, y no genera garbage.
    /// La música de la canción jugable NO pasa por aquí: la reproduce SongClock con PlayScheduled.
    /// </summary>
    public sealed class MusicLoop : MonoBehaviour
    {
        private enum EndAction { None, Pause, Stop }

        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;
        [Tooltip("Duración de cada rampa de fade-in / fade-out (segundos).")]
        [SerializeField, Range(0.05f, 3f)] private float _fadeSeconds = 0.4f;
        [SerializeField] private bool _playOnEnable;

        private AudioSource _source;
        private float _gain;          // 0..1 actual de la rampa
        private float _targetGain;    // 0..1 al que se dirige
        private EndAction _onSilent;  // qué hacer al llegar a 0
        private bool _paused;

        public bool HasClip => _clip != null;
        public bool IsPlaying => _source != null && _source.isPlaying;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = 0f;
            _source.clip = _clip;
        }

        private void OnEnable()
        {
            if (_playOnEnable) Play();
        }

        /// <summary>Arranca con fade-in. Si estaba pausada o bajando por un Stop, vuelve a subir sin reiniciar.</summary>
        public void Play()
        {
            if (_clip == null) return;

            _onSilent = EndAction.None;
            _targetGain = 1f;

            if (_paused) { _source.UnPause(); _paused = false; }
            if (!_source.isPlaying)
            {
                _gain = 0f;
                _source.volume = 0f;
                _source.Play();
            }
        }

        /// <summary>Baja con fade-out y detiene la música al llegar a 0.</summary>
        public void Stop()
        {
            if (_source == null || !_source.isPlaying) return;
            _targetGain = 0f;
            _onSilent = EndAction.Stop;
        }

        /// <summary>Baja con fade-out y pausa (conserva la posición) al llegar a 0.</summary>
        public void Pause()
        {
            if (_source == null || !_source.isPlaying) return;
            _targetGain = 0f;
            _onSilent = EndAction.Pause;
        }

        /// <summary>Retoma con fade-in desde donde se pausó (o arranca si no sonaba).</summary>
        public void Resume() => Play();

        private void Update()
        {
            if (_gain == _targetGain) return; // sin fade en curso: no hace nada

            _gain = Mathf.MoveTowards(_gain, _targetGain, Time.unscaledDeltaTime / _fadeSeconds);
            _source.volume = _volume * _gain;

            if (_gain <= 0f && _targetGain <= 0f)
            {
                if (_onSilent == EndAction.Pause) { _source.Pause(); _paused = true; }
                else if (_onSilent == EndAction.Stop) _source.Stop();
                _onSilent = EndAction.None;
            }
        }
    }
}
