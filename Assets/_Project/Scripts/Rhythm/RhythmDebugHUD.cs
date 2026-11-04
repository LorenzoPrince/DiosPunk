using TMPro;
using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// HUD de depuración para calibrar al tacto: último juicio, delta en ms, contadores y delta
    /// promedio (Perfect+Good). Refresca solo ante eventos; SetText con formato no genera garbage.
    /// Si el promedio es +12 ms, poner SongClock.LatencyOffsetMs = +12 (restar la latencia).
    /// </summary>
    public sealed class RhythmDebugHUD : MonoBehaviour
    {
        [SerializeField] private RhythmTrackController _track;
        [SerializeField] private TMP_Text _judgementText;
        [SerializeField] private TMP_Text _statsText;

        private int _perfect, _good, _miss, _combo, _maxCombo;
        private double _deltaSum;
        private int _deltaCount;

        private void OnEnable()
        {
            _track.NoteJudged += OnNoteJudged;
            Refresh(0f);
        }

        private void OnDisable()
        {
            _track.NoteJudged -= OnNoteJudged;
        }

        private void OnNoteJudged(HitType type, int lane, float deltaMs)
        {
            switch (type)
            {
                case HitType.Perfect:
                    _perfect++; _combo++; _deltaSum += deltaMs; _deltaCount++;
                    _judgementText.SetText("PERFECT  {0:1} ms", deltaMs);
                    break;
                case HitType.Good:
                    _good++; _combo++; _deltaSum += deltaMs; _deltaCount++;
                    _judgementText.SetText("GOOD  {0:1} ms", deltaMs);
                    break;
                case HitType.Miss:
                    _miss++; _combo = 0;
                    _judgementText.SetText("MISS  {0:1} ms", deltaMs);
                    break;
                default:
                    return;
            }

            if (_combo > _maxCombo) _maxCombo = _combo;
            Refresh(_deltaCount > 0 ? (float)(_deltaSum / _deltaCount) : 0f);
        }

        private void Refresh(float avgDeltaMs)
        {
            _statsText.SetText(
                "P {0:0}  G {1:0}  M {2:0}\nCombo {3:0} (max {4:0})\nAvg delta {5:1} ms",
                _perfect, _good, _miss, _combo, _maxCombo, avgDeltaMs);
        }
    }
}
