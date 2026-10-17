using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public static class EchoPathGenerator
    {
        // A fictional history runs FROM the locked target TO the Echo. Rewind reads it backwards.
        public static void Generate(Vector2 echoPosition, Vector2 playerPosition, GameTuning tuning, List<Vector2> history)
        {
            history.Clear();
            Vector2 target = tuning.ClampToArena(playerPosition + Random.insideUnitCircle * 0.75f, 0.9f);
            Vector2 direction = echoPosition - target;
            Vector2 normal = direction.sqrMagnitude > 0.001f ? new Vector2(-direction.y, direction.x).normalized : Vector2.right;
            float bend = Random.Range(0.8f, 3.5f) * (Random.value < 0.5f ? -1f : 1f);
            Vector2 a = tuning.ClampToArena(Vector2.Lerp(target, echoPosition, 0.33f) + normal * bend, 0.6f);
            Vector2 b = tuning.ClampToArena(Vector2.Lerp(target, echoPosition, 0.68f) + normal * bend * Random.Range(-0.8f, 0.8f), 0.6f);
            const int samples = 25;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / (samples - 1), s = 1f - t;
                history.Add(s * s * s * target + 3f * s * s * t * a + 3f * s * t * t * b + t * t * t * echoPosition);
            }
            history[history.Count - 1] = echoPosition;
        }
    }
}
