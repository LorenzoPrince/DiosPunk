using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    public struct ResultsData
    {
        public int levelIndex;
        public int score;
        public float accuracy;
        public int maxCombo;
        public int perfect;
        public int good;
        public int miss;
        public int punkUses;
        public bool newBest;
        public bool nextUnlocked;
        public bool hasNextLevel;

        /// <summary>true si es el último nivel: CONTINUAR pasa a ser EPILOGO.</summary>
        public bool isFinalLevel;
    }

    /// <summary>Pantalla de resultados: puntaje, precisión ponderada, combo máximo y evaluación del siguiente nivel.</summary>
    public sealed class ResultsScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _accuracyText;
        [SerializeField] private TMP_Text _comboText;
        [SerializeField] private TMP_Text _punkText;
        [SerializeField] private TMP_Text _countsText;
        [SerializeField] private TMP_Text _evaluationText;
        [SerializeField] private Button _retry;
        [SerializeField] private Button _next;
        [SerializeField] private TMP_Text _nextLabel;
        [SerializeField] private Button _levelSelect;
        [SerializeField] private Button _mainMenu;

        public event Action RetryRequested;
        public event Action NextRequested;
        public event Action LevelSelectRequested;
        public event Action MainMenuRequested;

        private void Awake()
        {
            _retry.onClick.AddListener(() => RetryRequested?.Invoke());
            _next.onClick.AddListener(() => NextRequested?.Invoke());
            _levelSelect.onClick.AddListener(() => LevelSelectRequested?.Invoke());
            _mainMenu.onClick.AddListener(() => MainMenuRequested?.Invoke());
        }

        public void Display(in ResultsData data)
        {
            _scoreText.text = "Puntaje: " + data.score.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');
            _accuracyText.text = "Precision: " + data.accuracy.ToString("F1", CultureInfo.InvariantCulture) + "%";
            _comboText.text = "Combo maximo: " + data.maxCombo;
            _punkText.text = "Usos de Modo Punk: " + data.punkUses;
            _countsText.text = "Perfect " + data.perfect + "   Good " + data.good + "   Miss " + data.miss;

            string evaluation = data.isFinalLevel ? "Completaste el ultimo nivel! Sigue el epilogo."
                : data.nextUnlocked ? "Siguiente nivel desbloqueado!"
                : data.hasNextLevel ? "Siguiente nivel disponible."
                : string.Empty;
            if (data.newBest) evaluation += "\nNuevo record!";
            _evaluationText.text = evaluation;

            _next.interactable = data.hasNextLevel || data.isFinalLevel;
            _nextLabel.text = data.isFinalLevel ? "EPILOGO" : "CONTINUAR";
        }
    }
}
