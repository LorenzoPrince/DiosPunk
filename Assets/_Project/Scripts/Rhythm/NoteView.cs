using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>Representación visual de una nota en pista. Reutilizada vía ObjectPool.</summary>
    public sealed class NoteView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;

        [Tooltip("Cuánto se oscurece el color del carril en las notas de debuff (0 = nada, 1 = negro).")]
        [SerializeField, Range(0f, 1f)] private float _debuffDarken = 0.5f;

        private Transform _transform;
        private Vector3 _baseScale = Vector3.one;
        private bool _hasArt;

        public int LaneIndex { get; private set; }
        public double TargetTime { get; private set; }

        // Estado exclusivo del bot de depuración (RhythmDebugAutoPlayer). Se resetea en cada Init.
        [System.NonSerialized] public bool AutoPlanned;
        [System.NonSerialized] public bool AutoFired;
        [System.NonSerialized] public float AutoPlanMs;

        private void Awake()
        {
            _transform = transform;
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        }

        public void Init(int laneIndex, double targetTime, bool isDebuffNote, Color laneColor)
        {
            LaneIndex = laneIndex;
            TargetTime = targetTime;
            AutoPlanned = false;
            AutoFired = false;
            AutoPlanMs = 0f;
            if (_renderer != null)
            {
                _renderer.color = isDebuffNote ? Color.Lerp(laneColor, Color.black, _debuffDarken) : laneColor;
            }
        }

        /// <summary>
        /// Arte definitivo de la nota: reemplaza el sprite placeholder. Con arte el color de carril no se aplica como tinte
        /// (el sprite ya viene coloreado); las notas de debuff solo se oscurecen.
        /// </summary>
        public void SetArt(Sprite sprite, float baseScale, bool isDebuffNote)
        {
            if (_renderer == null || sprite == null) return;
            _hasArt = true;
            _renderer.sprite = sprite;
            _baseScale = new Vector3(baseScale, baseScale, 1f);
            _renderer.color = isDebuffNote ? Color.Lerp(Color.white, Color.black, _debuffDarken) : Color.white;
        }

        public void SetPosition(float x, float y)
        {
            _transform.localPosition = new Vector3(x, y, 0f);
        }

        /// <summary>Posición con escala de perspectiva (solo con arte definitivo): la nota se achica hacia el fondo de la pista.</summary>
        public void SetPlacement(float x, float y, float perspectiveScale)
        {
            _transform.localPosition = new Vector3(x, y, 0f);
            _transform.localScale = _hasArt
                ? new Vector3(_baseScale.x * perspectiveScale, _baseScale.y * perspectiveScale, 1f)
                : new Vector3(perspectiveScale, perspectiveScale, 1f);
        }
    }
}
