using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>Representación visual de una nota en pista. Reutilizada vía ObjectPool.</summary>
    public sealed class NoteView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _debuffColor = new Color(1f, 0.15f, 0.15f, 1f);

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

        public void Init(int laneIndex, double targetTime, bool isDebuffNote)
        {
            LaneIndex = laneIndex;
            TargetTime = targetTime;
            AutoPlanned = false;
            AutoFired = false;
            AutoPlanMs = 0f;
            if (_renderer != null) _renderer.color = isDebuffNote ? _debuffColor : _normalColor;
        }

        public void SetPosition(float x, float y)
        {
            _transform.localPosition = new Vector3(x, y, 0f);
        }
    }
}
