using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Puente entre los eventos de juego y el SoundEffectManager: juicios de nota, debuffs (lanzamiento, impacto,
    /// escudo) y activación del Modo Punk. Así RhythmSession y los sistemas puros no conocen el audio. Todo por
    /// eventos, sin polling ni allocations.
    /// </summary>
    public sealed class GameplaySfxBridge : MonoBehaviour
    {
        [SerializeField] private RhythmSession _session;

        [Tooltip("Segundos de retraso de la arenga de Javier respecto del efecto de activación del Modo Punk.")]
        [SerializeField, Min(0f)] private float _shoutDelaySeconds = 0.15f;

        private void OnEnable()
        {
            _session.Track.NoteJudged += OnNoteJudged;
            _session.Debuffs.Incoming += OnIncoming;
            _session.Debuffs.Started += OnDebuffStarted;
            _session.Debuffs.Absorbed += OnAbsorbed;
            _session.Punk.Activated += OnPunkActivated;
        }

        private void OnDisable()
        {
            _session.Track.NoteJudged -= OnNoteJudged;
            _session.Debuffs.Incoming -= OnIncoming;
            _session.Debuffs.Started -= OnDebuffStarted;
            _session.Debuffs.Absorbed -= OnAbsorbed;
            _session.Punk.Activated -= OnPunkActivated;
        }

        private static void OnNoteJudged(HitType type, int lane, float deltaMs)
        {
            switch (type)
            {
                case HitType.Perfect: SoundEffectManager.Play(SfxId.HitPerfect); break;
                case HitType.Good: SoundEffectManager.Play(SfxId.HitGood); break;
                case HitType.Miss: SoundEffectManager.Play(SfxId.Miss); break;
            }
        }

        private static void OnIncoming(float lead) => SoundEffectManager.Play(SfxId.NpcThrow);

        private static void OnDebuffStarted(float duration) => SoundEffectManager.Play(SfxId.TomatoImpact);

        private static void OnAbsorbed() => SoundEffectManager.Play(SfxId.ShieldBreak);

        private void OnPunkActivated()
        {
            SoundEffectManager.Play(SfxId.PunkActivate);
            SoundEffectManager.Play(SfxId.PunkShout, 1f, _shoutDelaySeconds);
        }
    }
}
