using UnityEngine;
using UnityEngine.EventSystems;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Se agrega a un botón: al pasar el puntero o recibir el foco mueve la SelectionMarker hacia él. El puntero además da el
    /// foco al botón, así ratón y teclado comparten el mismo estado (se puede seguir con las flechas desde donde quedó el mouse).
    /// </summary>
    public sealed class SelectionMarkerTarget : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        [SerializeField] private SelectionMarker _marker;

        public void OnPointerEnter(PointerEventData eventData)
        {
            EventSystem es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != gameObject) es.SetSelectedGameObject(gameObject); // dispara OnSelect
            else Mark();
        }

        public void OnSelect(BaseEventData eventData) => Mark();

        private void Mark()
        {
            if (_marker != null) _marker.MoveTo((RectTransform)transform);
        }
    }
}
