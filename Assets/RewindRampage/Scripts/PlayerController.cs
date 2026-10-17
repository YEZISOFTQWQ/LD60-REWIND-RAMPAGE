using UnityEngine;

namespace RewindRampage
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer glow;
        [SerializeField] private LineRenderer cooldownRing;
        private GameTuning tuning;
        private float invulnerableUntil;
        private Vector2 facing = Vector2.up;

        public int Health { get; private set; }
        public Vector2 Position => transform.position;
        public Vector2 Facing => facing;

        public void Configure(SpriteRenderer bodyRenderer, SpriteRenderer glowRenderer, LineRenderer ring)
        {
            body = bodyRenderer;
            glow = glowRenderer;
            cooldownRing = ring;
        }

        public void Initialize(GameTuning settings) { tuning = settings; }

        public void ResetPlayer(int health = -1)
        {
            Health = health > 0 ? health : tuning.startingHealth;
            invulnerableUntil = 0f;
            facing = Vector2.up;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            body.color = GameArt.Cyan;
            cooldownRing.positionCount = 0;
        }

        public void Move(Vector2 input, float deltaTime)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            if (input.sqrMagnitude > 0.001f) facing = input.normalized;
            SetPosition(tuning.ClampToArena(Position + input * (tuning.moveSpeed * deltaTime), tuning.playerRadius));
        }

        public void SetPosition(Vector2 position) { transform.position = position; }
        public void Protect(float time, float duration) { invulnerableUntil = Mathf.Max(invulnerableUntil, time + duration); }

        public bool TakeDamage(float time)
        {
            if (time < invulnerableUntil || Health <= 0) return false;
            Health--;
            Protect(time, tuning.hurtInvulnerability);
            return true;
        }

        public void UpdateVisual(float time, bool rewinding, float cooldownFraction, bool ready)
        {
            Color color = rewinding ? GameArt.Gold : GameArt.Cyan;
            bool blink = !rewinding && time < invulnerableUntil && Mathf.FloorToInt(time * 22f) % 2 == 0;
            body.color = blink ? Color.white : color;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f);
            glow.color = new Color(color.r, color.g, color.b, rewinding ? 0.65f : 0.24f);
            glow.transform.localScale = Vector3.one * (rewinding ? 2.1f : 1.45f + Mathf.Sin(time * 4f) * 0.08f);

            float progress = rewinding ? 1f : cooldownFraction > 0f ? 1f - cooldownFraction : ready ? 1f : 0f;
            int count = progress <= 0f ? 0 : Mathf.CeilToInt(40f * progress) + 1;
            cooldownRing.positionCount = count;
            cooldownRing.startColor = cooldownRing.endColor = rewinding ? GameArt.Gold : ready ? GameArt.Cyan : GameArt.Muted;
            for (int i = 0; i < count; i++)
            {
                float angle = (90f - 360f * progress * i / Mathf.Max(1, count - 1)) * Mathf.Deg2Rad;
                cooldownRing.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.63f);
            }
        }
    }
}
