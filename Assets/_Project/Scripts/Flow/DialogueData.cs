using System;
using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>Variables de partida que una respuesta narrativa puede modificar (ver GameSession).</summary>
    public enum ChoiceVariable
    {
        None = 0,
        /// <summary>-1 más fácil, 0 normal, +1 más difícil: cambia la velocidad de caída de las notas.</summary>
        Difficulty = 1,
        /// <summary>Variante de escenario (0 = base).</summary>
        ScenarioVariant = 2
    }

    [Serializable]
    public struct DialogueLine
    {
        public string speaker;
        [TextArea(2, 6)] public string text;

        [Tooltip("Retrato opcional (estilo fotocopia).")]
        public Sprite portrait;
    }

    [Serializable]
    public struct DialogueChoice
    {
        [Tooltip("Respuesta corta que muestra el botón.")]
        public string label;
        public ChoiceVariable variable;
        public int value;
    }

    /// <summary>
    /// Escena narrativa pre-nivel: líneas de diálogo y, opcionalmente, respuestas cortas al final
    /// (el nodo punteado del Userflow) que modifican variables de GameSession.
    /// </summary>
    [CreateAssetMenu(menuName = "DiosPunk/Dialogue Data", fileName = "Dialogue")]
    public sealed class DialogueData : ScriptableObject
    {
        [SerializeField] private DialogueLine[] _lines = Array.Empty<DialogueLine>();

        [Tooltip("Pregunta que se muestra junto a las respuestas (si las hay).")]
        [SerializeField] private string _choicePrompt = string.Empty;

        [Tooltip("Hasta 3 respuestas cortas. Vacío = la escena termina sin decisión.")]
        [SerializeField] private DialogueChoice[] _choices = Array.Empty<DialogueChoice>();

        public DialogueLine[] Lines => _lines;
        public string ChoicePrompt => _choicePrompt;
        public DialogueChoice[] Choices => _choices;
    }
}
