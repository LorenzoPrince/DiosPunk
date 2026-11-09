using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Justificación visual de los debuffs (GDD): un NPC saboteador a un costado de la pista arroja un proyectil
    /// ("tomatazo") hacia la base.
    ///  - Modo Punk INACTIVO: el proyectil impacta, deja una mancha roja irregular en el centro de la pantalla que
    ///    ensucia la pista durante los 5 s del debuff y se desvanece al terminar, y se despliega el marco inferior dentado.
    ///  - Modo Punk ACTIVO: el proyectil choca contra el "Escudo Punk", hay un flash y aparece un cartel estilo fanzine
    ///    "¡DEBUFF ABSORBIDO!" para que se entienda por qué el escudo salvó la partida.
    /// Todo se dispara por eventos del DebuffSystem; el Update solo trabaja mientras hay algo en curso y no genera
    /// garbage. Los desplazamientos y el desvanecimiento de la mancha usan el tiempo de canción (se congelan en pausa);
    /// los pops y fundidos cortos son cosméticos (unscaledDeltaTime).
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

        [Header("Impacto (UI)")]
        [SerializeField] private Image _frame;
        [Tooltip("Mancha roja de tomate/tinta, centrada en la pantalla.")]
        [SerializeField] private Image _splat;
        [Tooltip("Sello/cartel compacto '¡DEBUFF ABSORBIDO!' fuera de los carriles (se activa y desactiva por script).")]
        [SerializeField] private RectTransform _absorbedBanner;
        [Tooltip("CanvasGroup del cartel, para el desvanecimiento de salida.")]
        [SerializeField] private CanvasGroup _bannerGroup;
        [Tooltip("Destello sutil y localizado bajo la HitBar (no ocupa toda la pantalla).")]
        [SerializeField] private Image _shieldFlash;

        [Header("Parámetros")]
        [Tooltip("Punto de impacto en espacio local de la pista, bajo la HitBar para no cubrir las notas.")]
        [SerializeField] private Vector2 _impactPoint = new Vector2(0f, -4.35f);
        [Tooltip("Desde dónde sale el proyectil, relativo al NPC.")]
        [SerializeField] private Vector2 _handOffset = new Vector2(0f, 0.8f);
        [SerializeField] private float _arcHeight = 1.6f;
        [SerializeField] private float _throwDistance = 0.8f;
        [SerializeField, Min(0.1f)] private float _effectSeconds = 0.5f;
        [Tooltip("Duración del anillo de escudo al absorber (más corta que el estallido del impacto).")]
        [SerializeField, Min(0.1f)] private float _absorbEffectSeconds = 0.35f;
        [SerializeField] private Color _shieldColor = new Color(1f, 0.85f, 0.1f, 0.6f);
        [Tooltip("Opacidad máxima del destello de absorción (sutil).")]
        [SerializeField, Range(0.05f, 0.6f)] private float _flashAlpha = 0.3f;
        [SerializeField] private Color _splatColor = new Color(1f, 1f, 1f, 1f);
        [Tooltip("Opacidad de la mancha (calibrada para ensuciar la vista sin tapar del todo la pista).")]
        [SerializeField, Range(0.1f, 1f)] private float _splatAlpha = 0.72f;
        [Tooltip("Opacidad de la mancha con 'Reducir efectos'.")]
        [SerializeField, Range(0.05f, 1f)] private float _splatAlphaReduced = 0.2f;
        [SerializeField, Min(0.1f)] private float _splatFadeSeconds = 1f;
        [Tooltip("Duración total del sello (entrada rápida, salida suave).")]
        [SerializeField, Min(0.3f)] private float _bannerSeconds = 1.2f;
        [SerializeField] private Color[] _palette =
        {
            new Color(0.92f, 0.12f, 0.10f, 1f),
            new Color(1.00f, 0.30f, 0.05f, 1f),
            new Color(0.85f, 0.05f, 0.25f, 1f),
            new Color(1.00f, 0.15f, 0.15f, 1f)
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
        private float _effectDuration = 0.5f;
        private bool _absorbed;
        private bool _frameActive;
        private Vector3 _projectileBaseScale = Vector3.one;

        private bool _splatActive;
        private double _splatEndSongTime;
        private float _splatPop;
        private float _bannerRemaining;
        private float _flashRemaining;

        private const float SplatPopSeconds = 0.18f;
        private const float FlashSeconds = 0.18f;
        private const float BannerPopSeconds = 0.1f;
        private const float BannerSettleSeconds = 0.08f;
        private const float BannerFadeSeconds = 0.35f;

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

        /// <summary>Con Modo Punk activo: el proyectil choca contra el escudo, destello y cartel de "debuff absorbido".</summary>
        private void OnAbsorbed()
        {
            PlaceAtImpact();
            _absorbed = true;
            _shield.color = _shieldColor;
            _shield.enabled = true;
            _phase = Phase.Resolving;
            _effectDuration = _absorbEffectSeconds;
            _effectRemaining = _effectDuration;

            // Destello sutil y localizado bajo la HitBar (no cubre los carriles).
            _flashRemaining = FlashSeconds;
            _shieldFlash.color = new Color(_shieldColor.r, _shieldColor.g, _shieldColor.b, _flashAlpha);
            _shieldFlash.enabled = true;

            // Sello compacto fuera de los carriles.
            _bannerRemaining = _bannerSeconds;
            _absorbedBanner.localScale = new Vector3(0.7f, 0.7f, 1f);
            _bannerGroup.alpha = 1f;
            _absorbedBanner.gameObject.SetActive(true);
        }

        /// <summary>Sin Modo Punk: el "tomatazo" impacta, ensucia la pantalla y empieza el debuff.</summary>
        private void OnStarted(float duration)
        {
            PlaceAtImpact();
            _absorbed = false;
            _phase = Phase.Resolving;
            _effectDuration = _effectSeconds;
            _effectRemaining = _effectDuration;

            _frame.color = WithAlpha(_current, GameSettings.ReduceVisualEffects ? 0.35f : 0.9f);
            _frame.enabled = true;
            _frameActive = true;

            // La mancha dura lo que dura el debuff y se desvanece en el último tramo.
            _splatEndSongTime = _session.Track.Clock.SongTime + duration;
            _splatPop = GameSettings.ReduceVisualEffects ? 0f : SplatPopSeconds;
            _splat.color = WithAlpha(_splatColor, SplatBaseAlpha());
            _splat.rectTransform.localScale = Vector3.one;
            _splat.enabled = true;
            _splatActive = true;
        }

        private void OnEnded()
        {
            _frameActive = false;
            _frame.enabled = false;
            _splatActive = false;
            _splat.enabled = false;
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
            if (_phase == Phase.Idle && !_frameActive && !_splatActive && _bannerRemaining <= 0f && _flashRemaining <= 0f) return;

            float dt = Time.unscaledDeltaTime;

            UpdateProjectile(dt);
            UpdateSplat(dt);
            UpdateAbsorbFeedback(dt);

            if (_frameActive && !GameSettings.ReduceVisualEffects)
            {
                Color c = _current;
                c.a = 0.75f + 0.2f * Mathf.Sin(Time.unscaledTime * 12f);
                _frame.color = c;
            }
        }

        private void UpdateProjectile(float dt)
        {
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
                float f = Mathf.Clamp01(_effectRemaining / _effectDuration);

                // Se desintegra (escudo) o estalla (impacto): se achica o crece y se desvanece.
                _projectile.transform.localScale = _projectileBaseScale * (_absorbed ? f : 1f + (1f - f) * 0.8f);
                _projectile.color = WithAlpha(_current, f);

                if (_absorbed) _shield.color = WithAlpha(_shieldColor, f);

                if (_effectRemaining <= 0f)
                {
                    _projectile.enabled = false;
                    _shield.enabled = false;
                    ResetThrowerPose();
                    _phase = Phase.Idle;
                }
            }
        }

        private void UpdateSplat(float dt)
        {
            if (!_splatActive) return;

            float alpha = SplatBaseAlpha();

            // Desvanecimiento en el último tramo del debuff (con el tiempo de canción: en pausa se congela).
            double remaining = _splatEndSongTime - _session.Track.Clock.SongTime;
            if (remaining < _splatFadeSeconds) alpha *= Mathf.Clamp01((float)(remaining / _splatFadeSeconds));
            _splat.color = WithAlpha(_splatColor, alpha);

            // "Estrellarse": la mancha entra grande y se asienta en una fracción de segundo.
            if (_splatPop > 0f)
            {
                _splatPop -= dt;
                float k = Mathf.Clamp01(_splatPop / SplatPopSeconds);
                float s = 1f + 0.35f * k;
                _splat.rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        private void UpdateAbsorbFeedback(float dt)
        {
            if (_flashRemaining > 0f)
            {
                _flashRemaining -= dt;
                float f = Mathf.Clamp01(_flashRemaining / FlashSeconds);
                _shieldFlash.color = new Color(_shieldColor.r, _shieldColor.g, _shieldColor.b, _flashAlpha * f);
                if (_flashRemaining <= 0f) _shieldFlash.enabled = false;
            }

            if (_bannerRemaining > 0f)
            {
                _bannerRemaining -= dt;
                float elapsed = _bannerSeconds - _bannerRemaining;

                // Entrada rápida (0.7 -> 1.1 -> 1) y salida con desvanecimiento suave.
                float scale = elapsed < BannerPopSeconds ? Mathf.Lerp(0.7f, 1.1f, elapsed / BannerPopSeconds)
                    : elapsed < BannerPopSeconds + BannerSettleSeconds
                        ? Mathf.Lerp(1.1f, 1f, (elapsed - BannerPopSeconds) / BannerSettleSeconds)
                        : 1f;
                _absorbedBanner.localScale = new Vector3(scale, scale, 1f);
                _bannerGroup.alpha = _bannerRemaining < BannerFadeSeconds ? Mathf.Clamp01(_bannerRemaining / BannerFadeSeconds) : 1f;

                if (_bannerRemaining <= 0f) _absorbedBanner.gameObject.SetActive(false);
            }
        }

        private float SplatBaseAlpha() => GameSettings.ReduceVisualEffects ? _splatAlphaReduced : _splatAlpha;

        private static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
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
            _splatActive = false;
            _bannerRemaining = 0f;
            _flashRemaining = 0f;

            if (_projectile != null)
            {
                _projectile.enabled = false;
                _projectile.transform.localScale = _projectileBaseScale;
            }
            if (_shield != null) _shield.enabled = false;
            if (_frame != null) _frame.enabled = false;
            if (_splat != null) _splat.enabled = false;
            if (_shieldFlash != null) _shieldFlash.enabled = false;
            if (_absorbedBanner != null) _absorbedBanner.gameObject.SetActive(false);
            if (_leftNpc != null && _rightNpc != null) ResetThrowerPose();
        }
    }
}
