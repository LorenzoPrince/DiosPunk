using System;
using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Estados de navegación. MainMenu, LevelSelect, Options y Credits viven en la escena MainMenu;
    /// PreLevel, SongPreparation, Gameplay, PauseMenu, GameOver y ResultsScreen en la escena Gameplay
    /// (Options también aparece en Gameplay, abierta desde la pausa).
    /// </summary>
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
        ResultsScreen = 8,
        Credits = 9
    }

    /// <summary>
    /// Máquina de estados de navegación: lógica pura (sin MonoBehaviour). Valida las transiciones permitidas
    /// dentro de una escena y recuerda desde dónde se abrió Options para poder volver (MainMenu o PauseMenu).
    /// Los cambios de escena (menú ↔ gameplay) los hace el SceneLoader, no esta máquina.
    /// </summary>
    public sealed class GameStateMachine
    {
        public GameState Current { get; private set; } = GameState.MainMenu;

        /// <summary>Estado desde el que se abrió Options.</summary>
        public GameState ReturnState { get; private set; } = GameState.MainMenu;

        /// <summary>Options abierta sobre el juego (desde la pausa o el Game Over): el mundo de juego sigue visible detrás.</summary>
        public bool OptionsOpenedOverGameplay =>
            Current == GameState.Options && (ReturnState == GameState.PauseMenu || ReturnState == GameState.GameOver);

        /// <summary>previo, nuevo.</summary>
        public event Action<GameState, GameState> Changed;

        public bool CanTransition(GameState from, GameState to)
        {
            switch (from)
            {
                case GameState.MainMenu:
                    return to == GameState.LevelSelect || to == GameState.Options || to == GameState.Credits;
                case GameState.LevelSelect:
                    return to == GameState.MainMenu;
                case GameState.Credits:
                    return to == GameState.MainMenu;
                case GameState.Options:
                    return to == GameState.MainMenu || to == GameState.PauseMenu || to == GameState.GameOver;
                case GameState.PreLevel:
                    return to == GameState.SongPreparation;
                case GameState.SongPreparation:
                    return to == GameState.Gameplay;
                case GameState.Gameplay:
                    return to == GameState.PauseMenu || to == GameState.GameOver || to == GameState.ResultsScreen;
                case GameState.PauseMenu:
                    return to == GameState.Gameplay || to == GameState.Options;
                case GameState.GameOver:
                    return to == GameState.Gameplay || to == GameState.Options;
                case GameState.ResultsScreen:
                    return to == GameState.PreLevel;
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

        /// <summary>Fuerza un estado sin validar (arranque de escena).</summary>
        public void Force(GameState state) => Apply(state);

        private void Apply(GameState next)
        {
            GameState previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
        }
    }
}
