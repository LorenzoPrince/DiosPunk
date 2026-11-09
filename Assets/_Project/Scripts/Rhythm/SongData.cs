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

        [Tooltip("Opcional. Segunda mezcla (guitarra distorsionada / furia) para el Modo Punk. Debe tener la misma duración, " +
                 "frecuencia de muestreo y alineación que el audio base: se reproducen en sincronía exacta (PlayScheduled).")]
        [SerializeField] private AudioClip _punkStemClip;
        [SerializeField, Min(1f)] private float _bpm = 120f;
        [SerializeField] private NoteData[] _notes = System.Array.Empty<NoteData>();

        [Header("Generador de chart de prueba (menú contextual del asset)")]
        [Tooltip("Segundos desde el inicio del audio hasta el primer beat.")]
        [SerializeField, Min(0f)] private float _testFirstBeatSeconds = 2f;
        [SerializeField, Min(1)] private int _testBeatCount = 64;
        [Tooltip("Cada N beats se agrega una nota extra a contratiempo (0 = ninguna, 1 = una por beat). Sube la densidad.")]
        [SerializeField, Min(0)] private int _testOffbeatEvery;
        [Tooltip("Beats iniciales a media densidad (una nota cada dos beats, sin contratiempos): entrada suave al nivel.")]
        [SerializeField, Min(0)] private int _testWarmupBeats;

        public string Title => _title;
        public AudioClip AudioClip => _audioClip;

        /// <summary>Pista secundaria del Modo Punk (puede ser null: la canción solo tiene la mezcla base).</summary>
        public AudioClip PunkStemClip => _punkStemClip;
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
            var notes = new System.Collections.Generic.List<NoteData>(_testBeatCount * 2);
            for (int i = 0; i < _testBeatCount; i++)
            {
                bool warmup = i < _testWarmupBeats;
                if (warmup && (i & 1) == 1) continue; // media densidad al inicio

                int lane = s_testLanePattern[i % s_testLanePattern.Length];
                notes.Add(new NoteData
                {
                    targetTimeDSP = _testFirstBeatSeconds + i * beat,
                    laneIndex = lane,
                    isDebuffNote = (i % 16) >= 12
                });

                // Nota extra a contratiempo en otro carril: más densidad en los niveles avanzados.
                if (!warmup && _testOffbeatEvery > 0 && i % _testOffbeatEvery == 0)
                {
                    notes.Add(new NoteData
                    {
                        targetTimeDSP = _testFirstBeatSeconds + (i + 0.5) * beat,
                        laneIndex = (lane + 2) % 4,
                        isDebuffNote = false
                    });
                }
            }

            _notes = notes.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
