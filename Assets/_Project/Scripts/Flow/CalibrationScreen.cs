using System;
using DiosPunk.Rhythm;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    /// <summary>
    /// Calibración de latencia con un metrónomo visual y sonoro (el jugador pulsa una tecla al ritmo; la media
    /// del desfase es el LatencyOffsetMs recomendado) y, en la primera partida, un mini tutorial de controles
    /// (validar los 4 carriles). Los clics se programan con AudioSource.PlayScheduled sobre dspTime y los
    /// timestamps de las pulsaciones (ctx.time) se convierten a tiempo DSP con la API de SongClock
    /// (InputRealtimeToDsp), la misma conversión que usa el juicio en juego.
    /// Modo recalibración (desde Opciones): sin tutorial y sin tocar el progreso; al aceptar devuelve la latencia.
    /// El Update solo corre en la fase de calibración (programar clics y animar el pulso) y no genera garbage.
    /// </summary>
    public sealed class CalibrationScreen : MonoBehaviour
    {
        private enum Phase { Intro, Calibrating, Result, Tutorial }

        [Header("Reloj")]
        [Tooltip("SongClock de la escena: provee la conversión de timestamps de input a tiempo DSP.")]
        [SerializeField] private SongClock _clock;

        [Header("Paneles por fase")]
        [SerializeField] private GameObject _introPanel;
        [SerializeField] private GameObject _calibratingPanel;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private GameObject _tutorialPanel;

        [Header("Botones")]
        [SerializeField] private Button _startCalibration;
        [SerializeField] private Button _retry;
        [SerializeField] private Button _accept;
        [SerializeField] private TMP_Text _acceptLabel;
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
        [Tooltip("Golpes válidos que se promedian (6 a 8), sin contar los de adaptación.")]
        [SerializeField, Range(6, 8)] private int _targetPresses = 8;
        [Tooltip("Pausa antes del primer clic.")]
        [SerializeField, Min(0.5f)] private float _leadInSeconds = 1.5f;

        [Header("Indicador rítmico y ajuste fino")]
        [Tooltip("Marcador que viaja hacia la línea de impacto y la toca justo con cada clic del metrónomo.")]
        [SerializeField] private RectTransform _marker;
        [SerializeField] private float _markerFromX = -420f;
        [Tooltip("X de la línea de impacto (donde el marcador coincide con el clic).")]
        [SerializeField] private float _markerToX = 300f;
        [SerializeField] private TMP_Text _offsetText;
        [SerializeField] private Button _offsetMinus;
        [SerializeField] private Button _offsetPlus;
        [SerializeField] private Button _offsetRestore;

        [Header("Tutorial de controles")]
        [SerializeField] private Image[] _laneBoxes = new Image[RhythmInputReader.LaneCount];
        [Tooltip("Rótulo de tecla de cada carril; refleja el esquema elegido en Opciones.")]
        [SerializeField] private TMP_Text[] _laneKeyLabels = new TMP_Text[RhythmInputReader.LaneCount];
        [SerializeField] private TMP_Text _tutorialStatus;

        private const string AcceptLabelNewGame = "ACEPTAR";
        private const string AcceptLabelRecalibrate = "GUARDAR";

        private InputAction _space;
        private LaneInputSet _laneSet;
        private int _adaptationDone;
        private float _lastDeltaMs;
        private readonly bool[] _laneDone = new bool[RhythmInputReader.LaneCount];
        private Color[] _laneColors;

        private Phase _phase;
        private bool _recalibration;
        private double _startDsp;
        private double _interval;
        private int _nextBeat;
        private int _presses;
        private double[] _deltas;       // desfases aceptados (ms), preasignado
        private double[] _scratch;      // copia ordenable para la mediana
        private int _rejected;          // pulsaciones descartadas por extremas (> OutlierMs)
        private float _resultOffsetMs;

        // Pulsación a más de 250 ms del clic más cercano: casi seguro un toque accidental, no latencia.
        private const double OutlierMs = 250.0;
        // Tras la mediana se descarta lo que quede a más de esto de ella (pulsaciones despistadas).
        private const double TrimMs = 80.0;
        // Los primeros golpes son de adaptación al ritmo: no cuentan.
        private const int AdaptationPresses = 2;
        // Con menos golpes consistentes que esto el resultado no es confiable y hay que repetir.
        private const int MinKept = 6;
        private int _lanesDone;

        /// <summary>true si se abrió desde Opciones (sin tutorial, sin tocar el progreso).</summary>
        public bool IsRecalibration => _recalibration;

        /// <summary>Calibración aceptada (y tutorial terminado, si corresponde): latencia en ms recomendada.</summary>
        public event Action<float> Completed;

        /// <summary>El jugador eligió omitir la calibración y el tutorial (solo primera partida).</summary>
        public event Action SkipRequested;

        public event Action BackRequested;

        /// <summary>Instante DSP del primer clic de la calibración en curso.</summary>
        public double ClickStartDsp => _startDsp;

        /// <summary>Segundos entre clics.</summary>
        public double BeatInterval => _interval;

        /// <summary>Llamar antes de mostrar la pantalla: true = recalibrar desde Opciones; false = primera partida.</summary>
        public void SetRecalibrationMode(bool recalibration)
        {
            _recalibration = recalibration;
            _skip.gameObject.SetActive(!recalibration);
            _acceptLabel.text = recalibration ? AcceptLabelRecalibrate : AcceptLabelNewGame;
        }

        private void Awake()
        {
            _laneColors = LaneStyle.CreateDefaultColors();
            _deltas = new double[Math.Max(_targetPresses, MinKept)];
            _scratch = new double[_deltas.Length];

            _startCalibration.onClick.AddListener(StartCalibration);
            _retry.onClick.AddListener(StartCalibration);
            _accept.onClick.AddListener(AcceptCalibration);
            _offsetMinus.onClick.AddListener(() => AdjustResult(-5f));
            _offsetPlus.onClick.AddListener(() => AdjustResult(5f));
            _offsetRestore.onClick.AddListener(() => SetResult(GameSettings.DefaultLatencyOffsetMs));
            _finishTutorial.onClick.AddListener(() => Completed?.Invoke(_resultOffsetMs));
            _skip.onClick.AddListener(() => SkipRequested?.Invoke());
            _back.onClick.AddListener(() => BackRequested?.Invoke());

            // Calibración: cualquier tecla de carril del esquema elegido o la Barra Espaciadora.
            _space = new InputAction("CalibrationSpace", InputActionType.Button, "<Keyboard>/space");
            _space.performed += ctx => RegisterPress(ctx.time);

            // El mismo LaneInputSet (esquema elegido) sirve a la calibración (cualquier carril) y al tutorial (cada carril).
            _laneSet = new LaneInputSet((lane, time) =>
            {
                if (_phase == Phase.Calibrating) RegisterPress(time);
                else RegisterLanePress(lane);
            });
        }

        // ---------- Ajuste fino de la latencia resultante ----------

        private void AdjustResult(float deltaMs) => SetResult(_resultOffsetMs + deltaMs);

        private void SetResult(float offsetMs)
        {
            _resultOffsetMs = Mathf.Clamp(offsetMs, -300f, 300f);
            _accept.interactable = true; // un ajuste manual deliberado habilita aceptar aunque la medición no fuera confiable
            RefreshOffsetText();
        }

        private void RefreshOffsetText()
        {
            _offsetText.text = "Latencia: " + (_resultOffsetMs >= 0f ? "+" : string.Empty) + _resultOffsetMs.ToString("F0") + " ms";
        }

        private void OnEnable()
        {
            _clock.ResetTimebase();
            ShowPhase(Phase.Intro);
        }

        private void OnDisable()
        {
            _space.Disable();
            _laneSet.Disable();
            if (_clickSourceA != null) _clickSourceA.Stop();
            if (_clickSourceB != null) _clickSourceB.Stop();
        }

        private void OnDestroy()
        {
            _space?.Dispose();
            _laneSet?.Dispose();
        }

        // ---------- Fases ----------

        private void ShowPhase(Phase phase)
        {
            _phase = phase;
            _introPanel.SetActive(phase == Phase.Intro);
            _calibratingPanel.SetActive(phase == Phase.Calibrating);
            _resultPanel.SetActive(phase == Phase.Result);
            _tutorialPanel.SetActive(phase == Phase.Tutorial);

            if (phase == Phase.Calibrating) _space.Enable(); else _space.Disable();
            if (phase == Phase.Calibrating || phase == Phase.Tutorial) _laneSet.Enable(); else _laneSet.Disable();
        }

        private void StartCalibration()
        {
            _interval = 60.0 / _bpm;
            _clock.SampleNow();
            _startDsp = AudioSettings.dspTime + _leadInSeconds;
            _nextBeat = 0;
            _presses = 0;
            _rejected = 0;
            _adaptationDone = 0;
            _progressText.text = "Adaptacion (no cuenta): 0 / " + AdaptationPresses;
            ShowPhase(Phase.Calibrating);
        }

        private void AcceptCalibration()
        {
            GameSettings.LatencyOffsetMs = _resultOffsetMs; // vigente ya para el resto de la sesión

            // Recalibrar desde Opciones: se guarda y se vuelve; la primera partida sigue con el tutorial.
            if (_recalibration) Completed?.Invoke(_resultOffsetMs);
            else StartTutorial();
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

            // Rótulos de tecla del esquema elegido.
            ControlScheme scheme = GameSettings.ControlScheme;
            for (int i = 0; i < _laneKeyLabels.Length && i < RhythmInputReader.LaneCount; i++)
            {
                if (_laneKeyLabels[i] != null) _laneKeyLabels[i].text = ControlSchemes.GetLaneLabel(scheme, i);
            }
            _tutorialStatus.text = TutorialProgressText();
            ShowPhase(Phase.Tutorial);
        }

        // ---------- Calibración ----------

        private void Update()
        {
            if (_phase != Phase.Calibrating) return;

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

            // Marcador: recorre la pista en un beat y toca la línea de impacto justo cuando suena cada clic
            // (la fracción del beat sale de dspTime, la misma base que programa el audio).
            float phase = (float)(beatPosition - Math.Floor(beatPosition));
            Vector2 p = _marker.anchoredPosition;
            p.x = Mathf.Lerp(_markerFromX, _markerToX, phase);
            _marker.anchoredPosition = p;
        }

        /// <summary>
        /// Registra una pulsación de calibración con su timestamp de input (línea de tiempo de realtimeSinceStartup).
        /// Lo llama la InputAction y también se puede invocar para probar el cálculo.
        /// </summary>
        public void RegisterPress(double inputRealtime)
        {
            if (_phase != Phase.Calibrating) return;

            _clock.SampleNow();
            double relative = _clock.InputRealtimeToDsp(inputRealtime) - _startDsp;
            if (relative < -_interval * 0.25) return; // antes de empezar

            double deltaMs = (relative - Math.Round(relative / _interval) * _interval) * 1000.0;
            _lastDeltaMs = (float)deltaMs;

            // Los primeros golpes son de adaptación: se muestran pero no entran en el promedio.
            if (_adaptationDone < AdaptationPresses)
            {
                _adaptationDone++;
                _progressText.text = _adaptationDone < AdaptationPresses
                    ? "Adaptacion (no cuenta): " + _adaptationDone + " / " + AdaptationPresses
                    : "Ahora cuentan los golpes: 0 / " + _targetPresses;
                return;
            }

            // Outliers extremos: no cuentan para la media ni para el objetivo de pulsaciones.
            if (Math.Abs(deltaMs) > OutlierMs)
            {
                _rejected++;
                _progressText.text = "Golpe muy fuera de ritmo (ignorado): " + _presses + " / " + _targetPresses;
                return;
            }

            _deltas[_presses] = deltaMs;
            _presses++;
            _progressText.text = "Golpes: " + _presses + " / " + _targetPresses + "  (ultimo " +
                                 (_lastDeltaMs >= 0f ? "+" : string.Empty) + _lastDeltaMs.ToString("F0") + " ms)";

            if (_presses >= _targetPresses) FinishCalibration();
        }

        private void FinishCalibration()
        {
            // Estimador robusto: mediana de los desfases y media recortada alrededor de ella (descarta las
            // pulsaciones a más de TrimMs de la mediana). Si quedaran muy pocas se usan todas.
            Array.Copy(_deltas, _scratch, _presses);
            Array.Sort(_scratch, 0, _presses);
            double median = (_presses & 1) == 1
                ? _scratch[_presses / 2]
                : (_scratch[_presses / 2 - 1] + _scratch[_presses / 2]) * 0.5;

            int kept = 0;
            double sum = 0.0;
            for (int i = 0; i < _presses; i++)
            {
                if (Math.Abs(_deltas[i] - median) > TrimMs) continue;
                sum += _deltas[i];
                kept++;
            }
            bool useAll = kept < MinKept;
            if (useAll)
            {
                kept = _presses;
                sum = 0.0;
                for (int i = 0; i < _presses; i++) sum += _deltas[i];
            }

            double mean = sum / kept;
            double sq = 0.0;
            for (int i = 0; i < _presses; i++)
            {
                if (!useAll && Math.Abs(_deltas[i] - median) > TrimMs) continue;
                double d = _deltas[i] - mean;
                sq += d * d;
            }
            double deviation = Math.Sqrt(sq / kept);

            _resultOffsetMs = Mathf.Clamp((float)mean, -300f, 300f);

            int discarded = _rejected + (_presses - kept);
            string sign = _resultOffsetMs >= 0f ? "+" : string.Empty;
            string text = "Desfase medio: " + mean.ToString("F1") + " ms (desvio " + deviation.ToString("F1") + " ms)\n" +
                          "Latencia recomendada: " + sign + _resultOffsetMs.ToString("F0") + " ms";
            if (discarded > 0) text += "\nSe descartaron " + discarded + " pulsaciones fuera de ritmo.";
            if (deviation > 40.0) text += "\nLas pulsaciones fueron irregulares: conviene repetir.";

            // Con menos de MinKept golpes consistentes no se confía en la medición: hay que repetir (o ajustar a mano).
            bool reliable = !useAll;
            if (!reliable) text += "\nSolo " + kept + " de " + _presses + " golpes fueron consistentes: repeti la calibracion.";
            _resultText.text = text;
            _accept.interactable = reliable;
            RefreshOffsetText();

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
                _tutorialStatus.text = TutorialProgressText();
            }
        }

        private string TutorialProgressText()
        {
            return "Pulsa cada carril (" + ControlSchemes.Describe(GameSettings.ControlScheme) + "): " + _lanesDone + " / " + _laneDone.Length;
        }
    }
}
