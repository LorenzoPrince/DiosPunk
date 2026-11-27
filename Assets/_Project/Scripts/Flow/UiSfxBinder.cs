using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
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
