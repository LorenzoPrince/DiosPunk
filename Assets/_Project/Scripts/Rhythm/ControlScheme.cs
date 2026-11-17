using System;
using UnityEngine.InputSystem;

namespace DiosPunk.Rhythm
{
    /// <summary>Esquema de teclas de los 4 carriles (se elige en Opciones y se guarda en progress.json).</summary>
    public enum ControlScheme
    {
        /// <summary>A S D F.</summary>
        Asdf = 0,
        /// <summary>D F J K (estándar de juegos de ritmo a 4 carriles).</summary>
        Dfjk = 1,
        /// <summary>Flechas Izquierda, Abajo, Arriba, Derecha.</summary>
        Arrows = 2,
        /// <summary>A S D F y Flechas a la vez (por defecto).</summary>
        All = 3
    }

    /// <summary>Textos y bindings de cada esquema. Todo son tablas estáticas: consultarlas no genera garbage.</summary>
    public static class ControlSchemes
    {
        public const int Count = 4;

        public static readonly string[] Names = { "ASDF", "DFJK", "FLECHAS", "TODOS" };

        // Rótulo corto por carril, usado bajo los receptáculos y en el tutorial.
        private static readonly string[][] s_laneLabels =
        {
            new[] { "A", "S", "D", "F" },
            new[] { "D", "F", "J", "K" },
            new[] { "←", "↓", "↑", "→" },
            new[] { "A / ←", "S / ↓", "D / ↑", "F / →" }
        };

        private static readonly string[] s_descriptions =
        {
            "A  S  D  F",
            "D  F  J  K",
            "←  ↓  ↑  →  (flechas)",
            "A  S  D  F  o  Flechas"
        };

        public static readonly string[] AsdfKeys =
            { "<Keyboard>/a", "<Keyboard>/s", "<Keyboard>/d", "<Keyboard>/f" };

        public static readonly string[] DfjkKeys =
            { "<Keyboard>/d", "<Keyboard>/f", "<Keyboard>/j", "<Keyboard>/k" };

        public static readonly string[] ArrowKeys =
            { "<Keyboard>/leftArrow", "<Keyboard>/downArrow", "<Keyboard>/upArrow", "<Keyboard>/rightArrow" };

        public static string GetLaneLabel(ControlScheme scheme, int lane) => s_laneLabels[(int)scheme][lane];

        public static string Describe(ControlScheme scheme) => s_descriptions[(int)scheme];

        public static string GetName(ControlScheme scheme) => Names[(int)scheme];

        public static ControlScheme Cycle(ControlScheme scheme, int step)
        {
            int next = ((int)scheme + step) % Count;
            if (next < 0) next += Count;
            return (ControlScheme)next;
        }

        /// <summary>Qué juegos de teclas (0 = ASDF, 1 = DFJK, 2 = flechas) están activos en el esquema.</summary>
        public static bool UsesKeySet(ControlScheme scheme, int keySet)
        {
            switch (keySet)
            {
                case 0: return scheme == ControlScheme.Asdf || scheme == ControlScheme.All;
                case 1: return scheme == ControlScheme.Dfjk;
                default: return scheme == ControlScheme.Arrows || scheme == ControlScheme.All;
            }
        }
    }

    /// <summary>
    /// Las InputActions de los 4 carriles para los tres juegos de teclas (ASDF, DFJK, flechas), creadas una sola vez.
    /// Cambiar de esquema solo activa o desactiva acciones ya creadas (sin allocations) y se reacciona al evento
    /// GameSettings.ControlSchemeChanged mientras el conjunto está habilitado. Cada pulsación entrega
    /// (carril, CallbackContext.time): el timestamp de hardware.
    /// </summary>
    public sealed class LaneInputSet : IDisposable
    {
        private static readonly string[][] s_bindings = { ControlSchemes.AsdfKeys, ControlSchemes.DfjkKeys, ControlSchemes.ArrowKeys };

        private readonly InputAction[][] _sets;
        private readonly Action<InputAction.CallbackContext>[][] _handlers;
        private bool _enabled;

        public LaneInputSet(Action<int, double> onPress)
        {
            int lanes = RhythmInputReader.LaneCount;
            _sets = new InputAction[s_bindings.Length][];
            _handlers = new Action<InputAction.CallbackContext>[s_bindings.Length][];

            for (int set = 0; set < s_bindings.Length; set++)
            {
                _sets[set] = new InputAction[lanes];
                _handlers[set] = new Action<InputAction.CallbackContext>[lanes];
                for (int lane = 0; lane < lanes; lane++)
                {
                    int capturedLane = lane; // captura única, en init
                    var action = new InputAction("Lane" + lane + "_Set" + set, InputActionType.Button, s_bindings[set][lane]);
                    Action<InputAction.CallbackContext> handler = ctx => onPress(capturedLane, ctx.time);
                    action.performed += handler;
                    _sets[set][lane] = action;
                    _handlers[set][lane] = handler;
                }
            }
        }

        public void Enable()
        {
            if (_enabled) return;
            _enabled = true;
            GameSettings.ControlSchemeChanged += OnSchemeChanged;
            Apply(GameSettings.ControlScheme);
        }

        public void Disable()
        {
            if (!_enabled) return;
            _enabled = false;
            GameSettings.ControlSchemeChanged -= OnSchemeChanged;
            Apply(GameSettings.ControlScheme);
        }

        private void OnSchemeChanged(ControlScheme scheme) => Apply(scheme);

        private void Apply(ControlScheme scheme)
        {
            for (int set = 0; set < _sets.Length; set++)
            {
                bool want = _enabled && ControlSchemes.UsesKeySet(scheme, set);
                for (int lane = 0; lane < _sets[set].Length; lane++)
                {
                    InputAction action = _sets[set][lane];
                    if (want == action.enabled) continue;
                    if (want) action.Enable(); else action.Disable();
                }
            }
        }

        public void Dispose()
        {
            Disable();
            for (int set = 0; set < _sets.Length; set++)
            {
                for (int lane = 0; lane < _sets[set].Length; lane++)
                {
                    _sets[set][lane].performed -= _handlers[set][lane];
                    _sets[set][lane].Dispose();
                }
            }
        }
    }
}
