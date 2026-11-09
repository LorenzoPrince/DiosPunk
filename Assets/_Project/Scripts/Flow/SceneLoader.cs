using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Servicio de carga de escenas. Usa SceneManager.LoadSceneAsync (modo Single: descarga la escena
    /// anterior) y, al terminar, libera los assets sin referencias (p. ej. el AudioClip de la canción
    /// anterior) con Resources.UnloadUnusedAssets. Vive en un GameObject DontDestroyOnLoad creado bajo demanda.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameplayScene = "Gameplay";

        private static SceneLoader s_instance;

        public static bool IsLoading { get; private set; }

        /// <summary>Progreso 0..1 de la carga en curso.</summary>
        public static event Action<float> ProgressChanged;

        /// <summary>Se dispara cuando la escena nueva ya está activa y se liberaron los assets sin uso.</summary>
        public static event Action<string> Loaded;

        public static void Load(string sceneName)
        {
            if (IsLoading) return;
            Ensure().StartCoroutine(Ensure().LoadRoutine(sceneName));
        }

        private static SceneLoader Ensure()
        {
            if (s_instance == null)
            {
                var go = new GameObject("SceneLoader");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<SceneLoader>();
            }
            return s_instance;
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError("[Flow] No se pudo cargar la escena '" + sceneName + "'. ¿Esta en Build Settings?");
                IsLoading = false;
                yield break;
            }

            while (!load.isDone)
            {
                ProgressChanged?.Invoke(load.progress);
                yield return null;
            }

            // Libera lo que ya nadie referencia (audio de la canción anterior, sprites de la escena vieja).
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();

            IsLoading = false;
            Loaded?.Invoke(sceneName);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_instance = null;
            IsLoading = false;
            ProgressChanged = null;
            Loaded = null;
        }
    }
}
