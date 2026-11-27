using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Marca un GameObject (panel de UI o raíz del mundo de juego) como visible en ciertos estados.
    /// El ScreenManager lo activa o desactiva; el objeto no necesita estar activo para ser gestionado.
    /// </summary>
    public sealed class ScreenPanel : MonoBehaviour
    {
        [SerializeField] private GameState[] _visibleIn = System.Array.Empty<GameState>();

        [Tooltip("Si es true, sigue visible cuando Options se abrió sobre el juego (desde la pausa o el Game Over).")]
        [SerializeField] private bool _keepWhenOptionsFromPause;

        [Tooltip("CanvasGroup que bloquea la interacción de la pantalla cuando no es la activa (interactable y blocksRaycasts en false) " +
                 "y la restaura al volver. Defensa extra: aunque el panel siguiera visible, sus botones no reaccionarían al hover.")]
        [SerializeField] private CanvasGroup _inputGroup;

        public void Apply(GameState state, bool optionsOpenedFromPause)
        {
            bool visible = optionsOpenedFromPause && _keepWhenOptionsFromPause;
            for (int i = 0; i < _visibleIn.Length && !visible; i++)
            {
                if (_visibleIn[i] == state) visible = true;
            }

            if (gameObject.activeSelf == visible)
            {
                // Sin cambio de visibilidad: una pantalla visible nunca queda con el grupo bloqueado (salvo mientras corre la
                // entrada animada, que bloquea los clics a propósito).
                if (visible && _inputGroup != null && !MenuIntroAnimator.IsPlaying)
                {
                    if (!_inputGroup.interactable) _inputGroup.interactable = true;
                    if (!_inputGroup.blocksRaycasts) _inputGroup.blocksRaycasts = true;
                }
                return;
            }

            // La interacción se fija ANTES de activar: un OnEnable (p. ej. la entrada animada del menú, que bloquea los clics
            // mientras dura) puede volver a bloquearla sin que este Apply la pise después.
            if (_inputGroup != null)
            {
                _inputGroup.interactable = visible;
                _inputGroup.blocksRaycasts = visible;
            }
            gameObject.SetActive(visible);
        }
    }
}
