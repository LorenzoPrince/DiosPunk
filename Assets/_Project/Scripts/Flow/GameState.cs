using System;
using UnityEngine;

namespace DiosPunk.Flow
{
    public enum GameState
    {
        MainMenu = 0,
        LevelSelect = 1,
        Options = 2,
        PreLevel = 3,
        SongPreparation = 4,
        Gameplay = 5,
        PauseMenu = 6,
        GameOver = 7,
        ResultsScreen = 8
    }

    /// <summary>
    /// Máquina de estados de navegación: lógica pura (sin MonoBehaviour). Valida las transiciones permitidas
    /// y recuerda desde dónde se abrió Options para poder volver (MainMenu o PauseMenu).
    /// </summary>
    public sealed class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.MainMenu;

        /// <summary>Estado desde el que se abrió Options.</summary>
        public GameState ReturnState { get; private set; } = GameState.MainMenu;

        public bool OptionsOpenedFromPause => Current == GameState.Options && ReturnState == GameState.PauseMenu;

        /// <summary>previo, nuevo.</summary>
        public event Action<GameState, GameState> Changed;

        public bool CanTransition(GameState from, GameState to)
        {
            switch (from)
            {
                case GameState.MainMenu:
                    return to == GameState.LevelSelect || to == GameState.Options;
                case GameState.LevelSelect:
                    return to == GameState.MainMenu || to == GameState.PreLevel;
                case GameState.Options:
                    return to == GameState.MainMenu || to == GameState.PauseMenu;
                case GameState.PreLevel:
                    return to == GameState.SongPreparation || to == GameState.LevelSelect;
                case GameState.SongPreparation:
                    return to == GameState.Gameplay || to == GameState.LevelSelect;
                case GameState.Gameplay:
                    return to == GameState.PauseMenu || to == GameState.GameOver || to == GameState.ResultsScreen;
                case GameState.PauseMenu:
                    return to == GameState.Gameplay || to == GameState.Options
                        || to == GameState.LevelSelect || to == GameState.MainMenu;
                case GameState.GameOver:
                    return to == GameState.Gameplay || to == GameState.LevelSelect || to == GameState.MainMenu;
                case GameState.ResultsScreen:
                    return to == GameState.PreLevel || to == GameState.LevelSelect || to == GameState.MainMenu;
                default:
                    return false;
            }
        }

        public bool TryGoTo(GameState next)
        {
            if (!CanTransition(Current, next))
            {
                Debug.LogWarning($"[Flow] Transicion no permitida: {Current} -> {next}");
                return false;
            }

            if (next == GameState.Options) ReturnState = Current;
            Apply(next);
            return true;
        }

        /// <summary>Fuerza un estado sin validar (arranque / reset).</summary>
        public void Force(GameState state) => Apply(state);

        private void Apply(GameState next)
        {
            GameState previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
        }
    }
}
