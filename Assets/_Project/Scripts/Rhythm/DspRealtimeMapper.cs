using UnityEngine;

namespace DiosPunk.Rhythm
{
    /// <summary>
    /// Convierte timestamps de input (línea de tiempo de Time.realtimeSinceStartup, p. ej. CallbackContext.time)
    /// a tiempo DSP. dspTime avanza a saltos (por buffer) y el valor observado nunca está adelantado, así que
    /// se estima el offset (dsp - realtime) quedándose con su máximo reciente. Misma técnica que SongClock;
    /// se usa en la calibración, que no tiene un SongClock.
    /// </summary>
    public sealed class DspRealtimeMapper
    {
        private const double DecayPerSecond = 0.0005;
        private const double ResyncThreshold = 0.1;

        private double _offset;
        private double _lastRealtime;
        private bool _initialized;

        /// <summary>Llamar una vez por frame (o antes de convertir).</summary>
        public void Sample()
        {
            double realtime = Time.realtimeSinceStartupAsDouble;
            double observed = AudioSettings.dspTime - realtime;

            if (!_initialized)
            {
                _offset = observed;
                _lastRealtime = realtime;
                _initialized = true;
                return;
            }

            _offset -= DecayPerSecond * (realtime - _lastRealtime);
            _lastRealtime = realtime;

            if (observed > _offset || _offset - observed > ResyncThreshold) _offset = observed;
        }

        public double RealtimeToDsp(double realtime) => realtime + _offset;

        public void Reset() => _initialized = false;
    }
}
