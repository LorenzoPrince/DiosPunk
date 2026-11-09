using System;
using DiosPunk.Rhythm;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>Opciones con tabs: Audio, Video, Controles y Accesibilidad. Los ajustes aún no se persisten.</summary>
    public sealed class OptionsScreen : MonoBehaviour
    {
        [SerializeField] private Button[] _tabButtons = Array.Empty<Button>();
        [SerializeField] private GameObject[] _tabPages = Array.Empty<GameObject>();
        [SerializeField] private Slider _volume;
        [SerializeField] private Toggle _fullscreen;
        [SerializeField] private Toggle _reduceEffects;
        [SerializeField] private Button _back;

        public event Action BackRequested;

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
        }

        private void OnEnable()
        {
            _volume.SetValueWithoutNotify(AudioListener.volume);
            _fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            _reduceEffects.SetIsOnWithoutNotify(GameSettings.ReduceVisualEffects);
            ShowTab(0);
        }

        private void ShowTab(int index)
        {
            for (int i = 0; i < _tabPages.Length; i++) _tabPages[i].SetActive(i == index);
        }
    }
}
