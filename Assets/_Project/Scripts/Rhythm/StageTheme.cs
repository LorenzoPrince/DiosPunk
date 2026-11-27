using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Arte y geometría de un escenario de nivel (fondo, pista en perspectiva, notas y receptáculos por carril, popups de
    /// juicio y panel de puntaje). Se carga por nombre desde Resources/Stages/ solo en la escena Gameplay (el menú no carga
    /// los sprites) y lo aplica StageThemeApplier. Un nivel sin tema conserva los visuales placeholder.
    ///
    /// Geometría: todos los sprites del arte usan PPU 100 y se escalan por artScale. Las notas caen en línea recta en
    /// pantalla de (laneBottomX, hitLineY) a (laneTopX, spawnY), achicándose hasta noteTopScale: perspectiva solo visual,
    /// el juicio sigue basándose únicamente en tiempos.
    /// </summary>
    [CreateAssetMenu(menuName = "DiosPunk/Stage Theme", fileName = "StageTheme")]
    public sealed class StageTheme : ScriptableObject
    {
        public const string ResourceFolder = "Stages/";

        [Header("Escenario")]
        [SerializeField] private Sprite _background;
        [SerializeField] private Sprite _highway;
        [Tooltip("Posición del centro del sprite de la pista (unidades de mundo, relativa a la pista).")]
        [SerializeField] private Vector2 _highwayPosition;

        [Header("Por carril (0 rojo, 1 amarillo, 2 verde, 3 azul)")]
        [SerializeField] private Sprite[] _notes = new Sprite[4];
        [SerializeField] private Sprite[] _receptacles = new Sprite[4];
        [Tooltip("X de mundo de cada carril en la línea de impacto.")]
        [SerializeField] private float[] _laneBottomX = new float[4];
        [Tooltip("X de mundo de cada carril donde nace la nota (extremo lejano de la pista).")]
        [SerializeField] private float[] _laneTopX = new float[4];

        [Header("Geometría de caída")]
        [Tooltip("Escala de los sprites del arte (PPU 100): unidades de mundo por pixel × 100.")]
        [SerializeField, Min(0.1f)] private float _artScale = 1.38f;
        [SerializeField] private float _hitLineY = -3.5f;
        [Tooltip("Y donde nace la nota: el extremo lejano de la pista dibujada.")]
        [SerializeField] private float _spawnY = 4.02f;
        [Tooltip("Velocidad de caída (u/s). Con la pista dibujada más corta se baja para conservar el tiempo de anticipación.")]
        [SerializeField, Min(0.1f)] private float _noteSpeed = 6.33f;
        [Tooltip("Escala de la nota en el extremo lejano (1 = tamaño en la línea de impacto).")]
        [SerializeField, Range(0.1f, 1f)] private float _noteTopScale = 0.22f;
        [Tooltip("Desplazamiento de los rótulos de teclas respecto de la línea de impacto (0 = centrados sobre el receptáculo).")]
        [SerializeField] private float _keyLabelOffsetY;

        [Header("Popups de juicio (2 variantes cada uno)")]
        [SerializeField] private Sprite[] _perfect = new Sprite[2];
        [SerializeField] private Sprite[] _good = new Sprite[2];
        [SerializeField] private Sprite[] _miss = new Sprite[2];

        [Header("HUD")]
        [SerializeField] private Sprite _scorePanel;

        public Sprite Background => _background;
        public Sprite Highway => _highway;
        public Vector2 HighwayPosition => _highwayPosition;
        public Sprite[] Notes => _notes;
        public Sprite[] Receptacles => _receptacles;
        public float[] LaneBottomX => _laneBottomX;
        public float[] LaneTopX => _laneTopX;
        public float ArtScale => _artScale;
        public float HitLineY => _hitLineY;
        public float SpawnY => _spawnY;
        public float NoteSpeed => _noteSpeed;
        public float NoteTopScale => _noteTopScale;
        public float KeyLabelOffsetY => _keyLabelOffsetY;
        public Sprite[] Perfect => _perfect;
        public Sprite[] Good => _good;
        public Sprite[] Miss => _miss;
        public Sprite ScorePanel => _scorePanel;
    }
}
