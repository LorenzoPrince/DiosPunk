using System;
using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>Identificadores de efectos de sonido. Los usa SoundEffectManager.Play(id).</summary>
    public enum SfxId
    {
        UiClick = 0,
        UiHover,
        HitPerfect,
        HitGood,
        Miss,
        NpcThrow,
        TomatoImpact,
        ShieldBreak,
        PunkActivate,
        PunkShout
    }

    /// <summary>
    /// Un efecto: una o varias variantes (se elige una al azar), volumen base y variación de tono.
    /// El área de sonido reemplaza los clips placeholder por los definitivos (.wav/.ogg) sin tocar código.
    /// </summary>
    [Serializable]
    public sealed class SfxEntry
    {
        [Tooltip("Variantes del efecto. Si hay más de una se elige una al azar en cada reproducción.")]
        public AudioClip[] clips = Array.Empty<AudioClip>();

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Rango de pitch aleatorio (1 = sin cambio). Evita que los sonidos repetidos suenen idénticos.")]
        [Range(0.5f, 2f)] public float pitchMin = 1f;
        [Range(0.5f, 2f)] public float pitchMax = 1f;

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// Biblioteca de SFX del juego (GDD, Audio Pipeline). Se carga desde Resources/Audio/SfxLibrary.
    /// Los clips iniciales son placeholders procedurales generados por el builder de escenas; los campos
    /// están expuestos por categoría para que el equipo de audio asigne los definitivos.
    /// </summary>
    [CreateAssetMenu(menuName = "DiosPunk/Sfx Library", fileName = "SfxLibrary")]
    public sealed class SfxLibrary : ScriptableObject
    {
        public const string ResourcePath = "Audio/SfxLibrary";

        [Header("UI (papel / fanzine)")]
        [SerializeField] private SfxEntry _uiClick = new SfxEntry();
        [SerializeField] private SfxEntry _uiHover = new SfxEntry();

        [Header("Juicio de notas")]
        [SerializeField] private SfxEntry _hitPerfect = new SfxEntry();
        [SerializeField] private SfxEntry _hitGood = new SfxEntry();
        [Tooltip("Error / corte al fallar una nota.")]
        [SerializeField] private SfxEntry _miss = new SfxEntry();

        [Header("Proyectiles y debuffs")]
        [Tooltip("Lanzamiento del tomate por el NPC saboteador.")]
        [SerializeField] private SfxEntry _npcThrow = new SfxEntry();
        [Tooltip("Impacto del tomatazo contra la pantalla.")]
        [SerializeField] private SfxEntry _tomatoImpact = new SfxEntry();
        [Tooltip("Metálico / rotura al absorber el debuff con el Escudo Punk.")]
        [SerializeField] private SfxEntry _shieldBreak = new SfxEntry();

        [Header("Modo Punk")]
        [Tooltip("Efecto de activación (acorde distorsionado, golpe de platillo, etc.).")]
        [SerializeField] private SfxEntry _punkActivate = new SfxEntry();
        [Tooltip("Arengas / frases breves de Javier al llegar al 100 %. Se elige una al azar.")]
        [SerializeField] private SfxEntry _punkShout = new SfxEntry();

        public SfxEntry Get(SfxId id)
        {
            switch (id)
            {
                case SfxId.UiClick: return _uiClick;
                case SfxId.UiHover: return _uiHover;
                case SfxId.HitPerfect: return _hitPerfect;
                case SfxId.HitGood: return _hitGood;
                case SfxId.Miss: return _miss;
                case SfxId.NpcThrow: return _npcThrow;
                case SfxId.TomatoImpact: return _tomatoImpact;
                case SfxId.ShieldBreak: return _shieldBreak;
                case SfxId.PunkActivate: return _punkActivate;
                case SfxId.PunkShout: return _punkShout;
                default: return null;
            }
        }
    }
}
