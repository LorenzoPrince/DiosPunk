using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>Escena narrativa breve previa a la canción: avanza línea por línea o se salta.</summary>
    public sealed class PreLevelScreen : MonoBehaviour
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _speakerText;
        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private Button _advance;
        [SerializeField] private Button _skip;

        private LevelData _level;
        private int _index;

        /// <summary>Se terminó o se saltó la escena.</summary>
        public event Action Finished;

        private void Awake()
        {
            _advance.onClick.AddListener(Advance);
            _skip.onClick.AddListener(() => Finished?.Invoke());
        }

        public void Begin(LevelData level)
        {
            _level = level;
            _index = 0;
            _titleText.text = level.Title;
            ShowLine();
        }

        private void Advance()
        {
            _index++;
            if (_level == null || _index >= _level.Dialogue.Length) Finished?.Invoke();
            else ShowLine();
        }

        private void ShowLine()
        {
            DialogueLine[] lines = _level.Dialogue;
            if (_index < lines.Length)
            {
                _speakerText.text = lines[_index].speaker;
                _bodyText.text = lines[_index].text;
            }
            else
            {
                _speakerText.text = string.Empty;
                _bodyText.text = "(sin dialogo)";
            }
        }
    }
}
