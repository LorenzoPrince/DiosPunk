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

    /// <summary>
    /// Se coloca en la raíz de un Canvas: al iniciar agrega un UiSfxTrigger a cada Selectable (botones, toggles,
    /// sliders), incluidos los de pantallas inactivas, y marca como importantes los que se llamen como los de la
    /// lista (por nombre del GameObject). Trabajo único en Awake; no hay costo por frame.
    /// </summary>
    public sealed class UiSfxBinder : MonoBehaviour
    {
        [Tooltip("Nombres de GameObject de los botones que suenan como 'opción importante' (#43).")]
        [SerializeField] private string[] _importantNames =
        {
            "PlayButton", "NewGameButton", "StartButton", "YesButton", "FinishButton", "AcceptButton"
        };

        private void Awake()
        {
            Selectable[] selectables = GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                GameObject go = selectables[i].gameObject;
                UiSfxTrigger trigger = go.GetComponent<UiSfxTrigger>();
                if (trigger == null) trigger = go.AddComponent<UiSfxTrigger>();
                trigger.Important = IsImportant(go.name);
            }
        }

        private bool IsImportant(string objectName)
        {
            for (int i = 0; i < _importantNames.Length; i++)
            {
                if (_importantNames[i] == objectName) return true;
            }
            return false;
        }
    }
}
