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

        [Tooltip("Si es true, sigue visible cuando Options se abrió desde la pausa (para ver el juego detrás).")]
        [SerializeField] private bool _keepWhenOptionsFromPause;

        public void Apply(GameState state, bool optionsOpenedFromPause)
        {
            bool visible = optionsOpenedFromPause && _keepWhenOptionsFromPause;
            for (int i = 0; i < _visibleIn.Length && !visible; i++)
            {
                if (_visibleIn[i] == state) visible = true;
            }

            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
