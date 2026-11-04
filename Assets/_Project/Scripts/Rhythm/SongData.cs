using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Datos inmutables de una canción: audio, BPM fijo y chart (NoteData con tiempos absolutos).
    /// </summary>
    [CreateAssetMenu(menuName = "DiosPunk/Song Data", fileName = "SongData")]
    public sealed class SongData : ScriptableObject
    {
        [SerializeField] private string _title = "Test Song";
        [SerializeField] private AudioClip _audioClip;
        [SerializeField, Min(1f)] private float _bpm = 120f;
        [SerializeField] private NoteData[] _notes = System.Array.Empty<NoteData>();

        [Header("Generador de chart de prueba (menú contextual del asset)")]
        [Tooltip("Segundos desde el inicio del audio hasta el primer beat.")]
        [SerializeField, Min(0f)] private float _testFirstBeatSeconds = 2f;
        [SerializeField, Min(1)] private int _testBeatCount = 64;

        public string Title => _title;
        public AudioClip AudioClip => _audioClip;
        public float Bpm => _bpm;

        /// <summary>Solo lectura por convención: no modificar en runtime.</summary>
        public NoteData[] Notes => _notes;

#if UNITY_EDITOR
        // Patrón de carriles para probar los 4 carriles y notas debuff (cada 4 notas de 16, las últimas).
        private static readonly int[] s_testLanePattern = { 0, 1, 2, 3, 1, 2, 0, 3, 2, 1, 3, 0, 0, 2, 1, 3 };

        [ContextMenu("Generate Test Chart (1 nota por beat)")]
        private void GenerateTestChart()
        {
            UnityEditor.Undo.RecordObject(this, "Generate Test Chart");

            double beat = 60.0 / _bpm;
            var notes = new NoteData[_testBeatCount];
            for (int i = 0; i < notes.Length; i++)
            {
                notes[i] = new NoteData
                {
                    targetTimeDSP = _testFirstBeatSeconds + i * beat,
                    laneIndex = s_testLanePattern[i % s_testLanePattern.Length],
                    isDebuffNote = (i % 16) >= 12
                };
            }

            _notes = notes;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
