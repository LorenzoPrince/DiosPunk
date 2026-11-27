using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Comprobación común de "¿este control puede reaccionar ahora?" para los efectos de hover y selección (cable del menú,
    /// sonidos de UI). Un control no reacciona si no está activo en la jerarquía o si algún CanvasGroup ancestro bloquea los
    /// raycasts o no es interactable (pantallas tapadas por otra: el menú principal bajo Opciones o el Selector, la hoja de
    /// pausa bajo Opciones...). Solo se consulta ante eventos del EventSystem y no genera garbage.
    /// </summary>
    public static class UiInputGuard
    {
        public static bool IsBlocked(Component control)
        {
            if (control == null || !control.gameObject.activeInHierarchy) return true;

            for (Transform t = control.transform; t != null; t = t.parent)
            {
                if (!t.TryGetComponent(out CanvasGroup group)) continue;
                if (!group.blocksRaycasts || !group.interactable) return true;
                if (group.ignoreParentGroups) break; // este grupo corta la herencia: los de más arriba ya no cuentan
            }
            return false;
        }
    }
}
