using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Indicador de progreso de la pista en el HUD: cinta con aguja que avanza y contador "mm:ss / mm:ss".
    /// Lee SongClock.SongTime (dspTime) contra la duración de la pista. Cada frame solo actualiza el fill y la
    /// posición de la aguja (sin allocations); el texto se reescribe únicamente cuando cambia el segundo entero,
    /// con un char[] preasignado. Va en el mismo GameObject que el panel: se detiene cuando el HUD se oculta.
    /// </summary>
    public sealed class SongProgressPresenter : MonoBehaviour
    {
        [SerializeField] private RhythmTrackController _track;
        [Tooltip("Imagen con Image Type = Filled (Horizontal).")]
        [SerializeField] private Image _fill;
        [Tooltip("Aguja que acompaña el final del fill. Anclada a la izquierda y centrada en vertical dentro de BarArea.")]
        [SerializeField] private RectTransform _needle;
        [Tooltip("Contenedor de la barra (define el ancho útil de la aguja).")]
        [SerializeField] private RectTransform _barArea;
        [SerializeField] private TMP_Text _timeText;

        private readonly char[] _buffer = new char[TimeFormatter.BufferSize];
        private int _lastSecond = -1;
        private int _lastTotal = -1;

        private void OnEnable()
        {
            _track.SongStarted += Restart;
            Restart();
        }

        private void OnDisable()
        {
            _track.SongStarted -= Restart;
        }

        private void Restart()
        {
            _lastSecond = -1;
            _lastTotal = -1;
            Apply(0.0f, 0);
        }

        private void Update()
        {
            // Tras terminar o detener la canción la cinta queda donde estaba.
            if (!_track.IsRunning) return;

            float total = _track.DurationSeconds;
            if (total <= 0f) return;

            double time = _track.Clock.SongTime;
            float progress = Mathf.Clamp01((float)(time / total));
            int second = time <= 0.0 ? 0 : (int)System.Math.Min(time, total);

            Apply(progress, second);
        }

        private void Apply(float progress, int second)
        {
            _fill.fillAmount = progress;
            _needle.anchoredPosition = new Vector2(progress * _barArea.rect.width, 0f);

            int total = (int)_track.DurationSeconds;
            if (second == _lastSecond && total == _lastTotal) return;

            _lastSecond = second;
            _lastTotal = total;
            int length = TimeFormatter.FormatProgress(second, total, _buffer);
            _timeText.SetCharArray(_buffer, 0, length);
        }
    }
}
