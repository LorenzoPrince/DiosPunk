using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Popup de juicio con el arte del escenario (Perfect / Good / Miss, dos variantes cada uno que se alternan). Aparece
    /// con un pequeño "pop" y se desvanece en una fracción de segundo. Solo actúa si el nivel tiene tema (Configure): sin
    /// tema queda apagado. Un fallo suave (Bad) usa el arte de Miss. El Update trabaja solo mientras el popup está visible
    /// y no genera garbage; los fundidos usan tiempo sin escalar (cosméticos).
    /// </summary>
    public sealed class JudgementPopup : MonoBehaviour
    {
        [SerializeField] private RhythmTrackController _track;
        [SerializeField] private Image _image;
        [Tooltip("Segundos que dura el popup.")]
        [SerializeField, Min(0.1f)] private float _seconds = 0.45f;
        [Tooltip("Tamaño respecto del sprite original (los sprites son de ~500 px de ancho).")]
        [SerializeField, Range(0.2f, 1.5f)] private float _spriteScale = 0.5f;

        private Sprite[] _perfect, _good, _miss;
        private bool _configured;
        private int _variant;
        private float _remaining;

        private void OnEnable()
        {
            _track.NoteJudged += OnNoteJudged;
            _image.enabled = false;
        }

        private void OnDisable()
        {
            _track.NoteJudged -= OnNoteJudged;
            _remaining = 0f;
        }

        public void Configure(StageTheme theme)
        {
            _perfect = theme.Perfect;
            _good = theme.Good;
            _miss = theme.Miss;
            _configured = true;
        }

        private void OnNoteJudged(HitType type, int lane, float deltaMs)
        {
            if (!_configured) return;

            Sprite[] set;
            switch (type)
            {
                case HitType.Perfect: set = _perfect; break;
                case HitType.Good: set = _good; break;
                case HitType.Miss:
                case HitType.Bad: set = _miss; break;
                default: return;
            }
            if (set == null || set.Length == 0) return;

            _variant = (_variant + 1) % set.Length; // alterna las dos variantes
            Sprite sprite = set[_variant];
            if (sprite == null) return;

            _image.sprite = sprite;
            _image.rectTransform.sizeDelta = sprite.rect.size * _spriteScale;
            _image.enabled = true;
            _remaining = _seconds;
            Apply(1f);
        }

        private void Update()
        {
            if (_remaining <= 0f) return;

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining <= 0f)
            {
                _image.enabled = false;
                return;
            }
            Apply(_remaining / _seconds);
        }

        // k: 1 al aparecer, 0 al terminar. Pop inicial (1,15 -> 1) y fundido en la segunda mitad.
        private void Apply(float k)
        {
            float pop = 1f + 0.15f * Mathf.Clamp01((k - 0.7f) / 0.3f);
            _image.rectTransform.localScale = new Vector3(pop, pop, 1f);
            _image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 2f));
        }
    }
}
