using System;
using System.IO;
using UnityEngine;

namespace DiosPunk.Flow
{
    [Serializable]
    public sealed class ProgressData
    {
        public int unlockedLevels = 1;
        public int[] bestScores = Array.Empty<int>();
    }

    /// <summary>Progreso local en JSON (Application.persistentDataPath/progress.json). Solo I/O en eventos, nunca por frame.</summary>
    public sealed class ProgressStore
    {
        private const string FileName = "progress.json";

        private readonly ProgressData _data;
        private readonly int _levelCount;

        public int UnlockedLevels => _data.unlockedLevels;

        private ProgressStore(ProgressData data, int levelCount)
        {
            _data = data;
            _levelCount = Math.Max(1, levelCount);

            if (_data.bestScores == null || _data.bestScores.Length != _levelCount)
            {
                int[] resized = new int[_levelCount];
                if (_data.bestScores != null) Array.Copy(_data.bestScores, resized, Math.Min(_data.bestScores.Length, _levelCount));
                _data.bestScores = resized;
            }
            _data.unlockedLevels = Math.Clamp(_data.unlockedLevels, 1, _levelCount);
        }

        private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static ProgressStore Load(int levelCount)
        {
            ProgressData data = null;
            try
            {
                if (File.Exists(FilePath)) data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Flow] No se pudo leer el progreso, se usa uno nuevo: " + e.Message);
            }
            return new ProgressStore(data ?? new ProgressData(), levelCount);
        }

        public int GetBestScore(int levelIndex) => _data.bestScores[levelIndex];

        /// <summary>Desbloquea hasta el nivel indicado (base 1). Devuelve true si cambió.</summary>
        public bool UnlockUpTo(int levelNumber)
        {
            int target = Math.Clamp(levelNumber, 1, _levelCount);
            if (target <= _data.unlockedLevels) return false;
            _data.unlockedLevels = target;
            return true;
        }

        /// <summary>Registra el puntaje; devuelve true si es un nuevo récord del nivel.</summary>
        public bool RegisterScore(int levelIndex, int score)
        {
            if (score <= _data.bestScores[levelIndex]) return false;
            _data.bestScores[levelIndex] = score;
            return true;
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(_data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Flow] No se pudo guardar el progreso: " + e.Message);
            }
        }

        public void Reset()
        {
            _data.unlockedLevels = 1;
            Array.Clear(_data.bestScores, 0, _data.bestScores.Length);
            Save();
        }

        public void UnlockAll()
        {
            _data.unlockedLevels = _levelCount;
            Save();
        }
    }
}
