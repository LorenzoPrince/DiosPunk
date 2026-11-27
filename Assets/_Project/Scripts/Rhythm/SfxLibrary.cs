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
        PunkShout,
        // Audio definitivo (guía de audio del equipo). Se agregan al final para no alterar los valores existentes.
        Splash,
        UiImportant,
        PageTurn,
        Pen,
        LevelStart,
        ResultExcellent,
        ResultGood,
        CrowdShout,
        Cheer,
        Motto
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
        [Tooltip("Arengas / frases breves de Javier al llegar al 100 % (#10 a #16). Se elige una al azar, sin repetir seguidas.")]
        [SerializeField] private SfxEntry _punkShout = new SfxEntry();

        [Header("Flujo y narrativa (audio definitivo)")]
        [Tooltip("#34 Inicio del juego / splash.")]
        [SerializeField] private SfxEntry _splash = new SfxEntry();
        [Tooltip("#43 Opción importante / Comenzar.")]
        [SerializeField] private SfxEntry _uiImportant = new SfxEntry();
        [Tooltip("#49 a #52 Cambio de hoja (transiciones y avance de diálogo). Varias tomas, sin repetir seguidas.")]
        [SerializeField] private SfxEntry _pageTurn = new SfxEntry();
        [Tooltip("#44 a #48 Birome (mecanografiado de los diálogos pre-nivel).")]
        [SerializeField] private SfxEntry _pen = new SfxEntry();
        [Tooltip("#33 Inicio de nivel.")]
        [SerializeField] private SfxEntry _levelStart = new SfxEntry();
        [Tooltip("#31 Resultados excelentes (precisión >= 85 %).")]
        [SerializeField] private SfxEntry _resultExcellent = new SfxEntry();
        [Tooltip("#32 Resultados del muy bueno para abajo.")]
        [SerializeField] private SfxEntry _resultGood = new SfxEntry();

        [Header("Arengas en partida (voz de Javier)")]
        [Tooltip("#00 a #05 Gritos.")]
        [SerializeField] private SfxEntry _crowdShout = new SfxEntry();
        [Tooltip("#06 a #09 Festejos.")]
        [SerializeField] private SfxEntry _cheer = new SfxEntry();
        [Tooltip("#17 a #30 Lemas.")]
        [SerializeField] private SfxEntry _motto = new SfxEntry();

        public SfxEntry Get(SfxId id)
        {
            switch (id)
            {
                case SfxId.Splash: return _splash;
                case SfxId.UiImportant: return _uiImportant;
                case SfxId.PageTurn: return _pageTurn;
                case SfxId.Pen: return _pen;
                case SfxId.LevelStart: return _levelStart;
                case SfxId.ResultExcellent: return _resultExcellent;
                case SfxId.ResultGood: return _resultGood;
                case SfxId.CrowdShout: return _crowdShout;
                case SfxId.Cheer: return _cheer;
                case SfxId.Motto: return _motto;
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
