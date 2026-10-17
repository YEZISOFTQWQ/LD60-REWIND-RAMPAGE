using TMPro;
using UnityEngine;

namespace RewindRampage
{
    [CreateAssetMenu(menuName = "Rewind Rampage/Game art")]
    public sealed class GameArt : ScriptableObject
    {
        public Sprite disc;
        public Sprite ring;
        public Sprite triangle;
        public Sprite diamond;
        public Sprite glow;
        public Material lineMaterial;
        public Material glowMaterial;
        public TMP_FontAsset font;
        public TMP_FontAsset chineseFont;

        public static readonly Color Cyan = new Color(0.23f, 0.96f, 0.91f);
        public static readonly Color Gold = new Color(1f, 0.76f, 0.26f);
        public static readonly Color Red = new Color(1f, 0.28f, 0.39f);
        public static readonly Color White = new Color(0.88f, 0.94f, 0.94f);
        public static readonly Color Muted = new Color(0.41f, 0.56f, 0.61f);
    }
}
