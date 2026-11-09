using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Menú principal. "JUGAR" pasa a "CONTINUAR" si ya hay progreso guardado. "PARTIDA NUEVA" pide confirmación
    /// cuando hay progreso, porque lo reemplaza.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private const string PlayLabelFirstTime = "JUGAR";
        private const string PlayLabelContinue = "CONTINUAR";

        [SerializeField] private Button _play;
        [SerializeField] private TMP_Text _playLabel;
        [SerializeField] private Button _newGame;
        [SerializeField] private Button _options;
        [SerializeField] private Button _credits;
        [SerializeField] private Button _quit;

        [Header("Confirmación de partida nueva")]
        [SerializeField] private GameObject _confirmPanel;
        [SerializeField] private Button _confirmYes;
        [SerializeField] private Button _confirmNo;

        public event Action PlayRequested;
        public event Action NewGameRequested;
        public event Action NewGameConfirmed;
        public event Action OptionsRequested;
        public event Action CreditsRequested;
        public event Action QuitRequested;

        private void Awake()
        {
            _play.onClick.AddListener(() => PlayRequested?.Invoke());
            _newGame.onClick.AddListener(() => NewGameRequested?.Invoke());
            _options.onClick.AddListener(() => OptionsRequested?.Invoke());
            _credits.onClick.AddListener(() => CreditsRequested?.Invoke());
            _quit.onClick.AddListener(() => QuitRequested?.Invoke());

            _confirmYes.onClick.AddListener(() =>
            {
                _confirmPanel.SetActive(false);
                NewGameConfirmed?.Invoke();
            });
            _confirmNo.onClick.AddListener(() => _confirmPanel.SetActive(false));
        }

        private void OnEnable()
        {
            _confirmPanel.SetActive(false);
        }

        public void SetHasSavedProgress(bool hasSave)
        {
            _playLabel.text = hasSave ? PlayLabelContinue : PlayLabelFirstTime;
        }

        public void ShowNewGameConfirmation()
        {
            _confirmPanel.SetActive(true);
        }
    }
}
