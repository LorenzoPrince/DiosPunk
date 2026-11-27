using DiosPunk.Rhythm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Sonidos de UI de un control: hover del puntero o navegación con teclado/gamepad, y clic / submit.
    /// Lo agrega UiSfxBinder a cada Selectable de un canvas; solo reacciona a eventos del EventSystem.
    /// Los controles marcados como "importantes" (Comenzar, Jugar, Aceptar...) suenan con UiImportant (#43).
    /// </summary>
    public sealed class UiSfxTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler, IMoveHandler
    {
        private Selectable _selectable;

        /// <summary>true = el clic suena como "opción importante" en vez del clic común.</summary>
        public bool Important { get; set; }

        private SfxId ClickId => Important ? SfxId.UiImportant : SfxId.UiClick;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        private bool Interactable => _selectable == null || _selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Interactable) SoundEffectManager.Play(SfxId.UiHover);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (Interactable) SoundEffectManager.Play(SfxId.UiHover);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactable) SoundEffectManager.Play(ClickId);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (Interactable) SoundEffectManager.Play(ClickId);
        }
    }
}
