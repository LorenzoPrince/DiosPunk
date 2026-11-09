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

        [Header("Modo Punk (esquina superior izquierda)")]
        [Tooltip("Imagen con Image Type = Filled (Horizontal).")]
        [SerializeField] private Image _punkFill;
        [SerializeField] private TMP_Text _punkLabel;
        [SerializeField] private Color _punkChargingColor = new Color(0.85f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color _punkActiveColor = new Color(1f, 0.85f, 0.1f, 1f);

        private const string PunkLabelIdle = "MODO PUNK";
        private const string PunkLabelActive = "MODO PUNK ACTIVO!";

        private readonly char[] _scoreBuffer = new char[ScoreFormatter.BufferSize];

        private void OnEnable()
        {
            _session.Score.Changed += OnScoreChanged;
            _session.Health.Changed += OnHealthChanged;
            _session.Punk.BarChanged += OnPunkBarChanged;

            OnPunkBarChanged(_session.Punk.Bar, _session.Punk.IsActive);
            OnScoreChanged(_session.Score.Score, _session.Score.Multiplier, _session.Score.Combo);
            OnHealthChanged(_session.Health.Current, _session.Health.Max);
        }

        private void OnDisable()
        {
            _session.Score.Changed -= OnScoreChanged;
            _session.Health.Changed -= OnHealthChanged;
            _session.Punk.BarChanged -= OnPunkBarChanged;
        }

        private void OnPunkBarChanged(float bar, bool active)
        {
            _punkFill.fillAmount = bar;
            _punkFill.color = active ? _punkActiveColor : _punkChargingColor;
            if (_punkLabel != null) _punkLabel.text = active ? PunkLabelActive : PunkLabelIdle;
        }

        private void OnScoreChanged(int score, int multiplier, int combo)
        {
            int length = ScoreFormatter.Format(score, _scoreBuffer);
            _scoreText.SetCharArray(_scoreBuffer, 0, length);
            _multiplierText.SetText("x{0:0}", multiplier);
            if (_accuracyText != null) _accuracyText.SetText("{0:1}%", _session.Score.AccuracyPercent);
        }

        private void OnHealthChanged(float current, float max)
        {
            _healthFill.fillAmount = max > 0f ? current / max : 0f;
        }
    }
}
