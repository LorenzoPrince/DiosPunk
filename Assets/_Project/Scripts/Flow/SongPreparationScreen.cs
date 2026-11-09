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

        public void Begin(LevelData level, DiosPunk.Rhythm.SongData song)
        {
            _titleText.text = song != null ? level.Title + " - " + song.Title : level.Title;
            _stageText.text = "Escenario: " + level.Stage;
            _difficultyText.text = "Dificultad: " + level.Difficulty;
        }
    }
}
