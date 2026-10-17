using UnityEngine;

namespace RewindRampage
{
    public sealed class EnemyController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer core;
        [SerializeField] private SpriteRenderer halo;
        private float warningRemaining;
        private float warningDuration;
        private float moveSpeed;
        private float visualTime;

        public bool IsAlive { get; private set; }
        public bool IsArmed => warningRemaining <= 0f;
        public int SpawnId { get; private set; }
        public Vector2 Position => transform.position;
        public Vector2 PreviousPosition { get; private set; }

        public void Configure(SpriteRenderer shell, SpriteRenderer center, SpriteRenderer glow)
        {
            body = shell;
            core = center;
            halo = glow;
        }

        public void Spawn(int id, Vector2 position, float speed, float warning)
        {
            SpawnId = id;
            IsAlive = true;
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
            float spawn = warningDuration > 0f ? 1f - warningRemaining / warningDuration : 1f;
            Color color = frozen ? GameArt.Gold : GameArt.Red;
            color.a = IsArmed ? 1f : 0.2f + spawn * 0.6f;
            body.color = color;
            core.color = frozen ? new Color(1f, 0.93f, 0.7f) : new Color(0.3f, 0.045f, 0.09f);
            core.transform.localScale = Vector3.one * (IsArmed ? 0.36f : spawn * 0.36f);
            halo.color = new Color(color.r, color.g, color.b, IsArmed ? 0.22f : 0.08f + 0.15f * Mathf.Sin(spawn * 20f));
            halo.transform.localScale = Vector3.one * (IsArmed ? 1.8f : 2.8f - spawn);
            body.transform.localScale = Vector3.one * (IsArmed ? 0.64f : 0.95f - spawn * 0.31f);
        }

        public void PushAway(Vector2 playerPosition)
        {
            Vector2 direction = Position - playerPosition;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            transform.position += (Vector3)(direction.normalized * 0.6f);
        }

        public void Die()
        {
            IsAlive = false;
            gameObject.SetActive(false);
        }
    }
}
