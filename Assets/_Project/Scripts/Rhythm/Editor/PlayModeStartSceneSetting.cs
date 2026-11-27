using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DiosPunk.Rhythm.Editor
{
    /// <summary>
    /// Opción de editor: al apretar Play desde cualquier escena el juego arranca en MainMenu (como lo haría una build), en
    /// vez de caer directo en el PreLevel del Gameplay sin pasar por el menú. Activada por defecto; se puede apagar desde
    /// DiosPunk/Play Mode para probar una escena suelta. No cambia la escena abierta en el editor.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartSceneSetting
    {
        private const string MenuPath = "DiosPunk/Play Mode/Iniciar siempre desde MainMenu";
        private const string PrefKey = "DiosPunk.PlayFromMainMenu";
        private const string MainMenuPath = "Assets/_Project/Scenes/MainMenu.unity";

        static PlayModeStartSceneSetting()
        {
            EditorApplication.delayCall += Apply;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        private static void Apply()
        {
            if (!Enabled)
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainMenuPath);
            EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
            Debug.Log("[Editor] Play " + (Enabled ? "arranca siempre desde MainMenu." : "arranca desde la escena abierta."));
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
