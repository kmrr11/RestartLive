using LifeSim.UI;
using UnityEngine;

namespace LifeSim.Core
{
    /// <summary>
    /// Entry point. Attach to an empty GameObject in Main scene, or auto-created at runtime.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (UnityEngine.Object.FindObjectOfType<GameUI>() != null)
                return;

            var go = new GameObject("LifeSim");
            go.AddComponent<GameUI>();
        }
    }
}
