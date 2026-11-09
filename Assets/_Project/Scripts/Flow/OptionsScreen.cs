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
            _fullscreen.onValueChanged.AddListener(value => Screen.fullScreen = value);
            _reduceEffects.onValueChanged.AddListener(value => GameSettings.ReduceVisualEffects = value);
            _back.onClick.AddListener(() => BackRequested?.Invoke());
            if (_recalibrate != null) _recalibrate.onClick.AddListener(() => RecalibrateRequested?.Invoke());
        }

        private void OnEnable()
        {
            _volume.SetValueWithoutNotify(AudioListener.volume);
            _fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            _reduceEffects.SetIsOnWithoutNotify(GameSettings.ReduceVisualEffects);

            if (_latencyLabel != null)
            {
                float latency = GameSettings.LatencyOffsetMs;
                _latencyLabel.text = "Latencia actual: " + (latency >= 0f ? "+" : string.Empty) + latency.ToString("F0") + " ms";
            }

            ShowTab(0);
        }

        private void ShowTab(int index)
        {
            for (int i = 0; i < _tabPages.Length; i++) _tabPages[i].SetActive(i == index);
        }
    }
}
