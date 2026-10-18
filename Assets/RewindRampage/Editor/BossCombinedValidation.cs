using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static partial class BossCombinedValidation
    {
        public static void ValidateAndBuildWeb()
        {
            DemoSceneBuilder.UpgradeToV083();
            Run();
            WebGLBuilder.Build();
        }
        public static void Probe()
        {
            GameTuning tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            EndlessSettings settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            int pass = 0, phase = 4, round = 0;
            using (EndlessBossDirector boss = new EndlessBossDirector(tuning, settings, null, null, () => ++pass))
            {
                float wall = 0f; boss.OnP6Completed += boss.Freeze;
                while (!boss.AtP7Boundary && wall < 180f)
                {
                    wall += 0.02f; boss.Tick(0.02f, 0f, false, new MechanicFrame(wall, Vector2.zero, Vector2.zero, true, 0.02f));
                    if (phase != boss.PhaseId || round != boss.Round)
                    { phase = boss.PhaseId; round = boss.Round; Debug.Log("PROBE P" + phase + " R" + round + " RTA=" + boss.Rta); }
                }
                if (!boss.AtP7Boundary) throw new InvalidOperationException("Boss sequence did not reach P7_PENDING");
                foreach (BossRoundHistory r in boss.Rounds)
                {
                    if (r.Plan == null || !BossSafeRouteSolver.ValidateWitness(r.Plan.Events, r.Plan.SafeRoute, tuning, settings.safeRouteMargin))
                        throw new InvalidOperationException("Missing/invalid safe route in P" + r.Phase + " R" + r.Index);
                    Debug.Log("CERTIFIED P" + r.Phase + " R" + r.Index + " entry=" + r.Entry + " end=" + r.End + " candidate=" + r.Plan.Candidate);
                }
                Debug.Log("PROBE PASSED: 9 rounds / " + boss.MechanicCount + " retained events; max plan " + boss.MaximumPlanningMilliseconds.ToString("0.0") + "ms; RTA=" + boss.Rta);
            }
        }
    }
}
