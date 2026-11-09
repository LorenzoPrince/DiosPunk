using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    public sealed class GameOverScreen : MonoBehaviour
    {
        [SerializeField] private Button _retry;
        [SerializeField] private Button _options;
        [SerializeField] private Button _levelSelect;
        [SerializeField] private Button _mainMenu;

        public event Action RetryRequested;
        public event Action OptionsRequested;
        public event Action LevelSelectRequested;
        public event Action MainMenuRequested;

        private void Awake()
        {
            _retry.onClick.AddListener(() => RetryRequested?.Invoke());
            _options.onClick.AddListener(() => OptionsRequested?.Invoke());
            _levelSelect.onClick.AddListener(() => LevelSelectRequested?.Invoke());
            _mainMenu.onClick.AddListener(() => MainMenuRequested?.Invoke());
        }
    }
}
