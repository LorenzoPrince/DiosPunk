using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    public sealed class MainMenuScreen : MonoBehaviour
    {
        [SerializeField] private Button _play;
        [SerializeField] private Button _options;
        [SerializeField] private Button _credits;
        [SerializeField] private Button _quit;

        public event Action PlayRequested;
        public event Action OptionsRequested;
        public event Action CreditsRequested;
        public event Action QuitRequested;

        private void Awake()
        {
            _play.onClick.AddListener(() => PlayRequested?.Invoke());
            _options.onClick.AddListener(() => OptionsRequested?.Invoke());
            _credits.onClick.AddListener(() => CreditsRequested?.Invoke());
            _quit.onClick.AddListener(() => QuitRequested?.Invoke());
        }
    }
}
