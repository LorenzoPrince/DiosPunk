using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Aplica un StageTheme a la escena Gameplay: enciende el fondo y la pista definitivos, apaga los placeholders,
    /// reemplaza los receptáculos y pads por el arte, reubica los rótulos de teclas, cambia el panel de puntaje y activa
    /// los popups de juicio. Se ejecuta una vez al cargar el nivel (la escena se recarga por nivel), no por frame. Sin
    /// tema no hace nada: los niveles sin arte definitivo conservan los visuales placeholder.
    /// </summary>
    public sealed class StageThemeApplier : MonoBehaviour
    {
        [SerializeField] private RhythmTrackController _track;

        [Header("Arte (se enciende con tema)")]
        [SerializeField] private SpriteRenderer _background;
        [SerializeField] private SpriteRenderer _highway;

        [Header("Placeholders (se apagan con tema)")]
        [SerializeField] private GameObject[] _placeholders = new GameObject[0];

        [Header("Por carril")]
        [SerializeField] private SpriteRenderer[] _receptacles = new SpriteRenderer[RhythmInputReader.LaneCount];
        [SerializeField] private SpriteRenderer[] _pads = new SpriteRenderer[RhythmInputReader.LaneCount];
        [SerializeField] private TMP_Text[] _keyLabels = new TMP_Text[RhythmInputReader.LaneCount];

        [Header("HUD")]
        [SerializeField] private Image _scorePanelImage;
        [SerializeField] private RectTransform _scoreText;
        [SerializeField] private RectTransform _multiplierText;
        [SerializeField] private RectTransform _accuracyText;
        [SerializeField] private JudgementPopup _popup;

        [Tooltip("Textos técnicos que el arte reemplaza (el texto de juicio con milisegundos lo sustituyen los popups).")]
        [SerializeField] private TMP_Text[] _hideWhenThemed = new TMP_Text[0];

        [Header("Layout del panel de puntaje con arte (px del canvas 1920x1080)")]
        [SerializeField] private Vector2 _scorePanelSize = new Vector2(420f, 294f);
        [SerializeField] private Vector2 _scoreTextPos = new Vector2(6f, 48f);
        [SerializeField] private Vector2 _multiplierPos = new Vector2(106f, -46f);
        [SerializeField] private Vector2 _accuracyPos = new Vector2(-62f, -4f);

        public bool Applied { get; private set; }

        public void Apply(StageTheme theme)
        {
            if (theme == null) return;
            Applied = true;

            _track.ApplyStage(theme);
            float s = theme.ArtScale;
            float hitY = theme.HitLineY;

            // Fondo (PPU 100, escala 1: cubre la pantalla) y pista en perspectiva.
            Show(_background, theme.Background, Vector3.zero, 1f);
            Show(_highway, theme.Highway, new Vector3(theme.HighwayPosition.x, theme.HighwayPosition.y, 0f), s);

            for (int i = 0; i < _placeholders.Length; i++)
            {
                if (_placeholders[i] != null) _placeholders[i].SetActive(false);
            }

            for (int lane = 0; lane < RhythmInputReader.LaneCount; lane++)
            {
                float x = theme.LaneBottomX[lane];
                Show(_receptacles[lane], theme.Receptacles[lane], new Vector3(x, hitY, 0f), s);

                // El pad de feedback copia la silueta del receptáculo (su color se tiñe al juzgar).
                if (_pads[lane] != null)
                {
                    _pads[lane].sprite = theme.Receptacles[lane];
                    _pads[lane].transform.localPosition = new Vector3(x, hitY, 0f);
                    _pads[lane].transform.localScale = new Vector3(s * 1.04f, s * 1.04f, 1f);
                }

                // Rótulo de tecla SOBRE el receptáculo: blanco, negrita y con contorno negro para que contraste con cualquier
                // color de receptáculo. Se dibuja encima del receptáculo y del pad pero debajo de las notas (que lo cubren al llegar).
                TMP_Text label = _keyLabels[lane];
                if (label != null)
                {
                    label.transform.localPosition = new Vector3(x, hitY + theme.KeyLabelOffsetY, 0f);
                    label.color = Color.white;
                    label.fontStyle = FontStyles.Bold;
                    label.fontSize = 6f;
                    label.outlineWidth = 0.28f;
                    label.outlineColor = new Color32(0, 0, 0, 255);
                    if (label is TextMeshPro worldLabel) worldLabel.sortingOrder = 7;
                }
            }

            ApplyScorePanel(theme);
            if (_popup != null) _popup.Configure(theme);
            for (int i = 0; i < _hideWhenThemed.Length; i++)
            {
                if (_hideWhenThemed[i] != null) _hideWhenThemed[i].enabled = false;
            }
        }

        private static void Show(SpriteRenderer renderer, Sprite sprite, Vector3 position, float scale)
        {
            if (renderer == null || sprite == null) return;
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.transform.localPosition = position;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.gameObject.SetActive(true);
        }

        private void ApplyScorePanel(StageTheme theme)
        {
            if (_scorePanelImage == null || theme.ScorePanel == null) return;

            _scorePanelImage.sprite = theme.ScorePanel;
            _scorePanelImage.type = Image.Type.Simple;
            _scorePanelImage.preserveAspect = true;
            _scorePanelImage.rectTransform.sizeDelta = _scorePanelSize;

            Place(_scoreText, _scoreTextPos, new Vector2(340f, 80f));
            if (_scoreText != null && _scoreText.TryGetComponent(out TMP_Text scoreTmp))
            {
                // El panel de arte es más angosto: el puntaje se autoajusta para no partirse en dos líneas.
                scoreTmp.enableAutoSizing = true;
                scoreTmp.fontSizeMin = 28f;
                scoreTmp.fontSizeMax = 60f;
                scoreTmp.textWrappingMode = TextWrappingModes.NoWrap;
            }
            Place(_multiplierText, _multiplierPos, new Vector2(90f, 56f));
            Place(_accuracyText, _accuracyPos, new Vector2(150f, 40f));
            FitCentered(_multiplierText, 28f, 46f);
            FitCentered(_accuracyText, 20f, 34f);
        }

        // Texto centrado y autoajustado dentro de su rectángulo (el arte deja zonas blancas de forma irregular).
        private static void FitCentered(RectTransform rect, float minSize, float maxSize)
        {
            if (rect == null || !rect.TryGetComponent(out TMP_Text tmp)) return;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // Los textos del panel se centran sobre el arte (anclaje y pivote al centro del panel).
        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
