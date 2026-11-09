using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Cortinilla de carga estilo fanzine: fundido a negro con grano de fotocopia, una tira de papel con el
    /// rótulo "CARGANDO..." y una cinta de progreso. La maneja el SceneLoader (alpha y progreso) y vive en un
    /// Canvas propio con el orden más alto, dentro del objeto DontDestroyOnLoad del loader, así tapa la carga
    /// asíncrona de la escena. Todo es cosmético (tiempo sin escalar); sin allocations: los rótulos son
    /// strings constantes.
    /// </summary>
    public sealed class SceneTransitionOverlay : MonoBehaviour
    {
        private static readonly string[] s_labels = { "CARGANDO", "CARGANDO.", "CARGANDO..", "CARGANDO..." };

        [SerializeField] private Canvas _canvas;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _paper;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private Image _progressFill;

        [Tooltip("Cada cuántos segundos se reanima el rótulo (puntos suspensivos) y el temblor de la fotocopia.")]
        [SerializeField, Min(0.05f)] private float _stepSeconds = 0.3f;

        private int _lastStep = -1;
        private float _baseRotation;

        public float Alpha => _group.alpha;

        private void Awake()
        {
            _baseRotation = _paper != null ? _paper.localEulerAngles.z : 0f;
            Hide();
        }

        /// <summary>Alpha 0..1 de toda la cortinilla. Con 0 se apaga el canvas (sin overdraw ni raycasts).</summary>
        public void SetAlpha(float alpha)
        {
            _group.alpha = alpha;
            bool visible = alpha > 0f;
            if (_canvas.enabled != visible) _canvas.enabled = visible;
            _group.blocksRaycasts = visible; // mientras se ve, bloquea los clics a la UI de abajo
        }

        public void SetProgress(float progress01)
        {
            if (_progressFill != null) _progressFill.fillAmount = Mathf.Clamp01(progress01);
        }

        /// <summary>Reinicia la animación del rótulo para una carga nueva.</summary>
        public void Begin()
        {
            _lastStep = -1;
            SetProgress(0f);
            Animate(0f);
        }

        /// <summary>Anima los puntos del rótulo y un leve temblor de la tira de papel (efecto fotocopia).</summary>
        public void Animate(float elapsedSeconds)
        {
            int step = (int)(elapsedSeconds / _stepSeconds);
            if (step == _lastStep) return;
            _lastStep = step;

            if (_label != null) _label.text = s_labels[step & 3];
            if (_paper != null) _paper.localRotation = Quaternion.Euler(0f, 0f, _baseRotation + ((step & 1) == 0 ? 0f : 0.8f));
        }

        public void Hide()
        {
            SetAlpha(0f);
        }
    }
}
