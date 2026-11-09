using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Mapea 4 carriles en simultáneo a A-S-D-F y a las Flechas (Izq, Abajo, Arriba, Der) con el New Input System y emite
    /// (lane, timestamp de hardware). El timestamp es CallbackContext.time: el momento real del
    /// evento, independiente de en qué frame se procesó (línea de tiempo de Time.realtimeSinceStartup).
    /// Las InputActions se crean por código para no depender de editar el .inputactions del proyecto.
    /// </summary>
    public sealed class RhythmInputReader : MonoBehaviour
    {
        public const int LaneCount = 4;

        public static readonly string[] PrimaryKeys =
            { "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d", "<Keyboard>/f" };

        public static readonly string[] ArrowKeys =
            { "<Keyboard>/leftArrow", "<Keyboard>/downArrow", "<Keyboard>/upArrow", "<Keyboard>/rightArrow" };

        /// <summary>laneIndex, timestamp del input en la línea de tiempo de Time.realtimeSinceStartup.</summary>
        public event Action<int, double> LanePressed;

        private InputAction[] _actions;
        private Action<InputAction.CallbackContext>[] _handlers;

        /// <summary>Emite un input sintético (bot de depuración) con el timestamp indicado.</summary>
        public void InjectLanePress(int lane, double inputRealtime)
        {
            LanePressed?.Invoke(lane, inputRealtime);
        }

        private void Awake()
        {
            _actions = new InputAction[LaneCount];
            _handlers = new Action<InputAction.CallbackContext>[LaneCount];

            for (int i = 0; i < LaneCount; i++)
            {
                int lane = i; // captura una sola vez, en init
                var action = new InputAction("Lane" + lane, InputActionType.Button);
                action.AddBinding(PrimaryKeys[lane]);
                action.AddBinding(ArrowKeys[lane]);

                Action<InputAction.CallbackContext> handler = ctx => LanePressed?.Invoke(lane, ctx.time);
                action.performed += handler;

                _actions[lane] = action;
                _handlers[lane] = handler;
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _actions.Length; i++) _actions[i].Enable();
        }

        private void OnDisable()
        {
            for (int i = 0; i < _actions.Length; i++) _actions[i].Disable();
        }

        private void OnDestroy()
        {
            if (_actions == null) return;
            for (int i = 0; i < _actions.Length; i++)
            {
                _actions[i].performed -= _handlers[i];
                _actions[i].Dispose();
            }
        }
    }
}
