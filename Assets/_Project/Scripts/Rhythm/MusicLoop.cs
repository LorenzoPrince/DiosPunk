using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Música de fondo en loop para los menús y la narrativa (menú principal #35, pre-nivel #36). Un AudioSource propio
    /// creado en Awake; se maneja con Play / Stop / Pause / Resume (sin trabajo por frame). La música de la canción
    /// jugable NO pasa por aquí: la reproduce SongClock con PlayScheduled.
    /// </summary>
    public sealed class MusicLoop : MonoBehaviour
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;
        [SerializeField] private bool _playOnEnable;

        private AudioSource _source;

        public bool HasClip => _clip != null;
        public bool IsPlaying => _source != null && _source.isPlaying;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 0f;
            _source.volume = _volume;
            _source.clip = _clip;
        }

        private void OnEnable()
        {
            if (_playOnEnable) Play();
        }

        public void Play()
        {
            if (_clip == null || _source.isPlaying) return;
            _source.Play();
        }

        public void Stop()
        {
            if (_source != null) _source.Stop();
        }

        public void Pause()
        {
            if (_source != null) _source.Pause();
        }

        public void Resume()
        {
            if (_clip == null || _source == null) return;
            _source.UnPause();
            if (!_source.isPlaying) _source.Play();
        }
    }
}
