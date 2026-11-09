using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    public sealed class SongPreparationScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _stageText;
        [SerializeField] private TMP_Text _difficultyText;
        [SerializeField] private Button _start;
        [SerializeField] private Button _back;

        public event Action StartRequested;
        public event Action BackRequested;

        private void Awake()
        {
            _start.onClick.AddListener(() => StartRequested?.Invoke());
            _back.onClick.AddListener(() => BackRequested?.Invoke());
        }

        /// <summary>Muestra título, escenario y dificultad, incluyendo lo elegido en la escena narrativa (GameSession).</summary>
        public void Begin(LevelData level, DiosPunk.Rhythm.SongData song)
        {
            _titleText.text = song != null ? level.Title + " - " + song.Title : level.Title;

            string stage = "Escenario: " + level.Stage;
            if (GameSession.ScenarioVariant > 0) stage += " (variante " + GameSession.ScenarioVariant + ")";
            _stageText.text = stage;

            string difficulty = "Dificultad: " + level.Difficulty;
            int modifier = GameSession.DifficultyModifier;
            if (modifier != 0)
            {
                int percent = Mathf.RoundToInt((GameSession.NoteSpeedMultiplier - 1f) * 100f);
                difficulty += " (velocidad " + (percent > 0 ? "+" : string.Empty) + percent + "%)";
            }
            _difficultyText.text = difficulty;
        }
    }
}
