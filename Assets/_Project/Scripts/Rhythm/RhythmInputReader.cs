using System;
using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Mapea los 4 carriles al esquema de teclas activo (GameSettings.ControlScheme: ASDF, DFJK, Flechas o ambos
    /// ASDF + Flechas) con el New Input System y emite (lane, timestamp de hardware). El timestamp es
    /// CallbackContext.time: el momento real del evento, independiente de en qué frame se procesó (línea de
    /// tiempo de Time.realtimeSinceStartup). Las InputActions se crean por código una sola vez (LaneInputSet) y
    /// al cambiar de esquema solo se activan/desactivan: sin allocations ni rebindeos en caliente.
    /// </summary>
    public sealed class RhythmInputReader : MonoBehaviour
    {
        public const int LaneCount = 4;

        /// <summary>laneIndex, timestamp del input en la línea de tiempo de Time.realtimeSinceStartup.</summary>
        public event Action<int, double> LanePressed;

        private LaneInputSet _set;

        /// <summary>Emite un input sintético (bot de depuración) con el timestamp indicado.</summary>
        public void InjectLanePress(int lane, double inputRealtime)
        {
            LanePressed?.Invoke(lane, inputRealtime);
        }

        private void Awake()
        {
            _set = new LaneInputSet((lane, time) => LanePressed?.Invoke(lane, time));
        }

        private void OnEnable() => _set.Enable();

        private void OnDisable() => _set.Disable();

        private void OnDestroy() => _set?.Dispose();
    }
}
