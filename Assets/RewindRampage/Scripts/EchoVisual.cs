using UnityEngine;

namespace RewindRampage
{
    // The same instance artwork is used by combat Echoes and harmless story copies.
    internal sealed class EchoVisual
    {
        private static readonly Color[] Palette =
        {
            new Color(1f, 0.2f, 0.6f, 0.8f), new Color(0.65f, 0.3f, 1f, 0.8f),
            new Color(1f, 0.4f, 0.27f, 0.8f), new Color(0.87f, 0.4f, 1f, 0.8f)
        };
        private readonly Color baseColor;
        public Transform Root { get; }
        public SpriteRenderer Body { get; }
        public SpriteRenderer Ring { get; }
        public SpriteRenderer Halo { get; }

        public EchoVisual(string name, int index, GameArt art, Transform parent)
        {
            baseColor = Palette[index % Palette.Length];
            Root = new GameObject(name).transform;
            Root.SetParent(parent, false);
            Body = ReplayVisuals.Sprite("Echo subject", Root, art.triangle, 0.8f, 9, baseColor);
            Ring = ReplayVisuals.Sprite("Echo outline", Root, art.ring, 1.3f, 7, baseColor);
            Halo = ReplayVisuals.Sprite("Echo glow", Root, art.glow, 1.8f, 6, baseColor);
            Halo.sharedMaterial = art.glowMaterial;
        }

        public void Present(EchoState state, float time, float opacity = 1f, float offset = 0f)
        {
            Color color = state == EchoState.Rewind ? GameArt.Red : baseColor;
            color.a *= opacity; Body.color = color;
            Body.transform.localPosition = new Vector3(offset, 0f, 0f);
            color = baseColor; color.a = 0.24f * opacity; Halo.color = color;
            color = state == EchoState.Warning || state == EchoState.Rewind ? GameArt.Red : baseColor;
            color.a = (state == EchoState.Warning ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(time * 20f)) : 0.65f) * opacity;
            Ring.color = color;
        }

        public void SetVisible(bool visible) { Root.gameObject.SetActive(visible); }
    }
}
