using System;
using DiosPunk.Rhythm;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Opciones con tabs: Audio, Video, Controles y Accesibilidad. En la escena MainMenu la pestaña de Audio
    /// incluye "Recalibrar latencia", que abre la calibración sin reiniciar el progreso. Los ajustes aún no se persisten
    /// (salvo la latencia, que se guarda en progress.json al recalibrar).
    /// </summary>
    public sealed class OptionsScreen : MonoBehaviour
    {
        [SerializeField] private Button[] _tabButtons = Array.Empty<Button>();
        [SerializeField] private GameObject[] _tabPages = Array.Empty<GameObject>();
        [SerializeField] private Slider _volume;
        [Tooltip("Volumen de efectos de sonido (GameSettings.SfxVolume), independiente del volumen general.")]
        [SerializeField] private Slider _sfxVolume;
        [Header("Controles")]
        [SerializeField] private Button _schemePrev;
        [SerializeField] private Button _schemeNext;
        [SerializeField] private TMP_Text _schemeValue;
        [SerializeField] private TMP_Text _schemeDetail;
        [Tooltip("Texto de ayuda de la pestaña Controles; se actualiza con el esquema elegido.")]
        [SerializeField] private TMP_Text _controlsHelp;

        [SerializeField] private Toggle _fullscreen;
        [SerializeField] private Toggle _reduceEffects;
        [Tooltip("Modo Inmortal / No Fail: la vida no baja de 1 HP. Se guarda en progress.json.")]
        [SerializeField] private Toggle _immortal;
        [SerializeField] private Button _back;

        [Header("Recalibración (solo en la escena MainMenu)")]
        [SerializeField] private Button _recalibrate;
        [SerializeField] private TMP_Text _latencyLabel;

        [Header("Ajuste fino de latencia (ambas escenas)")]
        [SerializeField] private Button _latencyMinus;
        [SerializeField] private Button _latencyPlus;
        [SerializeField] private Button _latencyRestore;

        private const float LatencyStepMs = 5f;

        public event Action BackRequested;
        public event Action RecalibrateRequested;

        private void Awake()
        {
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                _tabButtons[i].onClick.AddListener(() => ShowTab(index));
            }

            _volume.onValueChanged.AddListener(value => AudioListener.volume = value);
            if (_sfxVolume != null) _sfxVolume.onValueChanged.AddListener(OnSfxVolumeChanged);
            _fullscreen.onValueChanged.AddListener(value => Screen.fullScreen = value);
            _reduceEffects.onValueChanged.AddListener(value => GameSettings.ReduceVisualEffects = value);
            _immortal.onValueChanged.AddListener(GameSession.SetImmortalMode);
            _back.onClick.AddListener(() => BackRequested?.Invoke());
            if (_recalibrate != null) _recalibrate.onClick.AddListener(() => RecalibrateRequested?.Invoke());

            _latencyMinus.onClick.AddListener(() => GameSession.SetLatencyOffset(GameSettings.LatencyOffsetMs - LatencyStepMs));
            _latencyPlus.onClick.AddListener(() => GameSession.SetLatencyOffset(GameSettings.LatencyOffsetMs + LatencyStepMs));
            _latencyRestore.onClick.AddListener(() => GameSession.SetLatencyOffset(GameSettings.DefaultLatencyOffsetMs));

            _schemePrev.onClick.AddListener(() => CycleScheme(-1));
            _schemeNext.onClick.AddListener(() => CycleScheme(1));

            // Flechas Izquierda/Derecha cambian el esquema mientras la pestaña Controles está visible.
            _schemeLeft = new InputAction("SchemeLeft", InputActionType.Button, "<Keyboard>/leftArrow");
            _schemeLeft.performed += _ => CycleScheme(-1);
            _schemeRight = new InputAction("SchemeRight", InputActionType.Button, "<Keyboard>/rightArrow");
            _schemeRight.performed += _ => CycleScheme(1);
        }

        private const int ControlsTabIndex = 2;
        private InputAction _schemeLeft;
        private InputAction _schemeRight;

        private void CycleScheme(int step)
        {
            // Aplica, notifica (input y rótulos de la pista) y guarda en progress.json de inmediato.
            GameSession.SetControlScheme(ControlSchemes.Cycle(GameSettings.ControlScheme, step));
            RefreshScheme();
        }

        private void RefreshScheme()
        {
            ControlScheme scheme = GameSettings.ControlScheme;
            _schemeValue.text = ControlSchemes.GetName(scheme);
            _schemeDetail.text = ControlSchemes.Describe(scheme);
            _controlsHelp.text = "Pausa: Escape\nDebug: F1 Auto-Play, F2 reiniciar, F3 pausa, F4 modo, F5/P Modo Punk, F6/B debuff, V Inmortal, +/- latencia";
        }

        private void RefreshLatency(float latency)
        {
            if (_latencyLabel == null) return;
            _latencyLabel.text = "Latencia actual: " + (latency >= 0f ? "+" : string.Empty) + latency.ToString("F0") + " ms";
        }

        private void OnDisable()
        {
            GameSettings.LatencyOffsetMsChanged -= RefreshLatency;
            _schemeLeft.Disable();
            _schemeRight.Disable();
        }

        private void OnDestroy()
        {
            _schemeLeft?.Dispose();
            _schemeRight?.Dispose();
        }

        private void OnEnable()
        {
            _volume.SetValueWithoutNotify(AudioListener.volume);
            if (_sfxVolume != null) _sfxVolume.SetValueWithoutNotify(GameSettings.SfxVolume);
            _fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
            _reduceEffects.SetIsOnWithoutNotify(GameSettings.ReduceVisualEffects);
            _immortal.SetIsOnWithoutNotify(GameSettings.ImmortalMode);

            GameSettings.LatencyOffsetMsChanged += RefreshLatency;
            RefreshLatency(GameSettings.LatencyOffsetMs);

            RefreshScheme();
            ShowTab(0);
        }

        // Al arrastrar el slider se oye un clic de muestra, como mucho cada 0.15 s para no saturar las voces.
        private const float PreviewIntervalSeconds = 0.15f;
        private float _lastPreviewTime = -1f;

        private void OnSfxVolumeChanged(float value)
        {
            GameSettings.SfxVolume = value;
            if (Time.unscaledTime - _lastPreviewTime < PreviewIntervalSeconds) return;
            _lastPreviewTime = Time.unscaledTime;
            SoundEffectManager.Play(SfxId.HitPerfect);
        }

        private void ShowTab(int index)
        {
            for (int i = 0; i < _tabPages.Length; i++) _tabPages[i].SetActive(i == index);

            if (index == ControlsTabIndex) { _schemeLeft.Enable(); _schemeRight.Enable(); }
            else { _schemeLeft.Disable(); _schemeRight.Disable(); }
        }
    }
}
