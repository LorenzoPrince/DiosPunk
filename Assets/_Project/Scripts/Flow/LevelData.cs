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

    /// <summary>
    /// Datos de un nivel: títulos, dificultad, escena narrativa previa y canción.
    /// La canción NO se referencia directamente: así el menú no carga el audio. Se carga bajo demanda
    /// desde Resources/Songs/ (solo en la escena Gameplay) y se libera al salir.
    /// </summary>
    [CreateAssetMenu(menuName = "DiosPunk/Level Data", fileName = "Level")]
    public sealed class LevelData : ScriptableObject
    {
        public const string SongsResourceFolder = "Songs/";

        [SerializeField] private string _title = "Nivel";
        [SerializeField] private string _stage = "Escenario";
        [SerializeField] private string _difficulty = "Facil";
        [Tooltip("Nombre del SongData dentro de Assets/.../Resources/Songs/.")]
        [SerializeField] private string _songResource;
        [SerializeField] private DialogueLine[] _dialogue = Array.Empty<DialogueLine>();

        public string Title => _title;
        public string Stage => _stage;
        public string Difficulty => _difficulty;
        public string SongResource => _songResource;
        public DialogueLine[] Dialogue => _dialogue;

        public SongData LoadSong()
        {
            return string.IsNullOrEmpty(_songResource)
                ? null
                : Resources.Load<SongData>(SongsResourceFolder + _songResource);
        }
    }
}
