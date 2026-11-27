using System;
using DiosPunk.Rhythm;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Escena narrativa pre-nivel: muestra las líneas de un DialogueData con efecto mecanografiado, botones
    /// Siguiente/Continuar y Saltar y, al final, las respuestas cortas opcionales que modifican variables de
    /// GameSession. Al terminar avisa con Finished (el flujo pasa a la preparación de la canción).
    /// El Update solo corre mientras se escribe una línea y no genera garbage: el texto se asigna una vez por
    /// línea y la revelación usa TMP_Text.maxVisibleCharacters.
    /// </summary>
    public sealed class PreLevelDialoguePresenter : MonoBehaviour
    {
        private const string AdvanceLabel = "SIGUIENTE";
        private const string FinishLabel = "CONTINUAR";

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _speakerText;
        [SerializeField] private TMP_Text _bodyText;
        [SerializeField] private Image _portrait;
        [SerializeField] private Button _advance;
        [SerializeField] private TMP_Text _advanceLabel;
        [SerializeField] private Button _skip;

        [Header("Respuestas (hasta 3)")]
        [SerializeField] private GameObject _choicesRoot;
        [SerializeField] private Button[] _choiceButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] _choiceLabels = Array.Empty<TMP_Text>();

        [Header("Mecanografiado")]
        [SerializeField, Min(1f)] private float _charsPerSecond = 45f;
        [Tooltip("Cada cuántos caracteres revelados suena la birome (#44 a #48).")]
        [SerializeField, Min(1)] private int _charsPerPenSound = 4;
        [Tooltip("Volumen del sonido de tipeo (multiplica al de SfxLibrary): bajo para que no sature al repetirse.")]
        [SerializeField, Range(0.05f, 1f)] private float _penVolume = 0.14f;

        private DialogueData _data;
        private int _index;
        private bool _typing;
        private bool _finished;
        private float _revealed;
        private int _totalChars;
        private int _lastPenChar;

        // true desde el instante en que se decide cerrar (Saltar, Continuar, respuesta, ocultar o destruir el panel):
        // desde ahí el tipeo no puede disparar ni un trazo más, ni siquiera en el mismo frame.
        private bool _isClosing;

        /// <summary>Se terminó, se saltó o se eligió una respuesta.</summary>
        public event Action Finished;

        private void Awake()
        {
            _advance.onClick.AddListener(OnAdvance);
            _skip.onClick.AddListener(Finish);
            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                int index = i;
                _choiceButtons[i].onClick.AddListener(() => Choose(index));
            }
        }

        // Al ocultarse el panel (cambio de pantalla, carga de escena) se corta cualquier trazo pendiente.
        private void OnDisable() => CloseTyping();

        private void OnDestroy() => CloseTyping();

        /// <summary>Apaga el tipeo ANTES de cualquier cambio de estado y corta la birome (y el hover que pudiera estar sonando).</summary>
        private void CloseTyping()
        {
            _isClosing = true;
            _typing = false;
            SoundEffectManager.Stop(SfxId.Pen);
            SoundEffectManager.Stop(SfxId.UiHover);
        }

        public void Begin(LevelData level) => BeginDialogue(level.Title, level.Dialogue);

        /// <summary>Muestra cualquier DialogueData (escena pre-nivel o epílogo) bajo el título indicado.</summary>
        public void BeginDialogue(string title, DialogueData data)
        {
            GameSession.ResetNarrativeChoices();

            _data = data;
            _index = 0;
            _finished = false;
            _isClosing = false; // diálogo nuevo: el tipeo vuelve a estar permitido
            _titleText.text = title;
            _choicesRoot.SetActive(false);
            _advance.gameObject.SetActive(true);

            if (_data == null || _data.Lines.Length == 0)
            {
                ShowText(string.Empty, "(sin dialogo)", null);
                _typing = false;
                RefreshAdvanceLabel();
                return;
            }

            ShowLine();
        }

        private void Update()
        {
            if (!_typing || _isClosing) return;

            _revealed += Time.unscaledDeltaTime * _charsPerSecond;
            int visible = (int)_revealed;
            if (visible >= _totalChars) CompleteLine();
            else
            {
                _bodyText.maxVisibleCharacters = visible;

                // Birome al escribir: un trazo cada _charsPerPenSound letras nuevas (sin sonar en los espacios).
                if (visible - _lastPenChar >= _charsPerPenSound)
                {
                    _lastPenChar = visible;
                    SoundEffectManager.Play(SfxId.Pen, _penVolume); // el pitch varía ±0,05 por la entrada de SfxLibrary
                }
            }
        }

        private void ShowLine()
        {
            DialogueLine line = _data.Lines[_index];
            ShowText(line.speaker, line.text, line.portrait);
            RefreshAdvanceLabel();
        }

        private void ShowText(string speaker, string text, Sprite portrait)
        {
            _speakerText.text = speaker;

            _portrait.sprite = portrait;
            _portrait.enabled = portrait != null;

            _bodyText.text = text;
            _bodyText.maxVisibleCharacters = 0;
            _bodyText.ForceMeshUpdate();
            _totalChars = _bodyText.textInfo.characterCount;
            _revealed = 0f;
            _lastPenChar = 0;
            _typing = _totalChars > 0;
            if (!_typing) _bodyText.maxVisibleCharacters = int.MaxValue;
        }

        private void CompleteLine()
        {
            _typing = false;
            SoundEffectManager.Stop(SfxId.Pen); // la línea se completó de golpe: se corta el trazo
            _bodyText.maxVisibleCharacters = int.MaxValue;
        }

        private void OnAdvance()
        {
            if (_finished) return;

            if (_typing)
            {
                CompleteLine(); // primer clic: completa la línea; el siguiente avanza
                return;
            }

            _index++;
            SoundEffectManager.Play(SfxId.PageTurn); // cambio de hoja al avanzar el diálogo
            if (_data != null && _index < _data.Lines.Length) ShowLine();
            else if (_data != null && _data.Choices.Length > 0) ShowChoices();
            else Finish();
        }

        private void RefreshAdvanceLabel()
        {
            bool last = _data == null || _index >= _data.Lines.Length - 1;
            _advanceLabel.text = last && (_data == null || _data.Choices.Length == 0) ? FinishLabel : AdvanceLabel;
        }

        private void ShowChoices()
        {
            DialogueChoice[] choices = _data.Choices;
            _advance.gameObject.SetActive(false);
            _choicesRoot.SetActive(true);
            _speakerText.text = _data.ChoicePrompt;
            _portrait.enabled = false;

            for (int i = 0; i < _choiceButtons.Length; i++)
            {
                bool used = i < choices.Length;
                _choiceButtons[i].gameObject.SetActive(used);
                if (used) _choiceLabels[i].text = choices[i].label;
            }
        }

        private void Choose(int index)
        {
            if (_finished || _data == null || index >= _data.Choices.Length) return;

            DialogueChoice choice = _data.Choices[index];
            GameSession.ApplyChoice(choice.variable, choice.value);
            Finish();
        }

        private void Finish()
        {
            if (_finished) return; // evita doble disparo por doble clic
            _finished = true;
            CloseTyping(); // Continuar / Saltar / respuesta: primero se apaga el tipeo, después se avisa al flujo
            _choicesRoot.SetActive(false);
            Finished?.Invoke();
        }
    }
}
