using System;
using UnityEngine;

namespace RewindRampage
{
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer core;
        [SerializeField] private SpriteRenderer halo;
        [SerializeField] private GameArt art;
        private float warningRemaining;
        private float warningDuration;
        private float moveSpeed;
        private float visualTime;
        private int health;
        private float telegraphGlow = 1f, haloAlpha;
        public void SetTelegraphGlow(float multiplier)
        {
            telegraphGlow = Mathf.Clamp01(multiplier);
            Color color = halo.color; color.a = haloAlpha * telegraphGlow; halo.color = color;
        }

        public bool IsAlive { get; private set; }
        public bool IsArmed => warningRemaining <= 0f;
        public float SpawnProgress => warningDuration > 0f ? 1f - warningRemaining / warningDuration : 1f;
        public bool IsDead => !IsAlive;
        public bool IsVolatile { get; private set; }
        public int SpawnId { get; private set; }
        public Vector2 Position => transform.position;
        public Vector2 PreviousPosition { get; private set; }
        public event Action<EnemyController, DamageContext> OnDeath;

        public void Configure(SpriteRenderer shell, SpriteRenderer center, SpriteRenderer glow, GameArt visuals)
        {
            body = shell;
            core = center;
            halo = glow;
            art = visuals;
        }

        public void Spawn(int id, Vector2 position, float speed, float warning, bool isVolatile = false)
        {
            SpawnId = id;
            IsAlive = true;
            IsVolatile = isVolatile;
            health = 1;
            body.sprite = isVolatile ? art.hexagon : art.disc;
            warningRemaining = warningDuration = warning;
            moveSpeed = speed;
            visualTime = 0f;
            transform.position = position;
            PreviousPosition = position;
            gameObject.SetActive(true);
            UpdateVisual(false);
        }

        public void Tick(float deltaTime, Vector2 target)
        {
            if (!IsAlive) return;
            PreviousPosition = Position;
            visualTime += deltaTime;
            if (warningRemaining > 0f)
            {
                warningRemaining = Mathf.Max(0f, warningRemaining - deltaTime);
                UpdateVisual(false);
                return;
            }
            transform.position = Vector2.MoveTowards(Position, target, moveSpeed * deltaTime);
            UpdateVisual(false);
        }

        public void UpdateVisual(bool frozen)
        {
            float spawn = SpawnProgress;
            Color color = IsVolatile ? GameArt.Orange : frozen ? GameArt.Gold : GameArt.Red;
            color.a = IsArmed ? 1f : 0.2f + spawn * 0.6f;
            body.color = color;
            core.color = IsVolatile ? new Color(1f, 0.81f, 0.42f) : frozen ? new Color(1f, 0.93f, 0.7f) : new Color(0.3f, 0.045f, 0.09f);
            core.transform.localScale = Vector3.one * (IsArmed ? 0.36f : spawn * 0.36f);
            haloAlpha = IsArmed ? 0.22f : 0.08f + 0.15f * Mathf.Sin(spawn * 20f);
            halo.color = new Color(color.r, color.g, color.b, haloAlpha * telegraphGlow);
            halo.transform.localScale = Vector3.one * (IsArmed ? 1.8f : 2.8f - spawn);
            if (IsVolatile) halo.transform.localScale *= 1.2f + Mathf.Sin(visualTime * 6f) * 0.08f;
            body.transform.localScale = Vector3.one * (IsArmed ? 0.64f : 0.95f - spawn * 0.31f);
        }

        public void PushAway(Vector2 playerPosition)
        {
            Vector2 direction = Position - playerPosition;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            transform.position += (Vector3)(direction.normalized * 0.6f);
        }

        public bool TakeDamage(int amount, DamageContext context)
        {
            if (!IsAlive || !IsArmed || amount <= 0) return false;
            health -= amount;
            if (health > 0) return false;
            // Publish exactly once, with the position and enemy type still available to listeners.
            IsAlive = false;
            OnDeath?.Invoke(this, context);
            gameObject.SetActive(false);
            return true;
        }

        // Reset/pool removal is not a combat death and must never produce explosions or score.
        public void Despawn()
        {
            IsAlive = false;
            health = 0;
            gameObject.SetActive(false);
        }

        public void SetPresentationOpacity(float opacity)
        {
            Color color = body.color; color.a = opacity; body.color = color;
            color = core.color; color.a = opacity; core.color = color;
            color = halo.color; color.a = opacity * 0.22f; halo.color = color;
        }
    }
}
