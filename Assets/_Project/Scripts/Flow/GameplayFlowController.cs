using DiosPunk.Rhythm;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Orquestador de la escena Gameplay: narrativa pre-nivel, preparación, canción, pausa, Game Over y
    /// resultados. El nivel a jugar viene de GameSession. Para salir al menú o cambiar de nivel carga otra
    /// escena con el SceneLoader (lo que libera la canción actual).
    /// </summary>
    public sealed class GameplayFlowController : FlowControllerBase
    {
        [Header("Ritmo")]
        [SerializeField] private RhythmTrackController _track;
        [SerializeField] private RhythmSession _session;

        [SerializeField] private LevelCatalog _catalog;

        [Header("Pantallas")]
        [SerializeField] private PreLevelScreen _preLevel;
        [SerializeField] private SongPreparationScreen _preparation;
        [SerializeField] private OptionsScreen _options;
        [SerializeField] private PauseMenuScreen _pause;
        [SerializeField] private GameOverScreen _gameOver;
        [SerializeField] private ResultsScreen _results;

        private ProgressStore _progress;
        private int _levelIndex;
        private SongData _song;
        private InputAction _escape;

        private LevelData CurrentLevel => _catalog.Levels[_levelIndex];

        private void Awake()
        {
            _progress = GameSession.GetProgress(_catalog.Count);
            _levelIndex = Mathf.Clamp(GameSession.SelectedLevelIndex, 0, _catalog.Count - 1);

            _escape = new InputAction("Escape", InputActionType.Button, "<Keyboard>/escape");
            _escape.performed += _ => OnEscape();
        }

        private void OnEnable()
        {
            _escape.Enable();

            _preLevel.Finished += OnPreLevelFinished;

            _preparation.StartRequested += BeginSong;
            _preparation.BackRequested += ExitToLevelSelect;

            _options.BackRequested += CloseOptions;

            _pause.ResumeRequested += ResumeGameplay;
            _pause.RestartRequested += BeginSong;
            _pause.OptionsRequested += OpenOptions;
            _pause.ExitRequested += ExitToLevelSelect;

            _gameOver.RetryRequested += BeginSong;
            _gameOver.OptionsRequested += OpenOptions;
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

            _preLevel.Finished -= OnPreLevelFinished;

            _preparation.StartRequested -= BeginSong;
            _preparation.BackRequested -= ExitToLevelSelect;

            _options.BackRequested -= CloseOptions;

            _pause.ResumeRequested -= ResumeGameplay;
            _pause.RestartRequested -= BeginSong;
            _pause.OptionsRequested -= OpenOptions;
            _pause.ExitRequested -= ExitToLevelSelect;

            _gameOver.RetryRequested -= BeginSong;
            _gameOver.OptionsRequested -= OpenOptions;
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
            _song = null; // suelta la referencia: el SceneLoader liberará el AudioClip
        }

        private void Start()
        {
            // La canción se carga solo en esta escena (Resources) y se libera al salir de ella.
            _song = CurrentLevel.LoadSong();
            _preLevel.Begin(CurrentLevel);
            Machine.Force(GameState.PreLevel);
        }

        // ---------- Narrativa y preparación ----------

        private void OnPreLevelFinished()
        {
            _preparation.Begin(CurrentLevel, _song);
            Machine.TryGoTo(GameState.SongPreparation);
        }

        private void OpenOptions() => Machine.TryGoTo(GameState.Options);

        private void CloseOptions() => Machine.TryGoTo(Machine.ReturnState);

        // ---------- Gameplay ----------

        /// <summary>Inicia o reinicia la canción del nivel actual (desde preparación, pausa o Game Over).</summary>
        private void BeginSong()
        {
            if (_song == null)
            {
                Debug.LogError("[Flow] El nivel no tiene una canción en Resources/Songs.");
                return;
            }
            if (!Machine.TryGoTo(GameState.Gameplay)) return;

            // El cambio de estado ya activó el mundo de juego: cargar la canción y arrancar.
            _track.LoadSong(_song);
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
                    CloseOptions(); // vuelve a Pausa o a Game Over según desde dónde se abrió
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
                punkUses = _session.Punk.UseCount,
                newBest = newBest,
                nextUnlocked = nextUnlocked,
                hasNextLevel = _levelIndex + 1 < _catalog.Count && _progress.UnlockedLevels > _levelIndex + 1
            };

            _track.StopSong();
            _results.Display(data);
            Machine.TryGoTo(GameState.ResultsScreen);
        }

        // ---------- Resultados y salidas ----------

        private void RetryFromResults()
        {
            _preLevel.Begin(CurrentLevel);
            Machine.TryGoTo(GameState.PreLevel);
        }

        /// <summary>Recarga la escena Gameplay con el nivel siguiente: así se descarta el audio del anterior.</summary>
        private void NextLevel()
        {
            if (SceneLoader.IsLoading || _levelIndex + 1 >= _catalog.Count) return;
            GameSession.SelectedLevelIndex = _levelIndex + 1;
            _track.StopSong();
            SceneLoader.Load(SceneLoader.GameplayScene);
        }

        private void ExitToLevelSelect()
        {
            if (SceneLoader.IsLoading) return;
            _track.StopSong();
            GameSession.OpenLevelSelectOnMenu = true;
            SceneLoader.Load(SceneLoader.MainMenuScene);
        }

        private void ExitToMainMenu()
        {
            if (SceneLoader.IsLoading) return;
            _track.StopSong();
            SceneLoader.Load(SceneLoader.MainMenuScene);
        }
    }
}
