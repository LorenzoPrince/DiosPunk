using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Efecto placeholder del debuff: capa de color pulsante sobre la pantalla + temblor de la pista, con un
    /// rótulo "DEBUFF!". Si el escudo Punk lo absorbe, solo muestra un aviso breve. Con la opción de
    /// accesibilidad GameSettings.ReduceVisualEffects se anula el temblor y la capa queda tenue y fija.
    /// Se suscribe a eventos; el Update solo trabaja mientras hay un efecto activo o un aviso visible (sin allocations).
    /// </summary>
    public sealed class RhythmDebuffVisuals : MonoBehaviour
    {
        private const string DebuffMessage = "DEBUFF!";
        private const string AbsorbedMessage = "ESCUDO PUNK: DEBUFF ABSORBIDO";

        [SerializeField] private RhythmSession _session;
        [Tooltip("Transform que tiembla (la raíz de la pista). Solo es visual: el juicio usa tiempos, no posiciones.")]
        [SerializeField] private Transform _shakeTarget;
        [SerializeField] private Image _overlay;
        [SerializeField] private TMP_Text _statusText;

        [SerializeField] private Color _overlayColor = new Color(0.75f, 0.1f, 0.85f, 0.35f);
        [SerializeField, Min(0f)] private float _shakeAmount = 0.12f;
        [SerializeField, Min(0.1f)] private float _pulseHz = 4f;
        [SerializeField, Min(0.1f)] private float _messageSeconds = 1.5f;

        private bool _active;
        private float _messageRemaining;
        private Vector3 _basePosition;

        private void OnEnable()
        {
            _basePosition = _shakeTarget.localPosition;
            _session.Debuffs.Started += OnStarted;
            _session.Debuffs.Ended += OnEnded;
            _session.Debuffs.Absorbed += OnAbsorbed;
            ClearAll();
        }

        private void OnDisable()
        {
            _session.Debuffs.Started -= OnStarted;
            _session.Debuffs.Ended -= OnEnded;
            _session.Debuffs.Absorbed -= OnAbsorbed;
            ClearAll();
        }

        private void OnStarted(float duration)
        {
            _active = true;
            _overlay.enabled = true;
            _statusText.text = DebuffMessage;
            _statusText.enabled = true;
        }

        private void OnEnded()
        {
            _active = false;
            _overlay.enabled = false;
            _shakeTarget.localPosition = _basePosition;
            if (_messageRemaining <= 0f) _statusText.enabled = false;
        }

        private void OnAbsorbed()
        {
            _messageRemaining = _messageSeconds;
            _statusText.text = AbsorbedMessage;
            _statusText.enabled = true;
        }

        private void Update()
        {
            if (!_active && _messageRemaining <= 0f) return;

            if (_messageRemaining > 0f)
            {
                _messageRemaining -= Time.unscaledDeltaTime;
                if (_messageRemaining <= 0f && !_active) _statusText.enabled = false;
            }

            if (!_active) return;

            if (_session.IsPaused)
            {
                _shakeTarget.localPosition = _basePosition;
                return;
            }

            float time = Time.unscaledTime;
            bool reduce = GameSettings.ReduceVisualEffects;

            // Capa de color: pulsante normalmente; tenue y fija con accesibilidad.
            Color c = _overlayColor;
            c.a *= reduce ? 0.25f : 0.5f + 0.5f * Mathf.Sin(time * _pulseHz * 6.2832f);
            _overlay.color = c;

            if (reduce)
            {
                _shakeTarget.localPosition = _basePosition;
            }
            else
            {
                float x = (Mathf.PerlinNoise(time * 31f, 0.17f) - 0.5f) * 2f * _shakeAmount;
                float y = (Mathf.PerlinNoise(0.53f, time * 29f) - 0.5f) * 2f * _shakeAmount;
                _shakeTarget.localPosition = _basePosition + new Vector3(x, y, 0f);
            }
        }

        private void ClearAll()
        {
            _active = false;
            _messageRemaining = 0f;
            if (_overlay != null) _overlay.enabled = false;
            if (_statusText != null) _statusText.enabled = false;
            if (_shakeTarget != null) _shakeTarget.localPosition = _basePosition;
        }
    }
}
