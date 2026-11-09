using UnityEngine;
using UnityEngine.InputSystem;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Orquestador de la escena MainMenu: menú principal, selector de niveles, opciones y créditos.
    /// Al elegir un nivel guarda el índice en GameSession y carga la escena Gameplay con el SceneLoader.
    /// </summary>
    public sealed class MenuFlowController : FlowControllerBase
    {
        [SerializeField] private LevelCatalog _catalog;

        [Header("Pantallas")]
        [SerializeField] private MainMenuScreen _mainMenu;
        [SerializeField] private LevelSelectScreen _levelSelect;
        [SerializeField] private OptionsScreen _options;
        [SerializeField] private CreditsScreen _credits;

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

            _mainMenu.PlayRequested += GoToLevelSelect;
            _mainMenu.OptionsRequested += OpenOptions;
            _mainMenu.CreditsRequested += OpenCredits;
            _mainMenu.QuitRequested += QuitApplication;

            _levelSelect.LevelChosen += OnLevelChosen;
            _levelSelect.BackRequested += GoToMainMenu;

            _options.BackRequested += CloseOptions;
            _credits.BackRequested += GoToMainMenu;
        }

        private void OnDisable()
        {
            _escape.Disable();

            _mainMenu.PlayRequested -= GoToLevelSelect;
            _mainMenu.OptionsRequested -= OpenOptions;
            _mainMenu.CreditsRequested -= OpenCredits;
            _mainMenu.QuitRequested -= QuitApplication;

            _levelSelect.LevelChosen -= OnLevelChosen;
            _levelSelect.BackRequested -= GoToMainMenu;

            _options.BackRequested -= CloseOptions;
            _credits.BackRequested -= GoToMainMenu;
        }

        private void OnDestroy()
        {
            _escape?.Dispose();
        }

        private void Start()
        {
            _levelSelect.Refresh(_catalog.Levels, _progress.UnlockedLevels);

            // Al volver desde el gameplay ("Selector") se abre directamente el selector.
            Machine.Force(GameSession.ConsumeOpenLevelSelect() ? GameState.LevelSelect : GameState.MainMenu);
        }

        private void GoToMainMenu() => Machine.TryGoTo(GameState.MainMenu);

        private void GoToLevelSelect()
        {
            _levelSelect.Refresh(_catalog.Levels, _progress.UnlockedLevels);
            Machine.TryGoTo(GameState.LevelSelect);
        }

        private void OpenOptions() => Machine.TryGoTo(GameState.Options);

        private void CloseOptions() => Machine.TryGoTo(Machine.ReturnState);

        private void OpenCredits() => Machine.TryGoTo(GameState.Credits);

        private void OnLevelChosen(int index)
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
                case GameState.Credits: GoToMainMenu(); break;
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
