using System;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Una nota del chart. Dato puro y serializable; no contiene estado de runtime.
    /// </summary>
    [Serializable]
    public struct NoteData
    {
        /// <summary>
        /// Instante objetivo de impacto en segundos del dominio DSP, medido desde el inicio de la
        /// canción (0 = primer sample del AudioClip). SongClock lo convierte a dspTime absoluto.
        /// </summary>
        public double targetTimeDSP;

        /// <summary>Carril 0..3 (D/Izq, F/Abajo, J/Arriba, K/Der).</summary>
        public int laneIndex;

        /// <summary>Nota "injusta" generada por un debuff (se tiñe distinto en pista).</summary>
        public bool isDebuffNote;
    }
}
