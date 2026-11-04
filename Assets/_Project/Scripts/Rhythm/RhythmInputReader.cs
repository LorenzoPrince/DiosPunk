using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Mapea 4 carriles a D-F-J-K y Flechas en simultáneo usando el New Input System y emite
    /// (lane, timestamp de hardware). El timestamp es CallbackContext.time: el momento real del
    /// evento, independiente de en qué frame se procesó (línea de tiempo de Time.realtimeSinceStartup).
    /// Las InputActions se crean por código para no depender de editar el .inputactions del proyecto.
    /// </summary>
    public sealed class RhythmInputReader : MonoBehaviour
    {
        public const int LaneCount = 4;

        private static readonly string[] s_primaryKeys =
            { "<Keyboard>/d", "<Keyboard>/f", "<Keyboard>/j", "<Keyboard>/k" };

        private static readonly string[] s_arrowKeys =
            { "<Keyboard>/leftArrow", "<Keyboard>/downArrow", "<Keyboard>/upArrow", "<Keyboard>/rightArrow" };

        /// <summary>laneIndex, timestamp del input en la línea de tiempo de Time.realtimeSinceStartup.</summary>
        public event Action<int, double> LanePressed;

        private InputAction[] _actions;
        private Action<InputAction.CallbackContext>[] _handlers;

        private void Awake()
        {
            _actions = new InputAction[LaneCount];
            _handlers = new Action<InputAction.CallbackContext>[LaneCount];

            for (int i = 0; i < LaneCount; i++)
            {
                int lane = i; // captura una sola vez, en init
                var action = new InputAction("Lane" + lane, InputActionType.Button);
                action.AddBinding(s_primaryKeys[lane]);
                action.AddBinding(s_arrowKeys[lane]);

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
