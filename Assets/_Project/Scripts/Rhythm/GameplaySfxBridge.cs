using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Puente entre los eventos de juego y el SoundEffectManager: juicios de nota, debuffs (lanzamiento, impacto,
    /// escudo), activación del Modo Punk y arengas de Javier. Así RhythmSession y los sistemas puros no conocen el
    /// audio. Todo por eventos, sin polling ni allocations.
    ///  - Gritos (#00-05) cada _shoutComboStep aciertos seguidos; Festejos (#06-09) cada _cheerComboStep.
    ///  - Festejo también al absorber un debuff con el escudo.
    ///  - Modo Punk activado: efecto + arenga (#10-16).
    ///  - Lemas (#17-30) al quedarse sin vida (Game Over).
    /// </summary>
    public sealed class GameplaySfxBridge : MonoBehaviour
    {
        [SerializeField] private RhythmSession _session;

        [Tooltip("Segundos de retraso de la arenga de Javier respecto del efecto de activación del Modo Punk.")]
        [SerializeField, Min(0f)] private float _shoutDelaySeconds = 0.15f;

        [Header("Arengas por combo")]
        [Tooltip("Cada cuántos aciertos seguidos grita (#00-05).")]
        [SerializeField, Min(5)] private int _shoutComboStep = 25;
        [Tooltip("Cada cuántos aciertos seguidos festeja (#06-09). Si coincide con un grito, manda el festejo.")]
        [SerializeField, Min(5)] private int _cheerComboStep = 50;

        [Header("Cuenta de inicio ('1, 2, 3, ¡VA!', #33)")]
        [Tooltip("Cuántos beats antes de la primera nota cae el '¡VA!' (2 beats a 101 BPM = 1,19 s). El instante del '¡VA!' " +
                 "dentro del clip es SfxEntry.cueSeconds de LevelStart en la SfxLibrary.")]
        [SerializeField, Min(0f)] private float _countInBeats = 2f;

        private int _lastCombo;

        private void OnEnable()
        {
            _session.Track.NoteJudged += OnNoteJudged;
            _session.Track.SongStarted += OnSongStarted;
            _session.Track.SongPaused += OnSongInterrupted;
            _session.Track.SongStopped += OnSongInterrupted;
            _session.Debuffs.Incoming += OnIncoming;
            _session.Debuffs.Started += OnDebuffStarted;
            _session.Debuffs.Absorbed += OnAbsorbed;
            _session.Punk.Activated += OnPunkActivated;
            _session.Score.Changed += OnScoreChanged;
            _session.HealthDepleted += OnHealthDepleted;
            _lastCombo = 0;
        }

        private void OnDisable()
        {
            _session.Track.NoteJudged -= OnNoteJudged;
            _session.Track.SongStarted -= OnSongStarted;
            _session.Track.SongPaused -= OnSongInterrupted;
            _session.Track.SongStopped -= OnSongInterrupted;
            _session.Debuffs.Incoming -= OnIncoming;
            _session.Debuffs.Started -= OnDebuffStarted;
            _session.Debuffs.Absorbed -= OnAbsorbed;
            _session.Punk.Activated -= OnPunkActivated;
            _session.Score.Changed -= OnScoreChanged;
            _session.HealthDepleted -= OnHealthDepleted;
        }

        private void OnSongStarted()
        {
            _lastCombo = 0;

            // "1, 2, 3, ¡VA!" sincronizado con el pulso de la canción: el ¡VA! cae _countInBeats beats antes de la primera
            // nota (en el grid de beats del chart) y se programa con PlayScheduled sobre el mismo dspTime que el audio.
            RhythmTrackController track = _session.Track;
            if (track.TotalNotes == 0) return;
            double beatSeconds = 60.0 / track.SongBpm;
            double cueSongTime = track.FirstNoteTime - _countInBeats * beatSeconds;
            SoundEffectManager.PlayAtCue(SfxId.LevelStart, track.Clock.SongStartDsp + cueSongTime);
        }

        // Pausa o corte: la cuenta de inicio no debe seguir sonando (ni quedar programada) sobre el menú.
        private static void OnSongInterrupted() => SoundEffectManager.Stop(SfxId.LevelStart);

        private void OnNoteJudged(HitType type, int lane, float deltaMs)
        {
            switch (type)
            {
                // Los aciertos sintéticos (placeholder) solo suenan en niveles con metrónomo: con música real chocan con la banda.
                case HitType.Perfect: if (_session.Track.UseHitPlaceholderSfx) SoundEffectManager.Play(SfxId.HitPerfect); break;
                case HitType.Good: if (_session.Track.UseHitPlaceholderSfx) SoundEffectManager.Play(SfxId.HitGood); break;
                case HitType.Miss: SoundEffectManager.Play(SfxId.Miss); break;
                case HitType.Bad: SoundEffectManager.Play(SfxId.Miss, 0.6f); break;
            }
        }

        // Hitos de combo: solo al SUBIR (un cambio sin avance de combo, p. ej. un fallo, no dispara nada).
        private void OnScoreChanged(int score, int multiplier, int combo)
        {
            if (combo > _lastCombo && combo > 0)
            {
                if (combo % _cheerComboStep == 0) SoundEffectManager.Play(SfxId.Cheer);
                else if (combo % _shoutComboStep == 0) SoundEffectManager.Play(SfxId.CrowdShout);
            }
            _lastCombo = combo;
        }

        private static void OnIncoming(float lead) => SoundEffectManager.Play(SfxId.NpcThrow);

        private static void OnDebuffStarted(float duration) => SoundEffectManager.Play(SfxId.TomatoImpact);

        private static void OnAbsorbed()
        {
            SoundEffectManager.Play(SfxId.ShieldBreak);
            SoundEffectManager.Play(SfxId.Cheer, 1f, 0.3f); // el escudo salvó la partida: festejo
        }

        private void OnPunkActivated()
        {
            SoundEffectManager.Play(SfxId.PunkActivate);
            SoundEffectManager.Play(SfxId.PunkShout, 1f, _shoutDelaySeconds);
        }

        private static void OnHealthDepleted() => SoundEffectManager.Play(SfxId.Motto);
    }
}
