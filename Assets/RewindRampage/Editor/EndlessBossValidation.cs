using UnityEditor;

namespace RewindRampage.Editor
{
    // Retain the old editor entry point; v1.1 replaces the v1.0-only timings and P5 stop gate.
    // The replacement suite covers every new P4/P5/P6 acceptance ID and the old invariants.
    public static class EndlessBossValidation
    {
        [MenuItem("Rewind Rampage/Validate Boss P4-P6 (legacy entry)")]
        public static void Run() { BossCombinedValidation.Run(); }
    }
}
