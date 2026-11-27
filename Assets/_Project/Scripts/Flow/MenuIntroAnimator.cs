using System;
using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Animación de entrada en cascada del menú principal (orden de Animación_ux.mp4): 1° el fondo, 2° la ilustración, 3° el
    /// banner del logo, 4° los botones de papel uno tras otro y al final el cable de selección. Cada elemento interpola su
    /// CanvasGroup.alpha, un desplazamiento y una escala con tiempo sin escalar (ease-out cúbico). El Update solo corre mientras
    /// dura la animación (~1,7 s) y no genera garbage: los arrays se crean una sola vez en Awake. Se reproduce una vez por
    /// carga de la escena; mientras dura, la pantalla no recibe clics.
    /// </summary>
    public sealed class MenuIntroAnimator : MonoBehaviour
    {
        [Serializable]
        public sealed class Step
        {
            public RectTransform rect;
            public CanvasGroup group;
            [Min(0f)] public float delay;
            [Min(0.01f)] public float duration = 0.35f;
            [Tooltip("Desde dónde entra (px respecto de su posición final).")]
            public Vector2 offset;
            [Range(0.1f, 1.5f)] public float startScale = 1f;
        }

        [SerializeField] private Step[] _steps = Array.Empty<Step>();
        [Tooltip("Grupo de la pantalla: bloquea los clics hasta que termina la animación.")]
        [SerializeField] private CanvasGroup _inputGate;
        [Tooltip("Se reubica sobre el botón marcado al terminar (los botones se movieron durante la animación).")]
        [SerializeField] private SelectionMarker _marker;

        private const float MaxFrameStep = 1f / 30f;

        private Vector2[] _basePositions;
        private float _elapsed;
        private float _total;
        private bool _running;
        private bool _played;

        /// <summary>true mientras alguna entrada animada está en curso (los sonidos de hover de la UI se silencian).</summary>
        public static bool IsPlaying { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsPlaying = false;

        private void Awake()
        {
            _basePositions = new Vector2[_steps.Length];
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i].rect != null) _basePositions[i] = _steps[i].rect.anchoredPosition;
                float end = _steps[i].delay + _steps[i].duration;
                if (end > _total) _total = end;
            }
        }

        private void OnEnable()
        {
            if (_played) return; // volver desde Opciones o Créditos no repite la entrada

            _elapsed = 0f;
            _running = true;
            IsPlaying = true;
            if (_inputGate != null) _inputGate.blocksRaycasts = false;
            Apply(0f);
        }

        private void OnDisable()
        {
            // Si la pantalla se oculta a mitad de la entrada, todo queda en su estado final: nunca queda un elemento en alpha 0
            // ni el grupo bloqueado para la próxima vez que se muestre.
            if (_running) Finish();
        }

        private void Update()
        {
            if (!_running) return;

            // El primer cuadro tras cargar la escena puede durar medio segundo o más: se limita el paso para que la cascada no
            // se "saltee" entera y se vea completa desde su comienzo.
            _elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
            if (_elapsed >= _total)
            {
                Finish();
                return;
            }
            Apply(_elapsed);
        }

        private void Apply(float time)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                Step step = _steps[i];
                float k = Mathf.Clamp01((time - step.delay) / step.duration);
                float e = 1f - (1f - k) * (1f - k) * (1f - k); // ease-out cúbico

                if (step.group != null) step.group.alpha = e;
                if (step.rect == null) continue;
                step.rect.anchoredPosition = _basePositions[i] + step.offset * (1f - e);
                float scale = Mathf.Lerp(step.startScale, 1f, e);
                step.rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void Finish()
        {
            Apply(_total);
            _running = false;
            _played = true;
            IsPlaying = false;
            if (_inputGate != null) _inputGate.blocksRaycasts = true;
            if (_marker != null && isActiveAndEnabled)
            {
                _marker.Refresh();
                _marker.SelectDefaultIfNone(); // el teclado funciona apenas termina la entrada
            }
        }
    }
}
