using System.Collections.Generic;
using UnityEngine;

namespace RewindRampage
{
    public readonly struct TapeBossPose
    {
        public readonly Vector2 Ground;
        public readonly float Height, Tilt, Squash, ReelAngle, ImpactAge;
        public TapeBossPose(Vector2 ground, float height, float tilt, float squash, float reelAngle, float impactAge)
        { Ground = ground; Height = height; Tilt = tilt; Squash = squash; ReelAngle = reelAngle; ImpactAge = impactAge; }
    }

    // A seekable visual trajectory, never Rigidbody gravity or an actor/collision target.
    public sealed class TapeBossMotion
    {
        public readonly struct Jump
        {
            public readonly float Start, Takeoff, Landing, End;
            public readonly Vector2 From, To;
            public Jump(float start, Vector2 from, Vector2 to, EndlessSettings settings)
            {
                Start = start; Takeoff = start + settings.bossJumpChargeSeconds;
                Landing = Takeoff + Mathf.Sqrt(8f * settings.bossJumpHeight / settings.bossGravityStrength);
                End = Landing + settings.bossLandingSettleSeconds; From = from; To = to;
            }
        }
        private readonly EndlessSettings settings;
        private readonly List<Jump> jumps = new List<Jump>();
        public Vector2 InitialAnchor { get; }
        public float EntryLanding => settings.bossEntryFallDuration;
        public float EntryEnd => EntryLanding + settings.bossLandingSettleSeconds;
        public IReadOnlyList<Jump> Jumps => jumps;
        public TapeBossMotion(EndlessSettings settings, Vector2 half)
        { this.settings = settings; InitialAnchor = new Vector2(0f, -half.y + 3.1f); }
        public Jump AddJump(float at, Vector2 target)
        {
            Vector2 from = jumps.Count == 0 ? InitialAnchor : jumps[jumps.Count - 1].To;
            Jump jump = new Jump(at, from, target, settings); jumps.Add(jump); return jump;
        }
        public TapeBossPose Sample(float time)
        {
            Vector2 ground = InitialAnchor; float height = 0f, tilt = 0f, squash = 0f, impact = time - EntryLanding;
            float angle = time * 95f;
            if (time < EntryLanding)
            {
                height = 0.5f * settings.bossGravityStrength * (EntryLanding * EntryLanding - time * time);
                tilt = Mathf.Lerp(-9f, 2f, time / EntryLanding); angle = time * 260f;
            }
            else
            {
                squash = LandingSquash(impact); angle -= Mathf.Clamp(impact, 0f, 0.15f) * 95f;
                foreach (Jump jump in jumps)
                {
                    if (time < jump.Start) break;
                    float charge = Mathf.InverseLerp(jump.Start, jump.Takeoff, time);
                    if (time < jump.Takeoff) { squash = charge * 0.16f; angle += charge * charge * 160f; break; }
                    angle += 160f;
                    if (time < jump.Landing)
                    {
                        float u = Mathf.InverseLerp(jump.Takeoff, jump.Landing, time);
                        ground = Vector2.Lerp(jump.From, jump.To, Mathf.SmoothStep(0f, 1f, u));
                        height = 4f * settings.bossJumpHeight * u * (1f - u);
                        tilt = Mathf.Sign(jump.To.x - jump.From.x) * Mathf.Sin(u * Mathf.PI) * -11f;
                        squash = -0.045f * Mathf.Sin(u * Mathf.PI); angle += u * 240f; impact = -1f; break;
                    }
                    ground = jump.To; angle += 240f; impact = time - jump.Landing;
                    angle -= Mathf.Clamp(impact, 0f, 0.15f) * 95f; squash = LandingSquash(impact);
                }
            }
            return new TapeBossPose(ground, height, tilt, squash, angle, impact);
        }
        private float LandingSquash(float age) => age < 0f || age >= settings.bossLandingSettleSeconds ? 0f :
            Mathf.Sin(age / settings.bossLandingSettleSeconds * Mathf.PI * 2f) * 0.13f * (1f - age / settings.bossLandingSettleSeconds);
    }
}
