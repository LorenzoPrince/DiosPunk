using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Servicio global de efectos de sonido. Vive en un GameObject DontDestroyOnLoad creado al arrancar el juego
    /// (antes de cargar la primera escena), con un pool fijo de AudioSource: cada Play toma una voz libre (o la
    /// más antigua) sin instanciar nada ni generar garbage. El volumen sale de GameSettings.SfxVolume, que
    /// controla el slider "Volumen SFX" de Opciones, y es independiente de la música.
    /// Uso: SoundEffectManager.Play(SfxId.UiClick).
    /// </summary>
    public sealed class SoundEffectManager : MonoBehaviour
    {
        private const int VoiceCount = 12;

        private static SoundEffectManager s_instance;

        private AudioSource[] _voices;
        private SfxEntry[] _entries;
        private int _next;

        /// <summary>true si el servicio está listo (hay biblioteca cargada).</summary>
        public static bool IsReady => s_instance != null && s_instance._entries != null;

        /// <summary>Reproduce un efecto. volumeScale multiplica el volumen; delaySeconds lo retrasa (PlayDelayed).</summary>
        public static void Play(SfxId id, float volumeScale = 1f, float delaySeconds = 0f)
        {
            if (s_instance != null) s_instance.PlayInternal(id, volumeScale, delaySeconds);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (s_instance != null) return;

            var library = Resources.Load<SfxLibrary>(SfxLibrary.ResourcePath);
            if (library == null)
            {
                Debug.LogWarning("[Audio] No se encontro Resources/" + SfxLibrary.ResourcePath + ". Los SFX quedan en silencio.");
                return;
            }

            var go = new GameObject("SoundEffectManager");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<SoundEffectManager>();
            s_instance.Init(library);
        }

        private void Init(SfxLibrary library)
        {
            // Tabla indexada por SfxId: el lookup en juego es un acceso a array.
            int count = System.Enum.GetValues(typeof(SfxId)).Length;
            _entries = new SfxEntry[count];
            for (int i = 0; i < count; i++) _entries[i] = library.Get((SfxId)i);

            _voices = new AudioSource[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                _voices[i] = source;
            }
        }

        private void PlayInternal(SfxId id, float volumeScale, float delaySeconds)
        {
            float master = GameSettings.SfxVolume;
            if (master <= 0f) return;

            SfxEntry entry = _entries[(int)id];
            if (entry == null || !entry.HasClips) return;

            AudioClip[] clips = entry.clips;
            AudioClip clip = clips[clips.Length == 1 ? 0 : Random.Range(0, clips.Length)];
            if (clip == null) return;

            AudioSource voice = AcquireVoice();
            voice.clip = clip;
            voice.volume = Mathf.Clamp01(entry.volume * volumeScale * master);
            voice.pitch = entry.pitchMin == entry.pitchMax ? entry.pitchMin : Random.Range(entry.pitchMin, entry.pitchMax);

            if (delaySeconds > 0f) voice.PlayDelayed(delaySeconds);
            else voice.Play();
        }

        /// <summary>Primera voz libre a partir del cursor; si todas suenan, roba la más antigua (round-robin).</summary>
        private AudioSource AcquireVoice()
        {
            for (int i = 0; i < _voices.Length; i++)
            {
                int index = (_next + i) % _voices.Length;
                if (!_voices[index].isPlaying)
                {
                    _next = (index + 1) % _voices.Length;
                    return _voices[index];
                }
            }

            AudioSource stolen = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            stolen.Stop();
            return stolen;
        }
    }
}
