using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    // Presentation objects are pooled; logical identity and trigger time live on GhostNode data.
    public sealed class GhostNodeController
    {
        private sealed class NodeVisual
        {
            public Transform Root;
            public SpriteRenderer Ring;
            public SpriteRenderer Core;
        }

        private readonly GameTuning tuning;
        private readonly TrailDamageSystem damage;
        private readonly FeedbackController feedback;
        private readonly Action onTriggered;
        private readonly NodeVisual[] visuals;
        private float visualTime;

        public GhostNodeController(GameTuning tuning, GameArt art, TrailDamageSystem damage,
            FeedbackController feedback, Action onTriggered)
        {
            this.tuning = tuning;
            this.damage = damage;
            this.feedback = feedback;
            this.onTriggered = onTriggered;
            visuals = new NodeVisual[tuning.maximumGhostNodes];
            Transform root = new GameObject("Double Exposure Nodes").transform;
            root.SetParent(feedback.transform, false);
            for (int i = 0; i < visuals.Length; i++)
            {
                Transform item = new GameObject("Ghost Node " + i).transform;
                item.SetParent(root, false);
                SpriteRenderer halo = Sprite(item, "Gold halo", art.glow, 1.25f, 1, new Color(1f, 0.76f, 0.26f, 0.42f));
                halo.sharedMaterial = art.glowMaterial;
                SpriteRenderer ring = Sprite(item, "Exposure ring", art.ring, 0.7f, 4, GameArt.Gold);
                SpriteRenderer core = Sprite(item, "Exposure diamond", art.diamond, 0.31f, 5, GameArt.Gold);
                visuals[i] = new NodeVisual { Root = item, Ring = ring, Core = core };
                item.gameObject.SetActive(false);
            }
        }

        private static SpriteRenderer Sprite(Transform parent, string name, UnityEngine.Sprite sprite, float scale, int order, Color color)
        {
            GameObject item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.color = color;
            return renderer;
        }

        public void Tick(float deltaTime, IReadOnlyList<GhostNode> nodes)
        {
            visualTime += deltaTime;
            int visualIndex = 0;
            for (int i = 0; i < nodes.Count && visualIndex < visuals.Length; i++)
            {
                if (!nodes[i].IsActive || nodes[i].HasTriggered) continue;
                NodeVisual visual = visuals[visualIndex++];
                visual.Root.gameObject.SetActive(true);
                visual.Root.position = nodes[i].Position;
                visual.Ring.transform.localScale = Vector3.one * (0.7f + Mathf.Sin(visualTime * 5f) * 0.07f);
                visual.Core.transform.localRotation = Quaternion.Euler(0f, 0f, visualTime * 32f);
            }
            for (int i = visualIndex; i < visuals.Length; i++) visuals[i].Root.gameObject.SetActive(false);
        }

        public void TriggerNode(GhostNode node)
        {
            if (!node.TryTrigger()) return;
            feedback.ShowExplosion(node.Position, tuning.ghostNodeRadius, DamageSource.GhostNode, 0f);
            damage.ApplyArea(node.Position, tuning.ghostNodeRadius, tuning.ghostNodeDamage,
                new DamageContext(DamageSource.GhostNode, damage.RewindId));
            onTriggered?.Invoke();
        }

        public void ClearAllNodes()
        {
            for (int i = 0; i < visuals.Length; i++) visuals[i].Root.gameObject.SetActive(false);
        }
    }
}
