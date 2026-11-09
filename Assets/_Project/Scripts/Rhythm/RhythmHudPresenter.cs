using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Presenter del HUD de juego: puntaje y multiplicador (esquina superior derecha) y barra de vida.
    /// 100% orientado a eventos: no hay Update ni polling; SetText con formato numérico no genera garbage.
    /// </summary>
    public sealed class RhythmHudPresenter : MonoBehaviour
    {
        [SerializeField] private RhythmSession _session;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _multiplierText;
        [Tooltip("Imagen con Image Type = Filled (Horizontal).")]
        [SerializeField] private Image _healthFill;
        [Tooltip("Opcional.")]
        [SerializeField] private TMP_Text _accuracyText;

        private void OnEnable()
        {
            _session.Score.Changed += OnScoreChanged;
            _session.Health.Changed += OnHealthChanged;

            OnScoreChanged(_session.Score.Score, _session.Score.Multiplier, _session.Score.Combo);
            OnHealthChanged(_session.Health.Current, _session.Health.Max);
        }

        private void OnDisable()
        {
            _session.Score.Changed -= OnScoreChanged;
            _session.Health.Changed -= OnHealthChanged;
        }

        private void OnScoreChanged(int score, int multiplier, int combo)
        {
            _scoreText.SetText("{0:0}", score);
            _multiplierText.SetText("x{0:0}", multiplier);
            if (_accuracyText != null) _accuracyText.SetText("{0:1}%", _session.Score.AccuracyPercent);
        }

        private void OnHealthChanged(float current, float max)
        {
            _healthFill.fillAmount = max > 0f ? current / max : 0f;
        }
    }
}
