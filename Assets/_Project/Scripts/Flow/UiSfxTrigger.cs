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

        // Período de gracia al cargar la escena: el hover/navegación no suenan durante los primeros 0,25 s ni mientras corre la
        // entrada animada del menú (los botones se mueven bajo el puntero y la selección inicial se asigna sola).
        private const float StartupGraceSeconds = 0.25f;
        private static float s_graceUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_graceUntil = 0f;

        private static bool HoverMuted => MenuIntroAnimator.IsPlaying || Time.unscaledTime < s_graceUntil;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            float until = Time.unscaledTime + StartupGraceSeconds;
            if (until > s_graceUntil) s_graceUntil = until;
        }

        // Sin sonido si el control no es interactable o su pantalla está tapada/bloqueada por un CanvasGroup (p. ej. el menú
        // principal bajo Opciones): no se oye el hover ni el clic de lo que está detrás.
        private bool Interactable => (_selectable == null || _selectable.IsInteractable()) && !UiInputGuard.IsBlocked(this);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!HoverMuted && Interactable) SoundEffectManager.Play(SfxId.UiHover);
        }

        public void OnMove(AxisEventData eventData)
        {
            if (!HoverMuted && Interactable) SoundEffectManager.Play(SfxId.UiHover);
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
