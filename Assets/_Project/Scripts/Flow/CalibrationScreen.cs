using System;
using DiosPunk.Rhythm;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Pantalla de primera partida: calibración de latencia con un metrónomo visual y sonoro (el jugador pulsa
    /// una tecla al ritmo; la media del desfase es el LatencyOffsetMs recomendado) y mini tutorial de controles
    /// (validar los 4 carriles). Los clics se programan con AudioSource.PlayScheduled sobre dspTime y los
    /// timestamps de las pulsaciones (ctx.time) se convierten a tiempo DSP con DspRealtimeMapper.
    /// El Update solo corre en la fase de calibración (programar clics y animar el pulso) y no genera garbage.
    /// </summary>
    public sealed class CalibrationScreen : MonoBehaviour
    {
        private enum Phase { Intro, Calibrating, Result, Tutorial }

        [Header("Paneles por fase")]
        [SerializeField] private GameObject _introPanel;
        [SerializeField] private GameObject _calibratingPanel;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private GameObject _tutorialPanel;

        [Header("Botones")]
        [SerializeField] private Button _startCalibration;
        [SerializeField] private Button _retry;
        [SerializeField] private Button _accept;
        [SerializeField] private Button _finishTutorial;
        [SerializeField] private Button _skip;
        [SerializeField] private Button _back;

        [Header("Calibración")]
        [SerializeField] private Image _pulse;
        [SerializeField] private TMP_Text _progressText;
        [SerializeField] private TMP_Text _resultText;
        [SerializeField] private AudioSource _clickSourceA;
        [SerializeField] private AudioSource _clickSourceB;
        [SerializeField] private AudioClip _clickClip;
        [SerializeField, Min(40f)] private float _bpm = 100f;
        [SerializeField, Min(4)] private int _targetPresses = 10;
        [Tooltip("Pausa antes del primer clic.")]
        [SerializeField, Min(0.5f)] private float _leadInSeconds = 1.5f;

        [Header("Tutorial de controles")]
        [SerializeField] private Image[] _laneBoxes = new Image[RhythmInputReader.LaneCount];
        [SerializeField] private TMP_Text _tutorialStatus;

        private readonly DspRealtimeMapper _mapper = new DspRealtimeMapper();
        private InputAction _anyKey;
        private InputAction[] _laneActions;
        private readonly bool[] _laneDone = new bool[RhythmInputReader.LaneCount];
        private Color[] _laneColors;

        private Phase _phase;
        private double _startDsp;
        private double _interval;
        private int _nextBeat;
        private int _presses;
        private double _sum;
        private double _sumSquares;
        private float _resultOffsetMs;
        private int _lanesDone;

        /// <summary>Calibración aceptada y tutorial terminado: latencia en ms recomendada.</summary>
        public event Action<float> Completed;

        /// <summary>El jugador eligió omitir la calibración y el tutorial.</summary>
        public event Action SkipRequested;

        public event Action BackRequested;

        private void Awake()
        {
            _laneColors = LaneStyle.CreateDefaultColors();

            _startCalibration.onClick.AddListener(StartCalibration);
            _retry.onClick.AddListener(StartCalibration);
            _accept.onClick.AddListener(AcceptCalibration);
            _finishTutorial.onClick.AddListener(() => Completed?.Invoke(_resultOffsetMs));
            _skip.onClick.AddListener(() => SkipRequested?.Invoke());
            _back.onClick.AddListener(() => BackRequested?.Invoke());

            _anyKey = new InputAction("CalibrationPress", InputActionType.Button, "<Keyboard>/anyKey");
            _anyKey.performed += ctx => RegisterPress(ctx.time);

            _laneActions = new InputAction[RhythmInputReader.LaneCount];
            for (int i = 0; i < _laneActions.Length; i++)
            {
                int lane = i;
                var action = new InputAction("TutorialLane" + lane, InputActionType.Button);
                action.AddBinding(RhythmInputReader.PrimaryKeys[lane]);
                action.AddBinding(RhythmInputReader.ArrowKeys[lane]);
                action.performed += _ => RegisterLanePress(lane);
                _laneActions[i] = action;
            }
        }

        private void OnEnable()
        {
            _mapper.Reset();
            ShowPhase(Phase.Intro);
        }

        private void OnDisable()
        {
            _anyKey.Disable();
            for (int i = 0; i < _laneActions.Length; i++) _laneActions[i].Disable();
            if (_clickSourceA != null) _clickSourceA.Stop();
            if (_clickSourceB != null) _clickSourceB.Stop();
        }

        private void OnDestroy()
        {
            _anyKey?.Dispose();
            if (_laneActions == null) return;
            for (int i = 0; i < _laneActions.Length; i++) _laneActions[i].Dispose();
        }

        // ---------- Fases ----------

        private void ShowPhase(Phase phase)
        {
            _phase = phase;
            _introPanel.SetActive(phase == Phase.Intro);
            _calibratingPanel.SetActive(phase == Phase.Calibrating);
            _resultPanel.SetActive(phase == Phase.Result);
            _tutorialPanel.SetActive(phase == Phase.Tutorial);

            if (phase == Phase.Calibrating) _anyKey.Enable(); else _anyKey.Disable();
            for (int i = 0; i < _laneActions.Length; i++)
            {
                if (phase == Phase.Tutorial) _laneActions[i].Enable(); else _laneActions[i].Disable();
            }
        }

        private void StartCalibration()
        {
            _interval = 60.0 / _bpm;
            _mapper.Sample();
            _startDsp = AudioSettings.dspTime + _leadInSeconds;
            _nextBeat = 0;
            _presses = 0;
            _sum = 0.0;
            _sumSquares = 0.0;
            _progressText.text = "Pulsa una tecla al ritmo del metronomo: 0 / " + _targetPresses;
            ShowPhase(Phase.Calibrating);
        }

        private void AcceptCalibration()
        {
            GameSettings.LatencyOffsetMs = _resultOffsetMs; // vigente ya para el resto de la sesión
            StartTutorial();
        }

        private void StartTutorial()
        {
            _lanesDone = 0;
            for (int i = 0; i < _laneDone.Length; i++)
            {
                _laneDone[i] = false;
                Color c = _laneColors[i];
                c.a = 0.35f;
                _laneBoxes[i].color = c;
            }
            _finishTutorial.interactable = false;
            _tutorialStatus.text = "Pulsa cada carril (A S D F o flechas): 0 / " + _laneDone.Length;
            ShowPhase(Phase.Tutorial);
        }

        // ---------- Calibración ----------

        private void Update()
        {
            if (_phase != Phase.Calibrating) return;

            _mapper.Sample();
            double now = AudioSettings.dspTime;

            // Programa los próximos clics con anticipación, alternando dos AudioSource para que no se pisen.
            while (_startDsp + _nextBeat * _interval < now + 0.5)
            {
                AudioSource source = (_nextBeat & 1) == 0 ? _clickSourceA : _clickSourceB;
                source.clip = _clickClip;
                source.PlayScheduled(_startDsp + _nextBeat * _interval);
                _nextBeat++;
            }

            // Pulso visual sincronizado con dspTime.
            double beatPosition = (now - _startDsp) / _interval;
            float scale = 1f;
            if (beatPosition >= 0.0)
            {
                float fraction = (float)(beatPosition - Math.Floor(beatPosition));
                scale = 1f + 0.4f * Mathf.Max(0f, 1f - fraction * 4f);
            }
            _pulse.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Instante DSP del primer clic de la calibración en curso.</summary>
        public double ClickStartDsp => _startDsp;

        /// <summary>Segundos entre clics.</summary>
        public double BeatInterval => _interval;

        /// <summary>
        /// Registra una pulsación de calibración con su timestamp de input (línea de tiempo de realtimeSinceStartup).
        /// Lo llama la InputAction y también se puede invocar para probar el cálculo.
        /// </summary>
        public void RegisterPress(double inputRealtime)
        {
            if (_phase != Phase.Calibrating) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.isPressed) return;

            _mapper.Sample();
            double relative = _mapper.RealtimeToDsp(inputRealtime) - _startDsp;
            if (relative < -_interval * 0.25) return; // antes de empezar

            double deltaMs = (relative - Math.Round(relative / _interval) * _interval) * 1000.0;
            _presses++;
            _sum += deltaMs;
            _sumSquares += deltaMs * deltaMs;
            _progressText.text = "Pulsa una tecla al ritmo del metronomo: " + _presses + " / " + _targetPresses;

            if (_presses >= _targetPresses) FinishCalibration();
        }

        private void FinishCalibration()
        {
            double mean = _sum / _presses;
            double variance = Math.Max(0.0, _sumSquares / _presses - mean * mean);
            double deviation = Math.Sqrt(variance);

            _resultOffsetMs = Mathf.Clamp((float)mean, -300f, 300f);

            string sign = _resultOffsetMs >= 0f ? "+" : string.Empty;
            string text = "Desfase medio: " + mean.ToString("F1") + " ms (desvio " + deviation.ToString("F1") + " ms)\n" +
                          "Latencia recomendada: " + sign + _resultOffsetMs.ToString("F0") + " ms";
            if (deviation > 40.0) text += "\nLas pulsaciones fueron irregulares: conviene repetir.";
            _resultText.text = text;

            ShowPhase(Phase.Result);
        }

        // ---------- Tutorial ----------

        /// <summary>Registra la pulsación de un carril en el tutorial (la llaman las InputActions y las pruebas).</summary>
        public void RegisterLanePress(int lane)
        {
            if (_phase != Phase.Tutorial) return;

            // Feedback inmediato en cada pulsación; se cuenta solo la primera vez por carril.
            _laneBoxes[lane].color = _laneColors[lane];
            if (_laneDone[lane]) return;

            _laneDone[lane] = true;
            _lanesDone++;
            if (_lanesDone >= _laneDone.Length)
            {
                _tutorialStatus.text = "Listo! Los 4 carriles funcionan.";
                _finishTutorial.interactable = true;
            }
            else
            {
                _tutorialStatus.text = "Pulsa cada carril (A S D F o flechas): " + _lanesDone + " / " + _laneDone.Length;
            }
        }
    }
}
