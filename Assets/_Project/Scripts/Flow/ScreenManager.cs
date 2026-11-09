using UnityEngine;

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

        private void OnStateChanged(GameState previous, GameState next) => Apply();

        private void Apply()
        {
            GameStateMachine machine = _flow.Machine;
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] != null) _panels[i].Apply(machine.Current, machine.OptionsOpenedFromPause);
            }
        }
    }
}
