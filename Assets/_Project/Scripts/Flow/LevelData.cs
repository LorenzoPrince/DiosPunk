using System;
using DiosPunk.Rhythm;
using UnityEngine;

namespace DiosPunk.Flow
{
    [Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea(2, 6)] public string text;
    }

    /// <summary>Datos de un nivel: títulos, dificultad, escena narrativa previa y canción.</summary>
    [CreateAssetMenu(menuName = "DiosPunk/Level Data", fileName = "Level")]
    public sealed class LevelData : ScriptableObject
    {
        [SerializeField] private string _title = "Nivel";
        [SerializeField] private string _stage = "Escenario";
        [SerializeField] private string _difficulty = "Facil";
        [SerializeField] private SongData _song;
        [SerializeField] private DialogueLine[] _dialogue = Array.Empty<DialogueLine>();

        public string Title => _title;
        public string Stage => _stage;
        public string Difficulty => _difficulty;
        public SongData Song => _song;
        public DialogueLine[] Dialogue => _dialogue;
    }
}
