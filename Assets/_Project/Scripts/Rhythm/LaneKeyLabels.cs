using TMPro;
using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Rótulos de teclas bajo cada receptáculo de la HitBar. Reflejan el esquema elegido (A S D F / D F J K /
    /// flechas / ambos) y se actualizan solos al cambiarlo en Opciones (evento, sin polling).
    /// </summary>
    public sealed class LaneKeyLabels : MonoBehaviour
    {
        [Tooltip("Un TextMeshPro por carril (índice = lane).")]
        [SerializeField] private TMP_Text[] _labels = new TMP_Text[RhythmInputReader.LaneCount];

        private void OnEnable()
        {
            GameSettings.ControlSchemeChanged += Apply;
            Apply(GameSettings.ControlScheme);
        }

        private void OnDisable()
        {
            GameSettings.ControlSchemeChanged -= Apply;
        }

        private void Apply(ControlScheme scheme)
        {
            for (int i = 0; i < _labels.Length && i < RhythmInputReader.LaneCount; i++)
            {
                if (_labels[i] != null) _labels[i].text = ControlSchemes.GetLaneLabel(scheme, i);
            }
        }
    }
}
