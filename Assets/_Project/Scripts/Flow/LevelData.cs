using DiosPunk.Rhythm;
using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Datos de un nivel: títulos, dificultad, escena narrativa previa y canción.
    /// La canción NO se referencia directamente: así el menú no carga el audio. Se carga bajo demanda
    /// desde Resources/Songs/ (solo en la escena Gameplay) y se libera al salir.
    /// </summary>
    [CreateAssetMenu(menuName = "DiosPunk/Level Data", fileName = "Level")]
    public sealed class LevelData : ScriptableObject
    {
        public const string SongsResourceFolder = "Songs/";

        [SerializeField] private string _title = "Nivel";
        [SerializeField] private string _stage = "Escenario";
        [SerializeField] private string _difficulty = "Facil";
        [Tooltip("Nombre del SongData dentro de Assets/.../Resources/Songs/.")]
        [SerializeField] private string _songResource;
        [SerializeField] private DialogueData _dialogue;

        [Tooltip("Velocidad de caída de las notas de este nivel (1 = base). Permite una curva de dificultad suave.")]
        [SerializeField, Range(0.5f, 1.5f)] private float _noteSpeedMultiplier = 1f;

        [Header("Mecánicas del nivel (revelación progresiva)")]
        [Tooltip("false = sin debuffs: ritmo puro (nivel 1).")]
        [SerializeField] private bool _enableDebuffs = true;
        [Tooltip("false = el Modo Punk no carga (la barra se oculta).")]
        [SerializeField] private bool _enablePunkMode = true;
        [Tooltip("Segundo de canción del primer debuff (0 = igual al intervalo).")]
        [SerializeField, Min(0f)] private float _firstDebuffSeconds;
        [Tooltip("Segundos entre debuffs.")]
        [SerializeField, Min(5f)] private float _debuffIntervalSeconds = 25f;

        public bool EnableDebuffs => _enableDebuffs;
        public bool EnablePunkMode => _enablePunkMode;
        public float FirstDebuffSeconds => _firstDebuffSeconds;
        public float DebuffIntervalSeconds => _debuffIntervalSeconds;

        public float NoteSpeedMultiplier => _noteSpeedMultiplier;
        public string Title => _title;
        public string Stage => _stage;
        public string Difficulty => _difficulty;
        public string SongResource => _songResource;
        public DialogueData Dialogue => _dialogue;

        public SongData LoadSong()
        {
            return string.IsNullOrEmpty(_songResource)
                ? null
                : Resources.Load<SongData>(SongsResourceFolder + _songResource);
        }
    }
}
