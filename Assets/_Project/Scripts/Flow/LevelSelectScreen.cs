using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>Selector de niveles 1 a 5. Los niveles no desbloqueados quedan no interactuables.</summary>
    public sealed class LevelSelectScreen : MonoBehaviour
    {
        [SerializeField] private Button[] _levelButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] _levelLabels = Array.Empty<TMP_Text>();
        [SerializeField] private Button _back;

        /// <summary>Índice base 0.</summary>
        public event Action<int> LevelChosen;
        public event Action BackRequested;

        private void Awake()
        {
            for (int i = 0; i < _levelButtons.Length; i++)
            {
                int index = i;
                _levelButtons[i].onClick.AddListener(() => LevelChosen?.Invoke(index));
            }
            _back.onClick.AddListener(() => BackRequested?.Invoke());
        }

        public void Refresh(LevelData[] levels, int unlockedCount)
        {
            for (int i = 0; i < _levelButtons.Length; i++)
            {
                bool exists = i < levels.Length && levels[i] != null;
                bool unlocked = exists && i < unlockedCount;

                _levelButtons[i].interactable = unlocked;
                if (i < _levelLabels.Length)
                {
                    _levelLabels[i].text = unlocked
                        ? "NIVEL " + (i + 1) + "\n" + levels[i].Title
                        : "NIVEL " + (i + 1) + "\nBloqueado";
                }
            }
        }
    }
}
