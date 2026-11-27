using DiosPunk.Rhythm;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>Muestra y oculta los ScreenPanel de la escena según el estado de su orquestador (solo ante cambios de estado).</summary>
    public sealed class ScreenManager : MonoBehaviour
    {
        [SerializeField] private FlowControllerBase _flow;
        [SerializeField] private ScreenPanel[] _panels = System.Array.Empty<ScreenPanel>();

        private void OnEnable()
        {
            _flow.Machine.Changed += OnStateChanged;
            Apply();
        }

        private void OnDisable()
        {
            _flow.Machine.Changed -= OnStateChanged;
        }

        private bool _firstChangeSeen;

        private void OnStateChanged(GameState previous, GameState next)
        {
            Apply();
            EnsureValidSelection();

            // Cambio de hoja entre pantallas. No en el estado inicial de la escena (la cortinilla ya suena) ni
            // dentro del juego (pausa, Game Over, resultados): ahí manda el feedback propio de cada caso.
            if (_firstChangeSeen && previous != GameState.Gameplay && next != GameState.Gameplay)
            {
                SoundEffectManager.Play(SfxId.PageTurn);
            }
            _firstChangeSeen = true;
        }

        /// <summary>
        /// Al cambiar de pantalla el EventSystem puede quedarse con seleccionado un botón de la pantalla que se ocultó (p. ej.
        /// JUGAR mientras están abiertas las Opciones): la navegación con teclado se perdía y el foco quedaba "detrás". Si la
        /// selección ya no es válida se limpia y pasa al primer control interactable de la pantalla visible.
        /// </summary>
        private void EnsureValidSelection()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;

            GameObject selected = es.currentSelectedGameObject;
            if (selected != null && !UiInputGuard.IsBlocked(selected.transform)) return;

            es.SetSelectedGameObject(null);
            for (int i = 0; i < _panels.Length; i++)
            {
                ScreenPanel panel = _panels[i];
                if (panel == null || !panel.gameObject.activeInHierarchy) continue;

                Selectable first = panel.GetComponentInChildren<Selectable>(false);
                if (first != null && first.IsInteractable() && !UiInputGuard.IsBlocked(first))
                {
                    es.SetSelectedGameObject(first.gameObject);
                    return;
                }
            }
        }

        private void Apply()
        {
            GameStateMachine machine = _flow.Machine;
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] != null) _panels[i].Apply(machine.Current, machine.OptionsOpenedOverGameplay);
            }
        }
    }
}
