using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    public sealed class PauseMenuScreen : MonoBehaviour
    {
        [SerializeField] private Button _resume;
        [SerializeField] private Button _restart;
        [SerializeField] private Button _options;
        [SerializeField] private Button _exit;

        public event Action ResumeRequested;
        public event Action RestartRequested;
        public event Action OptionsRequested;
        public event Action ExitRequested;

        private void Awake()
        {
            _resume.onClick.AddListener(() => ResumeRequested?.Invoke());
            _restart.onClick.AddListener(() => RestartRequested?.Invoke());
            _options.onClick.AddListener(() => OptionsRequested?.Invoke());
            _exit.onClick.AddListener(() => ExitRequested?.Invoke());
        }
    }
}
