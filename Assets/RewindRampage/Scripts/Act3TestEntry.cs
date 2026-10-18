using UnityEngine;
using UnityEngine.SceneManagement;

namespace RewindRampage
{
    public sealed class Act3TestEntry : MonoBehaviour
    {
        public static void LoadArchive() { SceneManager.LoadScene("Act3"); }
        public static void LoadMainMenu() { SceneManager.LoadScene("SampleScene"); }

        // Scene-loading helpers remain for retry/menu flows. The player-facing F3 shortcut is retired.
    }
}
