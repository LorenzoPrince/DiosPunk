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

        public void SetPosition(float x, float y)
        {
            _transform.localPosition = new Vector3(x, y, 0f);
        }
    }
}
