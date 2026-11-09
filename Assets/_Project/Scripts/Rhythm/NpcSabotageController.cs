using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Justificación visual de los debuffs (GDD): un NPC saboteador a un costado de la pista arroja un proyectil
    /// saturado hacia la base. Si el Modo Punk está activo, el proyectil choca contra el "Escudo Punk" y se
    /// desintegra; si no, impacta, empieza el debuff y se despliega el marco inferior dentado con el color del
    /// proyectil. Todo se dispara por eventos del DebuffSystem; el Update solo mueve lo que está en vuelo o en efecto
    /// y no genera garbage. Los desplazamientos usan el tiempo de canción (se congelan en pausa); los fundidos
    /// son puramente cosméticos.
    /// </summary>
    public sealed class NpcSabotageController : MonoBehaviour
    {
        private enum Phase { Idle, Flying, Resolving }

        [SerializeField] private RhythmSession _session;

        [Header("Personajes (hijos de la pista)")]
        [SerializeField] private Transform _leftNpc;
        [SerializeField] private Transform _rightNpc;

        [Header("Proyectil y escudo (hijos de la pista)")]
        [SerializeField] private SpriteRenderer _projectile;
        [SerializeField] private SpriteRenderer _shield;

        [Header("Marco inferior dentado (UI)")]
        [SerializeField] private Image _frame;

        [Header("Parámetros")]
        [Tooltip("Punto de impacto en espacio local de la pista.")]
        [SerializeField] private Vector2 _impactPoint = new Vector2(0f, -4.1f);
        [Tooltip("Desde dónde sale el proyectil, relativo al NPC.")]
        [SerializeField] private Vector2 _handOffset = new Vector2(0f, 0.8f);
        [SerializeField] private float _arcHeight = 1.6f;
        [SerializeField] private float _throwDistance = 0.8f;
        [SerializeField, Min(0.1f)] private float _effectSeconds = 0.5f;
        [SerializeField] private Color _shieldColor = new Color(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private Color[] _palette =
        {
            new Color(1f, 0.1f, 0.65f, 1f),
            new Color(0.25f, 1f, 0.2f, 1f),
            new Color(1f, 0.5f, 0.05f, 1f),
            new Color(0.15f, 0.85f, 1f, 1f)
        };

        private Phase _phase;
        private double _launchTime;
        private float _lead = 1f;
        private Vector3 _start;
        private Vector3 _end;
        private bool _fromLeft = true;
        private int _paletteIndex = -1;
        private Color _current;
        private Transform _thrower;
        private Vector3 _leftBase;
        private Vector3 _rightBase;
        private float _effectRemaining;
        private bool _absorbed;
        private bool _frameActive;
        private Vector3 _projectileBaseScale = Vector3.one;

        private void Awake()
        {
            _leftBase = _leftNpc.localPosition;
            _rightBase = _rightNpc.localPosition;
            _projectileBaseScale = _projectile.transform.localScale;
        }

        private void OnEnable()
        {
            _session.Debuffs.Incoming += OnIncoming;
            _session.Debuffs.Started += OnStarted;
            _session.Debuffs.Ended += OnEnded;
            _session.Debuffs.Absorbed += OnAbsorbed;
            _session.Track.SongStarted += Clear;
            _session.Track.SongStopped += Clear;
            Clear();
        }

        private void OnDisable()
        {
            _session.Debuffs.Incoming -= OnIncoming;
            _session.Debuffs.Started -= OnStarted;
            _session.Debuffs.Ended -= OnEnded;
            _session.Debuffs.Absorbed -= OnAbsorbed;
            _session.Track.SongStarted -= Clear;
            _session.Track.SongStopped -= Clear;
            Clear();
        }

        // ---------- Eventos del debuff ----------

        private void OnIncoming(float lead)
        {
            _lead = lead;
            _fromLeft = !_fromLeft;
            _thrower = _fromLeft ? _leftNpc : _rightNpc;
            _paletteIndex = (_paletteIndex + 1) % _palette.Length;
            _current = _palette[_paletteIndex];

            Vector3 npcBase = _fromLeft ? _leftBase : _rightBase;
            _start = npcBase + new Vector3(_fromLeft ? _handOffset.x : -_handOffset.x, _handOffset.y, 0f);
            _end = new Vector3(_impactPoint.x, _impactPoint.y, 0f);
            _launchTime = _session.Track.Clock.SongTime;

            _projectile.color = _current;
            _projectile.transform.localScale = _projectileBaseScale;
            _projectile.transform.localPosition = _start;
            _projectile.enabled = true;
            _phase = Phase.Flying;
        }

        private void OnAbsorbed()
        {
            PlaceAtImpact();
            _absorbed = true;
            _shield.color = _shieldColor;
            _shield.enabled = true;
            _phase = Phase.Resolving;
            _effectRemaining = _effectSeconds;
        }

        private void OnStarted(float duration)
        {
            PlaceAtImpact();
            _absorbed = false;
            _phase = Phase.Resolving;
            _effectRemaining = _effectSeconds;

            Color c = _current;
            c.a = GameSettings.ReduceVisualEffects ? 0.35f : 0.9f;
            _frame.color = c;
            _frame.enabled = true;
            _frameActive = true;
        }

        private void OnEnded()
        {
            _frameActive = false;
            _frame.enabled = false;
        }

        /// <summary>Sin aviso previo (p. ej. debuff forzado con F6) el proyectil aparece ya en el punto de impacto.</summary>
        private void PlaceAtImpact()
        {
            if (_phase == Phase.Idle)
            {
                if (_paletteIndex < 0) _paletteIndex = 0;
                _current = _palette[_paletteIndex];
                _projectile.color = _current;
                _projectile.enabled = true;
                _projectile.transform.localScale = _projectileBaseScale;
            }
            _projectile.transform.localPosition = new Vector3(_impactPoint.x, _impactPoint.y, 0f);
        }

        // ---------- Animación ----------

        private void Update()
        {
            if (_phase == Phase.Idle && !_frameActive) return;

            float dt = Time.unscaledDeltaTime;

            if (_phase == Phase.Flying)
            {
                if (_session.IsPaused) return;

                float k = Mathf.Clamp01((float)((_session.Track.Clock.SongTime - _launchTime) / _lead));

                Vector3 p = Vector3.Lerp(_start, _end, k);
                p.y += Mathf.Sin(k * Mathf.PI) * _arcHeight;
                _projectile.transform.localPosition = p;
                _projectile.transform.Rotate(0f, 0f, 540f * dt);

                // El NPC se inclina hacia la pista y vuelve durante el primer cuarto del vuelo.
                float lean = Mathf.Sin(Mathf.Clamp01(k * 4f) * Mathf.PI) * _throwDistance;
                Vector3 npcBase = _fromLeft ? _leftBase : _rightBase;
                _thrower.localPosition = npcBase + new Vector3(_fromLeft ? lean : -lean, 0f, 0f);
            }
            else if (_phase == Phase.Resolving)
            {
                _effectRemaining -= dt;
                float f = Mathf.Clamp01(_effectRemaining / _effectSeconds);

                // Se desintegra (escudo) o estalla (impacto): se achica y se desvanece.
                _projectile.transform.localScale = _projectileBaseScale * (_absorbed ? f : 1f + (1f - f) * 0.8f);
                Color pc = _current;
                pc.a = f;
                _projectile.color = pc;

                if (_absorbed)
                {
                    Color sc = _shieldColor;
                    sc.a = f;
                    _shield.color = sc;
                }

                if (_effectRemaining <= 0f)
                {
                    _projectile.enabled = false;
                    _shield.enabled = false;
                    ResetThrowerPose();
                    _phase = Phase.Idle;
                }
            }

            if (_frameActive && !GameSettings.ReduceVisualEffects)
            {
                Color c = _current;
                c.a = 0.75f + 0.2f * Mathf.Sin(Time.unscaledTime * 12f);
                _frame.color = c;
            }
        }

        private void ResetThrowerPose()
        {
            _leftNpc.localPosition = _leftBase;
            _rightNpc.localPosition = _rightBase;
        }

        private void Clear()
        {
            _phase = Phase.Idle;
            _frameActive = false;
            if (_projectile != null)
            {
                _projectile.enabled = false;
                _projectile.transform.localScale = _projectileBaseScale;
            }
            if (_shield != null) _shield.enabled = false;
            if (_frame != null) _frame.enabled = false;
            if (_leftNpc != null && _rightNpc != null) ResetThrowerPose();
        }
    }
}
