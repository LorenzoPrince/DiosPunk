using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Servicio de carga de escenas con cortinilla. Usa SceneManager.LoadSceneAsync (modo Single: descarga la
    /// escena anterior) detrás de una cortinilla estilo fanzine (SceneTransitionOverlay, prefab en
    /// Resources/UI/SceneTransition): fundido de entrada, carga con la activación retenida hasta que la cortinilla
    /// cubre la pantalla, y fundido de salida. Al terminar libera los assets sin referencias (p. ej. el AudioClip
    /// de la canción anterior) con Resources.UnloadUnusedAssets. Vive en un GameObject DontDestroyOnLoad creado
    /// bajo demanda. Si el prefab no existe, carga igual pero en seco.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string GameplayScene = "Gameplay";
        public const string OverlayResource = "UI/SceneTransition";

        private const float FadeInSeconds = 0.25f;
        private const float FadeOutSeconds = 0.35f;
        // La cortinilla se muestra al menos este tiempo: evita un parpadeo cuando la escena carga al instante.
        private const float MinCoverSeconds = 0.45f;
        // LoadSceneAsync con allowSceneActivation = false se detiene en 0.9.
        private const float ActivationProgress = 0.9f;

        private static SceneLoader s_instance;

        private SceneTransitionOverlay _overlay;

        public static bool IsLoading { get; private set; }

        /// <summary>Progreso 0..1 de la carga en curso.</summary>
        public static event Action<float> ProgressChanged;

        /// <summary>Se dispara cuando la escena nueva ya está activa, se liberaron los assets sin uso y terminó la cortinilla.</summary>
        public static event Action<string> Loaded;

        public static void Load(string sceneName)
        {
            if (IsLoading) return;
            SceneLoader loader = Ensure();
            loader.StartCoroutine(loader.LoadRoutine(sceneName));
        }

        private static SceneLoader Ensure()
        {
            if (s_instance == null)
            {
                var go = new GameObject("SceneLoader");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<SceneLoader>();
                s_instance.CreateOverlay();
            }
            return s_instance;
        }

        private void CreateOverlay()
        {
            var prefab = Resources.Load<SceneTransitionOverlay>(OverlayResource);
            if (prefab == null)
            {
                Debug.LogWarning("[Flow] No se encontro Resources/" + OverlayResource + ": las escenas cargan sin cortinilla.");
                return;
            }
            _overlay = Instantiate(prefab, transform);
            _overlay.name = "SceneTransitionOverlay";
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;

            // 1) La cortinilla cubre la pantalla antes de tocar la escena.
            if (_overlay != null)
            {
                _overlay.Begin();
                yield return Fade(_overlay.Alpha, 1f, FadeInSeconds);
            }

            AsyncOperation load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError("[Flow] No se pudo cargar la escena '" + sceneName + "'. ¿Esta en Build Settings?");
                if (_overlay != null) yield return Fade(_overlay.Alpha, 0f, FadeOutSeconds);
                IsLoading = false;
                yield break;
            }

            // 2) Carga con la activación retenida: la escena nueva no aparece hasta que la cortinilla lleva su tiempo mínimo.
            load.allowSceneActivation = false;
            float coverStart = Time.unscaledTime;

            while (load.progress < ActivationProgress || Time.unscaledTime - coverStart < MinCoverSeconds)
            {
                float progress = Mathf.Clamp01(load.progress / ActivationProgress);
                if (_overlay != null)
                {
                    _overlay.SetProgress(progress);
                    _overlay.Animate(Time.unscaledTime - coverStart);
                }
                ProgressChanged?.Invoke(progress);
                yield return null;
            }

            if (_overlay != null) _overlay.SetProgress(1f);
            ProgressChanged?.Invoke(1f);
            load.allowSceneActivation = true;

            while (!load.isDone) yield return null;

            // Libera lo que ya nadie referencia (audio de la canción anterior, sprites de la escena vieja).
            yield return Resources.UnloadUnusedAssets();
            GC.Collect();

            // 3) La escena nueva ya está lista detrás de la cortinilla: se descubre con un fundido.
            if (_overlay != null) yield return Fade(1f, 0f, FadeOutSeconds);

            IsLoading = false;
            Loaded?.Invoke(sceneName);
        }

        /// <summary>Interpola el alpha de la cortinilla con tiempo sin escalar (funciona aunque timeScale sea 0).</summary>
        private IEnumerator Fade(float from, float to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _overlay.SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds)));
                yield return null;
            }
            _overlay.SetAlpha(to);
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
