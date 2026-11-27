using UnityEngine;
using UnityEngine.EventSystems;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Marca de selección del arte (cable de guitarra junto a las opciones del menú, subrayado con flecha en la pausa). Sigue
    /// al botón bajo el puntero o con el foco del teclado: cada botón lleva un SelectionMarkerTarget que le avisa. La posición
    /// se calcula en espacio de MUNDO a partir de los bordes del botón, así que funciona con cualquier anclaje (un botón
    /// anclado abajo a la derecha, otro arriba a la izquierda...) y con cualquier escala del canvas. Al mostrarse la pantalla
    /// se marca y se selecciona el botón por defecto (el teclado funciona desde el primer momento). El movimiento es un
    /// seguimiento suave que solo corre mientras hay un desplazamiento en curso: sin garbage.
    /// </summary>
    public sealed class SelectionMarker : MonoBehaviour
    {
        public enum Placement
        {
            /// <summary>A la izquierda del botón, con el extremo derecho del marcador tocando su borde izquierdo (cable del menú).</summary>
            LeftOfTarget,
            /// <summary>Debajo del botón, centrado y del ancho del botón (subrayado de la pausa).</summary>
            BelowTarget
        }

        [SerializeField] private RectTransform _marker;
        [SerializeField] private Placement _placement;
        [Tooltip("Botón que se marca y se selecciona al mostrarse la pantalla.")]
        [SerializeField] private RectTransform _defaultTarget;
        [Tooltip("Desplazamiento fino respecto de la posición calculada (px del canvas).")]
        [SerializeField] private Vector2 _offset;
        [Tooltip("Solo BelowTarget: el marcador mide el ancho del botón por este factor.")]
        [SerializeField, Min(0.5f)] private float _widthFactor = 1.2f;
        [Tooltip("Tiempo de seguimiento del movimiento (s). 0 = salto directo.")]
        [SerializeField, Min(0f)] private float _followSeconds = 0.1f;

        private RectTransform _current;
        private Vector3 _goal;
        private bool _moving;

        private void OnEnable()
        {
            _moving = false;
            if (_defaultTarget == null) return;

            MoveTo(_defaultTarget, true);

            // Selección inicial: el foco del teclado arranca en el botón por defecto.
            EventSystem es = EventSystem.current;
            if (es != null) es.SetSelectedGameObject(_defaultTarget.gameObject);
        }

        /// <summary>Mueve la marca hacia un botón. snap = true la coloca de inmediato (sin seguimiento).</summary>
        public void MoveTo(RectTransform target, bool snap = false)
        {
            if (_marker == null || target == null) return;
            _current = target;

            // Escala del canvas: los desplazamientos y tamaños en px se pasan a unidades de mundo.
            float worldScale = _marker.lossyScale.x > 0f ? _marker.lossyScale.x : 1f;
            Rect r = target.rect;

            if (_placement == Placement.LeftOfTarget)
            {
                // Pivote a la derecha: el borde derecho del marcador queda sobre el borde izquierdo del botón.
                _marker.pivot = new Vector2(1f, 0.5f);
                Vector3 edge = target.TransformPoint(new Vector3(r.xMin, r.center.y, 0f));
                _goal = edge + new Vector3(_offset.x * worldScale, _offset.y * worldScale, 0f);
            }
            else
            {
                float targetWidth = r.width * target.lossyScale.x / worldScale; // ancho del botón en px del canvas
                float width = targetWidth * _widthFactor;
                float aspect = _marker.sizeDelta.x > 0f ? _marker.sizeDelta.y / _marker.sizeDelta.x : 0.17f;
                _marker.pivot = new Vector2(0.5f, 1f);
                _marker.sizeDelta = new Vector2(width, width * aspect);
                Vector3 edge = target.TransformPoint(new Vector3(r.center.x, r.yMin, 0f));
                _goal = edge + new Vector3(_offset.x * worldScale, _offset.y * worldScale, 0f);
            }

            if (snap || _followSeconds <= 0f)
            {
                _marker.position = _goal;
                _moving = false;
            }
            else
            {
                _moving = true;
            }
        }

        /// <summary>Vuelve a calcular la posición sobre el último botón marcado (tras animaciones o cambios de layout).</summary>
        public void Refresh()
        {
            if (_current != null) MoveTo(_current, true);
        }

        private void Update()
        {
            if (!_moving) return;

            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / (_followSeconds * 0.4f));
            Vector3 p = Vector3.Lerp(_marker.position, _goal, k);
            if ((p - _goal).sqrMagnitude < 0.25f)
            {
                p = _goal;
                _moving = false;
            }
            _marker.position = p;
        }
    }
}
