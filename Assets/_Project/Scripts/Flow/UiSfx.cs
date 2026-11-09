using DiosPunk.Rhythm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Sonidos de UI de un control: hover del puntero o navegación con teclado/gamepad, y clic / submit.
    /// Lo agrega UiSfxBinder a cada Selectable de un canvas; solo reacciona a eventos del EventSystem.
    /// </summary>
    public sealed class UiSfxTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler, ISubmitHandler, IMoveHandler
    {
        private Selectable _selectable;

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
            if (Interactable) SoundEffectManager.Play(SfxId.UiClick);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (Interactable) SoundEffectManager.Play(SfxId.UiClick);
        }
    }

    /// <summary>
    /// Se coloca en la raíz de un Canvas: al iniciar agrega un UiSfxTrigger a cada Selectable (botones, toggles,
    /// sliders), incluidos los de pantallas inactivas. Trabajo único en Awake; no hay costo por frame.
    /// </summary>
    public sealed class UiSfxBinder : MonoBehaviour
    {
        private void Awake()
        {
            Selectable[] selectables = GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                GameObject go = selectables[i].gameObject;
                if (go.GetComponent<UiSfxTrigger>() == null) go.AddComponent<UiSfxTrigger>();
            }
        }
    }
}
