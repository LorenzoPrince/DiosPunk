using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>Lista ordenada de los niveles del juego (1 a 5). La referencian ambas escenas.</summary>
    [CreateAssetMenu(menuName = "DiosPunk/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private LevelData[] _levels = System.Array.Empty<LevelData>();

        public LevelData[] Levels => _levels;
        public int Count => _levels.Length;
    }
}
