using DiosPunk.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Orquestador de la escena MainMenu: menú principal, partida nueva (calibración + tutorial), selector de
    /// niveles, opciones y créditos. Al elegir un nivel guarda el índice en GameSession y carga la escena
    /// Gameplay con el SceneLoader.
    /// </summary>
    public sealed class MenuFlowController : FlowControllerBase
    {
        [SerializeField] private LevelCatalog _catalog;

        [Header("Pantallas")]
        [SerializeField] private MainMenuScreen _mainMenu;
        [SerializeField] private LevelSelectScreen _levelSelect;
        [SerializeField] private OptionsScreen _options;
        [SerializeField] private CreditsScreen _credits;
        [SerializeField] private CalibrationScreen _calibration;

        private ProgressStore _progress;
        private InputAction _escape;

        private void Awake()
        {
            _progress = GameSession.GetProgress(_catalog.Count);

            _escape = new InputAction("Escape", InputActionType.Button, "<Keyboard>/escape");
            _escape.performed += _ => OnEscape();
        }

        private void OnEnable()
        {
            _escape.Enable();

            _mainMenu.PlayRequested += OnPlayRequested;
            _mainMenu.NewGameRequested += OnNewGameRequested;
            _mainMenu.NewGameConfirmed += StartNewGame;
            _mainMenu.OptionsRequested += OpenOptions;
            _mainMenu.CreditsRequested += OpenCredits;
            _mainMenu.QuitRequested += QuitApplication;

            _levelSelect.LevelChosen += StartLevel;
            _levelSelect.BackRequested += GoToMainMenu;

            _options.BackRequested += CloseOptions;
            _credits.BackRequested += GoToMainMenu;

            _calibration.Completed += OnCalibrationCompleted;
            _calibration.SkipRequested += OnCalibrationSkipped;
            _calibration.BackRequested += GoToMainMenu;
        }

        private void OnDisable()
        {
            _escape.Disable();

            _mainMenu.PlayRequested -= OnPlayRequested;
            _mainMenu.NewGameRequested -= OnNewGameRequested;
            _mainMenu.NewGameConfirmed -= StartNewGame;
            _mainMenu.OptionsRequested -= OpenOptions;
            _mainMenu.CreditsRequested -= OpenCredits;
            _mainMenu.QuitRequested -= QuitApplication;

            _levelSelect.LevelChosen -= StartLevel;
            _levelSelect.BackRequested -= GoToMainMenu;

            _options.BackRequested -= CloseOptions;
            _credits.BackRequested -= GoToMainMenu;

            _calibration.Completed -= OnCalibrationCompleted;
            _calibration.SkipRequested -= OnCalibrationSkipped;
            _calibration.BackRequested -= GoToMainMenu;
        }

        private void OnDestroy()
        {
            _escape?.Dispose();
        }

        private void Start()
        {
            _levelSelect.Refresh(_catalog.Levels, _progress.UnlockedLevels);
            _mainMenu.SetHasSavedProgress(GameSession.HasSavedProgress);

            // Al volver desde el gameplay ("Selector") se abre directamente el selector.
            Machine.Force(GameSession.ConsumeOpenLevelSelect() ? GameState.LevelSelect : GameState.MainMenu);
        }

        // ---------- Menú principal ----------

        private void GoToMainMenu()
        {
            _mainMenu.SetHasSavedProgress(GameSession.HasSavedProgress);
            Machine.TryGoTo(GameState.MainMenu);
        }

        /// <summary>JUGAR / CONTINUAR: con progreso guardado va al selector; sin progreso es la primera partida.</summary>
        private void OnPlayRequested()
        {
            if (GameSession.HasSavedProgress) GoToLevelSelect();
            else StartNewGame();
        }

        /// <summary>PARTIDA NUEVA: pide confirmación si ya hay progreso (porque lo reemplaza).</summary>
        private void OnNewGameRequested()
        {
            if (GameSession.HasSavedProgress) _mainMenu.ShowNewGameConfirmation();
            else StartNewGame();
        }

        /// <summary>Entra a la calibración + tutorial. El progreso solo se reemplaza al terminar u omitir.</summary>
        private void StartNewGame()
        {
            Machine.TryGoTo(GameState.Calibration);
        }

        private void GoToLevelSelect()
        {
            _levelSelect.Refresh(_catalog.Levels, _progress.UnlockedLevels);
            Machine.TryGoTo(GameState.LevelSelect);
        }

        private void OpenOptions() => Machine.TryGoTo(GameState.Options);

        private void CloseOptions() => Machine.TryGoTo(Machine.ReturnState);

        private void OpenCredits() => Machine.TryGoTo(GameState.Credits);

        // ---------- Primera partida ----------

        private void OnCalibrationCompleted(float latencyOffsetMs)
        {
            GameSettings.LatencyOffsetMs = latencyOffsetMs;
            _progress.StartNewGame(true, latencyOffsetMs);
            StartLevel(0);
        }

        private void OnCalibrationSkipped()
        {
            _progress.StartNewGame(false, 0f);
            GameSettings.LatencyOffsetMs = 0f;
            StartLevel(0);
        }

        private void StartLevel(int index)
        {
            if (SceneLoader.IsLoading) return;
            GameSession.SelectedLevelIndex = index;
            SceneLoader.Load(SceneLoader.GameplayScene);
        }

        private void OnEscape()
        {
            switch (Machine.Current)
            {
                case GameState.Options: CloseOptions(); break;
                case GameState.LevelSelect:
                case GameState.Credits:
                case GameState.Calibration: GoToMainMenu(); break;
            }
        }

        private void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        [ContextMenu("Reset Progress")]
        private void ResetProgress()
        {
            _progress?.Reset();
            _levelSelect.Refresh(_catalog.Levels, _progress.UnlockedLevels);
        }

        [ContextMenu("Unlock All Levels")]
        private void UnlockAllLevels()
        {
            _progress?.UnlockAll();
            _levelSelect.Refresh(_catalog.Levels, _progress.UnlockedLevels);
        }
    }
}
