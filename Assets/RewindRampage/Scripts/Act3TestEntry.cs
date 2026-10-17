using UnityEngine;
using UnityEngine.SceneManagement;

namespace RewindRampage
{
    public sealed class Act3TestEntry : MonoBehaviour
    {
        public static void LoadArchive() { SceneManager.LoadScene("Act3"); }
        public static void LoadMainMenu() { SceneManager.LoadScene("SampleScene"); }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) LoadArchive();
        }
    }
}
