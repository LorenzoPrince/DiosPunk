using TMPro;
using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// HUD de depuración para calibrar al tacto. Lee las métricas de RhythmTrackController.Stats y
    /// refresca solo ante eventos; SetText con formato numérico no genera garbage.
    /// Si el promedio es +12 ms, poner SongClock.LatencyOffsetMs = +12 (restar la latencia).
    /// </summary>
    public sealed class RhythmDebugHUD : MonoBehaviour
    {
        [SerializeField] private RhythmTrackController _track;
        [SerializeField] private TMP_Text _judgementText;
        [SerializeField] private TMP_Text _statsText;
        [Tooltip("Opcional: media y desvío estándar del delta.")]
        [SerializeField] private TMP_Text _metricsText;

        private void OnEnable()
        {
            _track.NoteJudged += OnNoteJudged;
            _track.SongStarted += OnSongStarted;
            OnSongStarted();
        }

        private void OnDisable()
        {
            _track.NoteJudged -= OnNoteJudged;
            _track.SongStarted -= OnSongStarted;
        }

        private void OnSongStarted()
        {
            _judgementText.text = "-";
            Refresh();
        }

        private void OnNoteJudged(HitType type, int lane, float deltaMs)
        {
            switch (type)
            {
                case HitType.Perfect: _judgementText.SetText("PERFECT  {0:1} ms", deltaMs); break;
                case HitType.Good: _judgementText.SetText("GOOD  {0:1} ms", deltaMs); break;
                case HitType.Miss: _judgementText.SetText("MISS  {0:1} ms", deltaMs); break;
                default: return;
            }
            Refresh();
        }

        private void Refresh()
        {
            RhythmStats s = _track.Stats;
            _statsText.SetText(
                "P {0:0}  G {1:0}  M {2:0}\nCombo {3:0} (max {4:0})",
                s.Perfect, s.Good, s.Miss, s.Combo, s.MaxCombo);

            if (_metricsText != null)
            {
                _metricsText.SetText("Delta avg {0:1} ms  sd {1:1} ms",
                    (float)s.MeanDeltaMs, (float)s.StdDevDeltaMs);
            }
        }
    }
}
