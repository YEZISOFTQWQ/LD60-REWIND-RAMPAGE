using UnityEditor;

namespace RewindRampage.Editor
{
    // Existing editor shortcut now captures the current combined specification.
    public static class EndlessBossPreview
    {
        [MenuItem("Rewind Rampage/Capture Boss visuals (legacy entry)")]
        public static void Render() { BossCombinedPreview.Render(); }
    }
}
