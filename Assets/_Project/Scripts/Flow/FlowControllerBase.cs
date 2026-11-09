using UnityEngine;

namespace DiosPunk.Flow
{
    /// <summary>Base de los orquestadores de cada escena (menú y gameplay): expone la máquina de estados al ScreenManager.</summary>
    public abstract class FlowControllerBase : MonoBehaviour
    {
        public GameStateMachine Machine { get; } = new GameStateMachine();
    }
}
