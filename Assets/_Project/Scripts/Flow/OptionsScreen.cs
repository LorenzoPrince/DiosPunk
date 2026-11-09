using System;
using DiosPunk.Rhythm;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Opciones con tabs: Audio, Video, Controles y Accesibilidad. En la escena MainMenu la pestaña de Audio
    /// incluye "Recalibrar latencia", que abre la calibración sin reiniciar el progreso. Los ajustes aún no se persisten
    /// (salvo la latencia, que se guarda en progress.json al recalibrar).
    /// </summary>
    public sealed class OptionsScreen : MonoBehaviour
    {
        [SerializeField] private Button[] _tabButtons = Array.Empty<Button>();
        [SerializeField] private GameObject[] _tabPages = Array.Empty<GameObject>();
        [SerializeField] private Slider _volume;
        [Tooltip("Volumen de efectos de sonido (GameSettings.SfxVolume), independiente del volumen general.")]
        [SerializeField] private Slider _sfxVolume;
        [SerializeField] private Toggle _fullscreen;
        [SerializeField] private Toggle _reduceEffects;
        [SerializeField] private Button _back;

        [Header("Recalibración (solo en la escena MainMenu)")]
        [SerializeField] private Button _recalibrate;
        [SerializeField] private TMP_Text _latencyLabel;

        public event Action BackRequested;
        public event Action RecalibrateRequested;

        private void Awake()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                _tabButtons[i].onClick.AddListener(() => ShowTab(index));
            }

            _volume.onValueChanged.AddListener(value => AudioListener.volume = value);
            if (_sfxVolume != null) _sfxVolume.onValueChanged.AddListener(OnSfxVolumeChanged);
            _fullscreen.onValueChanged.AddListener(value => Screen.fullScreen = value);
            _reduceEffects.onValueChanged.AddListener(value => GameSettings.ReduceVisualEffects = value);
            _back.onClick.AddListener(() => BackRequested?.Invoke());
            if (_recalibrate != null) _recalibrate.onClick.AddListener(() => RecalibrateRequested?.Invoke());
        }

        private void OnEnable()
        {
            _volume.SetValueWithoutNotify(AudioListener.volume);
            if (_sfxVolume != null) _sfxVolume.SetValueWithoutNotify(GameSettings.SfxVolume);
            _fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            _reduceEffects.SetIsOnWithoutNotify(GameSettings.ReduceVisualEffects);

            if (_latencyLabel != null)
            {
                float latency = GameSettings.LatencyOffsetMs;
                _latencyLabel.text = "Latencia actual: " + (latency >= 0f ? "+" : string.Empty) + latency.ToString("F0") + " ms";
            }

            ShowTab(0);
        }

        // Al arrastrar el slider se oye un clic de muestra, como mucho cada 0.15 s para no saturar las voces.
        private const float PreviewIntervalSeconds = 0.15f;
        private float _lastPreviewTime = -1f;

        private void OnSfxVolumeChanged(float value)
        {
            GameSettings.SfxVolume = value;
            if (Time.unscaledTime - _lastPreviewTime < PreviewIntervalSeconds) return;
            _lastPreviewTime = Time.unscaledTime;
            SoundEffectManager.Play(SfxId.HitPerfect);
        }

        private void ShowTab(int index)
        {
            for (int i = 0; i < _tabPages.Length; i++) _tabPages[i].SetActive(i == index);
        }
    }
}
