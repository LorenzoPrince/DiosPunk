using DiosPunk.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Orquesta el flujo de pantallas: dueño de la máquina de estados, conecta las pantallas con el motor
    /// de ritmo (pausa, Game Over, victoria) y el progreso guardado. No manipula la UI directamente: eso
    /// lo hace el ScreenManager reaccionando a los cambios de estado.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [Header("Ritmo")]
        [SerializeField] private RhythmTrackController _track;
        [SerializeField] private RhythmSession _session;

        [Header("Niveles (1 a 5)")]
        [SerializeField] private LevelData[] _levels = System.Array.Empty<LevelData>();

        [Header("Pantallas")]
        [SerializeField] private MainMenuScreen _mainMenu;
        [SerializeField] private LevelSelectScreen _levelSelect;
        [SerializeField] private OptionsScreen _options;
        [SerializeField] private PreLevelScreen _preLevel;
        [SerializeField] private SongPreparationScreen _preparation;
        [SerializeField] private PauseMenuScreen _pause;
        [SerializeField] private GameOverScreen _gameOver;
        [SerializeField] private ResultsScreen _results;

        private ProgressStore _progress;
        private int _levelIndex;
        private InputAction _escape;

        public GameStateMachine Machine { get; } = new GameStateMachine();

        private LevelData CurrentLevel => _levels[_levelIndex];

        private void Awake()
        {
            _progress = ProgressStore.Load(_levels.Length);

            _escape = new InputAction("Escape", InputActionType.Button, "<Keyboard>/escape");
            _escape.performed += _ => OnEscape();
        }

        private void OnEnable()
        {
            _escape.Enable();

            _mainMenu.PlayRequested += GoToLevelSelect;
            _mainMenu.OptionsRequested += OpenOptions;
            _mainMenu.QuitRequested += QuitApplication;

            _levelSelect.LevelChosen += OnLevelChosen;
            _levelSelect.BackRequested += GoToMainMenu;

            _options.BackRequested += CloseOptions;

            _preLevel.Finished += OnPreLevelFinished;

            _preparation.StartRequested += BeginSong;
            _preparation.BackRequested += GoToLevelSelect;

            _pause.ResumeRequested += ResumeGameplay;
            _pause.RestartRequested += BeginSong;
            _pause.OptionsRequested += OpenOptions;
            _pause.ExitRequested += ExitToLevelSelect;

            _gameOver.RetryRequested += BeginSong;
            _gameOver.LevelSelectRequested += ExitToLevelSelect;
            _gameOver.MainMenuRequested += ExitToMainMenu;

            _results.RetryRequested += RetryFromResults;
            _results.NextRequested += NextLevel;
            _results.LevelSelectRequested += ExitToLevelSelect;
            _results.MainMenuRequested += ExitToMainMenu;

            _track.SongFinished += OnSongFinished;
            _session.HealthDepleted += OnHealthDepleted;
        }

        private void OnDisable()
        {
            _escape.Disable();

            _mainMenu.PlayRequested -= GoToLevelSelect;
            _mainMenu.OptionsRequested -= OpenOptions;
            _mainMenu.QuitRequested -= QuitApplication;

            _levelSelect.LevelChosen -= OnLevelChosen;
            _levelSelect.BackRequested -= GoToMainMenu;

            _options.BackRequested -= CloseOptions;

            _preLevel.Finished -= OnPreLevelFinished;

            _preparation.StartRequested -= BeginSong;
            _preparation.BackRequested -= GoToLevelSelect;

            _pause.ResumeRequested -= ResumeGameplay;
            _pause.RestartRequested -= BeginSong;
            _pause.OptionsRequested -= OpenOptions;
            _pause.ExitRequested -= ExitToLevelSelect;

            _gameOver.RetryRequested -= BeginSong;
            _gameOver.LevelSelectRequested -= ExitToLevelSelect;
            _gameOver.MainMenuRequested -= ExitToMainMenu;

            _results.RetryRequested -= RetryFromResults;
            _results.NextRequested -= NextLevel;
            _results.LevelSelectRequested -= ExitToLevelSelect;
            _results.MainMenuRequested -= ExitToMainMenu;

            _track.SongFinished -= OnSongFinished;
            _session.HealthDepleted -= OnHealthDepleted;
        }

        private void OnDestroy()
        {
            _escape?.Dispose();
        }

        private void Start()
        {
            Machine.Force(GameState.MainMenu);
        }

        // ---------- Navegación de menús ----------

        private void GoToMainMenu() => Machine.TryGoTo(GameState.MainMenu);

        private void GoToLevelSelect()
        {
            _levelSelect.Refresh(_levels, _progress.UnlockedLevels);
            Machine.TryGoTo(GameState.LevelSelect);
        }

        private void OpenOptions() => Machine.TryGoTo(GameState.Options);

        private void CloseOptions() => Machine.TryGoTo(Machine.ReturnState);

        private void OnLevelChosen(int index)
        {
            _levelIndex = index;
            EnterPreLevel();
        }

        private void EnterPreLevel()
        {
            _preLevel.Begin(CurrentLevel);
            Machine.TryGoTo(GameState.PreLevel);
        }

        private void OnPreLevelFinished()
        {
            _preparation.Begin(CurrentLevel);
            Machine.TryGoTo(GameState.SongPreparation);
        }

        private void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------- Gameplay ----------

        /// <summary>Inicia o reinicia la canción del nivel actual (desde preparación, pausa o Game Over).</summary>
        private void BeginSong()
        {
            if (!Machine.TryGoTo(GameState.Gameplay)) return;

            // El cambio de estado ya activó el mundo de juego: cargar la canción y arrancar.
            _track.LoadSong(CurrentLevel.Song);
            _track.StartSong();
        }

        private void OnEscape()
        {
            switch (Machine.Current)
            {
                case GameState.Gameplay:
                    _track.PauseSong();
                    Machine.TryGoTo(GameState.PauseMenu);
                    break;
                case GameState.PauseMenu:
                    ResumeGameplay();
                    break;
                case GameState.Options:
                    CloseOptions();
                    break;
                case GameState.LevelSelect:
                    GoToMainMenu();
                    break;
            }
        }

        private void ResumeGameplay()
        {
            if (!Machine.TryGoTo(GameState.Gameplay)) return;
            _track.ResumeSong();
        }

        private void OnHealthDepleted()
        {
            if (Machine.Current != GameState.Gameplay) return;
            _track.StopSong();
            Machine.TryGoTo(GameState.GameOver);
        }

        private void OnSongFinished()
        {
            if (Machine.Current != GameState.Gameplay || _session.Health.IsDepleted) return;

            ScoreSystem score = _session.Score;
            RhythmStats stats = _track.Stats;

            bool newBest = _progress.RegisterScore(_levelIndex, score.Score);
            bool nextUnlocked = _progress.UnlockUpTo(_levelIndex + 2);
            _progress.Save();

            var data = new ResultsData
            {
                levelIndex = _levelIndex,
                score = score.Score,
                accuracy = score.AccuracyPercent,
                maxCombo = score.MaxCombo,
                perfect = stats.Perfect,
                good = stats.Good,
                miss = stats.Miss,
                punkUses = 0, // Modo Punk: Hito 3
                newBest = newBest,
                nextUnlocked = nextUnlocked,
                hasNextLevel = _levelIndex + 1 < _levels.Length && _progress.UnlockedLevels > _levelIndex + 1
            };

            _track.StopSong();
            _results.Display(data);
            Machine.TryGoTo(GameState.ResultsScreen);
        }

        // ---------- Salidas (limpian la sesión de ritmo) ----------

        private void ExitToLevelSelect()
        {
            _track.StopSong();
            GoToLevelSelect();
        }

        private void ExitToMainMenu()
        {
            _track.StopSong();
            GoToMainMenu();
        }

        private void RetryFromResults() => EnterPreLevel();

        private void NextLevel()
        {
            if (_levelIndex + 1 >= _levels.Length) return;
            _levelIndex++;
            EnterPreLevel();
        }

        // ---------- Utilidades de prueba ----------

        [ContextMenu("Reset Progress")]
        private void ResetProgress() => _progress?.Reset();

        [ContextMenu("Unlock All Levels")]
        private void UnlockAllLevels() => _progress?.UnlockAll();
    }
}
