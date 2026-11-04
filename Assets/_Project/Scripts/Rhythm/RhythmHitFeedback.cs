using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Feedback visual por carril sobre la HitBar: al presionar una tecla se enciende el "pad" del carril
    /// (blanco tenue) y al juzgarse se tiñe: Verde = Perfect, Amarillo = Good, Rojo = Miss. Se desvanece solo.
    /// El fade es puramente visual (Time.unscaledDeltaTime); no interviene en el timing rítmico.
    /// </summary>
    public sealed class RhythmHitFeedback : MonoBehaviour
    {
        [SerializeField] private RhythmTrackController _track;
        [SerializeField] private RhythmInputReader _input;
        [Tooltip("Un SpriteRenderer por carril (índice = lane).")]
        [SerializeField] private SpriteRenderer[] _lanePads = new SpriteRenderer[RhythmInputReader.LaneCount];

        [SerializeField] private Color _perfectColor = new Color(0.1f, 1f, 0.25f, 0.9f);
        [SerializeField] private Color _goodColor = new Color(1f, 0.9f, 0.1f, 0.9f);
        [SerializeField] private Color _missColor = new Color(1f, 0.15f, 0.15f, 0.9f);
        [SerializeField] private Color _pressColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.3f;

        private float[] _remaining;
        private Color[] _base;

        private void Awake()
        {
            _remaining = new float[RhythmInputReader.LaneCount];
            _base = new Color[RhythmInputReader.LaneCount];
            for (int i = 0; i < _lanePads.Length && i < _remaining.Length; i++)
            {
                if (_lanePads[i] != null) _lanePads[i].color = Color.clear;
            }
        }

        private void OnEnable()
        {
            _track.NoteJudged += OnNoteJudged;
            _input.LanePressed += OnLanePressed;
        }

        private void OnDisable()
        {
            _track.NoteJudged -= OnNoteJudged;
            _input.LanePressed -= OnLanePressed;
        }

        private void OnLanePressed(int lane, double inputRealtime)
        {
            // No pisar un juicio que ya está en pantalla (el orden de suscripción no está garantizado).
            if (_remaining[lane] > 0f) return;
            Flash(lane, _pressColor, _fadeSeconds * 0.5f);
        }

        private void OnNoteJudged(HitType type, int lane, float deltaMs)
        {
            switch (type)
            {
                case HitType.Perfect: Flash(lane, _perfectColor, _fadeSeconds); break;
                case HitType.Good: Flash(lane, _goodColor, _fadeSeconds); break;
                case HitType.Miss: Flash(lane, _missColor, _fadeSeconds); break;
            }
        }

        private void Flash(int lane, Color color, float seconds)
        {
            _base[lane] = color;
            _remaining[lane] = seconds;
            SetPadColor(lane, color);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int lane = 0; lane < _remaining.Length; lane++)
            {
                if (_remaining[lane] <= 0f) continue;

                _remaining[lane] -= dt;
                float k = Mathf.Clamp01(_remaining[lane] / _fadeSeconds);
                Color c = _base[lane];
                c.a *= k;
                SetPadColor(lane, c);
            }
        }

        private void SetPadColor(int lane, Color color)
        {
            if (lane < _lanePads.Length && _lanePads[lane] != null) _lanePads[lane].color = color;
        }
    }
}
