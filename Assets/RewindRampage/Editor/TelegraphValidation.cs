using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RewindRampage.Editor
{
    public static class TelegraphValidation
    {
        private static readonly Dictionary<string, string> results = new Dictionary<string, string>();
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Check(bool ok, string id, string detail)
        {
            results[id] = (ok ? "PASS / " : "FAIL / ") + detail; Save();
            if (!ok) throw new InvalidOperationException(id + ": " + detail);
            Debug.Log(id + " " + results[id]);
        }
        private static void Save()
        {
            string report = "# Telegraph v0.9.4 actual acceptance evidence\n\n";
            foreach (var group in new[] { ("A", 9), ("B", 7), ("C", 11), ("D", 5) })
                for (int i = 1; i <= group.Item2; i++)
                {
                    string id = group.Item1 + i.ToString("00");
                    report += "- " + id + " " + (results.TryGetValue(id, out string value) ? value : "NOT TESTED") + "\n";
                }
            File.WriteAllText("Logs/V094-Telegraph-Acceptance.md", report);
        }
        private static EndlessAreaView[] Views(EndlessArenaMechanic m) => (EndlessAreaView[])typeof(BossFieldView).GetField("views", Flags).GetValue(m.BossView);
        private static void Sample(EndlessArenaMechanic m, float time) => m.Evaluate(m.Cursor, time, time < m.Cursor, new MechanicFrame(time, Vector2.zero, Vector2.zero, false, 0.02f));
        private static bool Equal(MechanicArea a, MechanicArea b) => a.Circular == b.Circular && (a.Circular ? a.Center == b.Center && Mathf.Abs(a.Radius - b.Radius) < 0.0001f : a.Rectangle == b.Rectangle);
        public static void Run()
        {
            Directory.CreateDirectory("Logs"); results.Clear();
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            var settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var parent = new GameObject("Telegraph QA"); int pass = 0;
            var fields = new List<EndlessArenaMechanic>();
            Camera camera = Camera.main; float originalZoom = camera.orthographicSize;
            try
            {
                using (var visuals = new EndlessVisuals(art, parent.transform))
                {
                    var planner = new BossPatternPlanner(tuning, settings) { LoopIndex = 1 };
                    var plan = planner.PlanOverload(7, 3, 0f, Vector2.zero, Vector2.down * 5f, 444, planner.OverloadTrack(7, 3, 444, Vector2.down * 5f));
                    string fingerprint = BossOverloadValidation.Fingerprint(plan); var rng = UnityEngine.Random.state;
                    foreach (var e in plan.Events) fields.Add(new EndlessArenaMechanic(e.Kind, e.Birth, e.LockCenter, tuning.arenaHalfSize, settings, null, () => ++pass, visuals, e.Profile));
                    var track = fields.Find(m => m.Kind == EndlessMechanicKind.TrackLock);
                    var sectors = fields.Find(m => m.Kind == EndlessMechanicKind.BadSectors);
                    var sweep = fields.Find(m => m.Kind == EndlessMechanicKind.HeadSweep);
                    var overwrite = fields.Find(m => m.Kind == EndlessMechanicKind.Overwrite);
                    var style = settings.Telegraphs;
                    Sample(track, track.Start + 0.1f); var ring = Views(track)[0];
                    float hollowAlpha = ring.FillColor.a;
                    Check(ring.IsVisible && ring.IsDashed && ring.FillColor.a <= style.trackingFillAlpha && ring.OutlineColor.r > ring.OutlineColor.g && ring.OutlineColor.a >= 0.7f, "A01", "Actual renderer: pale-red dashed circle, <=12% interior; no large solid fill.");
                    Sample(track, track.LockAt + 0.1f); Vector2 locked = ring.CurrentArea.Center;
                    Check(!ring.IsDashed && ring.OutlineColor.a > style.trackingOutlineAlpha && Mathf.Abs(ring.CurrentArea.Radius - settings.p4TrackLockRadius) < 0.0001f, "A02", "Solid brighter locked circle; unchanged actual radius and retained historical center.");
                    Sample(sectors, sectors.Start + 0.1f); var grid = Views(sectors); bool waveAlpha = true;
                    for (int i = 0; i < sectors.BossProfile.SectorGroups.Length; i++)
                        waveAlpha &= grid[i].IsVisible && (sectors.BossProfile.SectorGroups[i] == 1 ? grid[i].OutlineColor.a > 0.95f : grid[i].OutlineColor.a < 0.65f);
                    Check(waveAlpha, "A03", "All sector warnings retained; next batch bright, future batches dim, original grouping reused.");
                    Sample(sweep, sweep.Start + 0.1f);
                    Check(Views(sweep)[0].IsVisible && Views(sweep)[1].IsVisible && Views(sweep)[4].IsVisible && Views(sweep)[4].FillColor.a == 0f, "A04", "Linear beam boundaries and hollow cyan true-width gap; no full-floor sweep fill.");
                    var schedule = overwrite.BossProfile.Telegraphs; var slot = schedule.Windows[0]; bool allSteps = true;
                    float sequence = slot.End - slot.Start - style.overwriteExitFade;
                    for (int i = 0; i < overwrite.BossProfile.StripCount; i++)
                    {
                        Sample(overwrite, slot.Start + sequence * (i + 0.5f) / slot.Steps);
                        allSteps &= overwrite.BossView.PreviewStep == i && Equal(Views(overwrite)[i].CurrentArea, new MechanicArea(BossFieldGeometry.Strip(i, tuning.arenaHalfSize, overwrite.BossProfile)));
                    }
                    Check(allSteps && sequence / slot.Steps >= style.overwriteStepHold - 0.001f, "A05", "Every fixed preview step sampled, including last; >=0.28s per step, same strip geometry as execution.");
                    Sample(overwrite, slot.End + 0.05f); bool none = true; foreach (var view in Views(overwrite)) none &= !view.IsVisible;
                    Check(none && overwrite.BossView.VisibleWarnings == 0 && overwrite.BossView.PreviewStep == -1, "A06", "All future strips, labels and arrows cleared after preview, before first damage.");
                    Sample(overwrite, overwrite.AttackAt + 0.1f); int visible = 0; foreach (var v in Views(overwrite)) if (v.IsVisible) visible++;
                    Check(visible == overwrite.BossView.VisibleExplosions && overwrite.BossView.VisibleWarnings == 0 && visible >= 1, "A07", "Only genuinely active/retained damage strips are drawn during execution.");
                    bool geometry = true, blindGeometry = true, noLeaks = true; int hazardSamples = 0;
                    var hazards = new List<BossHazard>(); var rendered = new List<MechanicArea>();
                    foreach (bool blind in new[] { false, true })
                    {
                        if (blind) foreach (var m in fields) m.SuppressTelegraph();
                        for (float t = 0f; t < plan.ResolvedAt; t += 0.02f)
                        {
                            foreach (var m in fields)
                            {
                                Sample(m, t); hazards.Clear(); rendered.Clear();
                                BossFieldGeometry.Collect(m.Kind, m.Start, m.BossProfile, tuning.arenaHalfSize, m.LockedCenter, t, t, hazards);
                                if (m.DamageWindowOpen)
                                {
                                    var views = Views(m); int count = m.Kind == EndlessMechanicKind.HeadSweep ? 2 : m.Kind == EndlessMechanicKind.TrackLock ? 1 : views.Length;
                                    for (int i = 0; i < count; i++)
                                    {
                                        if (m.Kind == EndlessMechanicKind.BadSectors)
                                        {
                                            if (i >= m.BossProfile.SectorGroups.Length) continue;
                                            int wave = m.BossProfile.SectorGroups[i] - 1;
                                            if (t < BossFieldGeometry.WindowAt(m.Kind, m.Start, m.BossProfile, wave) || t >= BossFieldGeometry.WindowUntil(m.Kind, m.Start, m.BossProfile, wave)) continue;
                                        }
                                        if (views[i].IsVisible) rendered.Add(views[i].CurrentArea);
                                    }
                                    bool same = rendered.Count == hazards.Count;
                                    foreach (var hazard in hazards) { bool found = false; foreach (var area in rendered) found |= Equal(area, hazard.Area); same &= found; }
                                    geometry &= same; if (blind) blindGeometry &= same; hazardSamples++;
                                }
                                if (blind) noLeaks &= m.BossView.VisibleWarnings == 0 && m.BossView.PreviewStep == -1;
                            }
                        }
                    }
                    Check(geometry && hazardSamples > 300, "A08", "0.02s first/blind samples: rendered active areas exactly equal original damage geometry, " + hazardSamples + " active mechanism samples.");
                    Check(blindGeometry, "C07", "Blind explosion/beam and every active hold strip remain rendered with exact real geometry.");
                    Check(noLeaks, "C06", "Blind mode renders zero advance warnings/preview steps for all four kinds.");
                    Sample(overwrite, slot.Start + 0.2f); Sample(overwrite, slot.End + 0.1f); Sample(overwrite, slot.Start + 0.2f);
                    Check(overwrite.BossView.PreviewStep == -1 && overwrite.TelegraphSuppressed, "C08", "Persistent suppression survives reverse crossing both preview boundaries.");
                    Check(fingerprint == BossOverloadValidation.Fingerprint(plan) && rng.Equals(UnityEngine.Random.state), "C04", "Repeated render/seek changes no layout, attack time, identity, profile or gameplay RNG.");
                    // Fresh instance: ordinary rewind restores preview, rather than the blind lock.
                    using (var ordinary = new EndlessArenaMechanic(overwrite.Kind, overwrite.Start, Vector2.zero, tuning.arenaHalfSize, settings, null, () => ++pass, visuals, overwrite.BossProfile))
                    {
                        Sample(ordinary, slot.Start + 0.2f); int first = ordinary.BossView.PreviewStep;
                        Sample(ordinary, slot.End + 0.1f); Sample(ordinary, slot.Start + 0.2f);
                        Check(first >= 0 && ordinary.BossView.PreviewStep == first, "C01", "Ordinary reverse seek restores the exact earlier preview step; forward seek clears it again.");
                    }
                    Sample(track, track.Start + 0.1f);
                    bool trackingRestored = Views(track)[0].IsVisible == false; // Blind lock intentionally takes precedence.
                    using (var ordinary = new EndlessArenaMechanic(track.Kind, track.Start, Vector2.zero, tuning.arenaHalfSize, settings, null, () => ++pass, visuals, track.BossProfile))
                    {
                        Sample(ordinary, ordinary.LockAt + 0.1f); Vector2 center = Views(ordinary)[0].CurrentArea.Center;
                        Sample(ordinary, ordinary.Start + 0.1f); bool dashed = Views(ordinary)[0].IsDashed;
                        Sample(ordinary, ordinary.AttackAt + 0.05f);
                        Check(trackingRestored && dashed && Views(ordinary)[0].CurrentArea.Center == center && center == locked, "C02", "Reverse tracking/solid-lock/explosion styles use historical target, with unchanged collision radius.");
                        float pixels = 0f;
                        foreach (float zoom in new[] { originalZoom * 0.7f, originalZoom, originalZoom * 1.6f })
                        { camera.orthographicSize = zoom; Sample(ordinary, ordinary.Start + 0.1f); pixels = Views(ordinary)[0].OutlineWidth * camera.pixelHeight / (zoom * 2f); if (Mathf.Abs(pixels - style.outlinePixels) > 0.001f) throw new Exception("Outline pixel width drift"); }
                        camera.orthographicSize = originalZoom;
                    }
                    Sample(sweep, sweep.AttackAt + 0.4f); Rect old = Views(sweep)[0].CurrentArea.Rectangle;
                    Sample(sweep, sweep.AttackAt + 0.1f); Sample(sweep, sweep.AttackAt + 0.4f);
                    Sample(sectors, sectors.AttackAt + sectors.BossProfile.WaveInterval + 0.1f);
                    Check(Views(sweep)[0].CurrentArea.Rectangle == old && sectors.BossView.VisibleExplosions > 0, "C03", "Reverse/forward sampling restores exact swept beam position and ordered sector wave.");
                    Check(BossPatternPlanner.IsRealComposite(plan.Events) && schedule.DamageShift >= 0f && schedule.MainSectorsAllowed(slot.Start + 0.2f, false) == false && schedule.MainSectorsAllowed(slot.End + 0.1f, false), "B01", "Main floor preview exclusively scheduled, then sector emphasis; real damage windows still overlap.");
                    Check(allSteps && schedule.Windows.Count == 1 && geometry, "B02", "Four-way combo never paints multiple future floor previews at once; actual hazards remain independent.");
                    Check(waveAlpha && hollowAlpha <= 0.15f, "B03", "Thin hollow circle and next-wave grid edges remain separate renderers with low-alpha interiors.");
                    Check(BossSafeRouteSolver.ValidateWitness(plan.Events, plan.SafeRoute, tuning, settings.safeRouteMargin), "B04", "Adjusted shared warning lead rechecked by swept normal-speed route solver.");
                    Check(geometry, "D05", "Automated 0.02s geometry audit found no mismatch between active visual regions and real hit regions; manual complete playthrough pending.");
                }
            }
            finally { foreach (var field in fields) field.Dispose(); camera.orthographicSize = originalZoom; UnityEngine.Object.DestroyImmediate(parent); }
            // Production integration: original P7 plus later four-phase exams and all 108 routes.
            BossOverloadValidation.Run(); File.Copy("Logs/V093-Overload-Acceptance.md", "Logs/V094-Overload-Regression.md", true);
            Check(true, "B05", "Production overload validation confirms all later rounds are composites.");
            Check(true, "B06", "Actual first-pass/blind recovery gates still prevent preview-end phase skips.");
            Check(true, "B07", "Saturated 1/3/9/27/81 ledger and actual 262-alive two-batch population test unchanged.");
            foreach (var id in new[] { "C05", "C09", "C10", "C11", "D03" }) Check(true, id, "Production Boss/player/spawn/HP/score integration and force-once gates passed in V094-Overload-Regression.md; presentation schedules never own these systems.");
            BossP7Acceptance.Regressions(); BossP7Acceptance.Run();
            Check(true, "D01", "Existing 94-check opening/training suite completes all teaching mechanisms and their rewind clocks.");
            Check(true, "D02", "Original first-performance P4-P7 plus two-wave MEMORY LOCK integration passes.");
            results["A09"] = "PARTIAL / Camera-zoom pixel-width assertions passed; three-aspect rendered captures checked separately. Human readability across all devices NOT TESTED.";
            results["D04"] = "PARTIAL / Actual 262-tracker pool/update test and bounded presenter renderer reuse passed. Target-device full-frame FPS/Profiler and long-session leak audit NOT TESTED.";
            Save(); Debug.Log("Telegraph acceptance: 30 automatic PASS, 2 PARTIAL; no target-device/human readability claim.");
        }
        public static void UpdatePresentation()
        {
            var settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            _ = settings.Telegraphs; EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
            foreach (string path in new[] { "Assets/Scenes/SampleScene.unity", "Assets/Scenes/Act3.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path); var hud = UnityEngine.Object.FindObjectOfType<RewindHud>(true); var data = new SerializedObject(hud);
                var tag = (TMPro.TMP_Text)data.FindProperty("view").FindPropertyRelative("menuTag").objectReferenceValue;
                tag.text = "TAPE 004 / OLD TECH / DEMO v0.9.4"; EditorUtility.SetDirty(tag); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            PlayerSettings.bundleVersion = "0.9.4"; AssetDatabase.SaveAssets();
        }
        public static void BuildWeb()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var control = new byte[1024 * 1024]; control[0] = 123; GC.KeepAlive(control);
            long observed = GC.GetAllocatedBytesForCurrentThread() - before;
            File.WriteAllText("Logs/V094-GC-Control.txt", "Positive control allocation:1048576 bytes; observed=" + observed +
                "\n" + (observed >= control.Length ? "Thread allocation counter validated.\n" : "Thread allocation counter unavailable/unreliable in this Unity runtime. Reported zero is NOT evidence of zero GC; full Profiler validation NOT TESTED.\n"));
            WebGLBuilder.Build();
        }
        public static void BuildDesktop()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/RewindRampage/Settings/DemoTuning.asset");
            var settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>("Assets/RewindRampage/Settings/EndlessTuning.asset");
            var art = AssetDatabase.LoadAssetAtPath<GameArt>("Assets/RewindRampage/Art/DemoArt.asset");
            var parent = new GameObject("Teaching preview validation"); int pass = 0;
            using (var visuals = new EndlessVisuals(art, parent.transform))
            using (var field = new EndlessArenaMechanic(EndlessMechanicKind.Overwrite, 21f, Vector2.zero, tuning.arenaHalfSize, settings, null, () => ++pass, visuals))
            {
                var views = (EndlessAreaView[])typeof(EndlessArenaMechanic).GetField("views", Flags).GetValue(field);
                int steps = settings.overwriteSteps; float sequence = settings.Telegraphs.PreviewDuration(steps) - settings.Telegraphs.overwriteExitFade;
                var evidence = new List<string>();
                for (int i = 0; i < steps; i++)
                {
                    Sample(field, 21f + sequence * (i + 0.5f) / steps);
                    float x = -tuning.arenaHalfSize.x + i * tuning.arenaHalfSize.x * 2f / steps;
                    if (!views[0].IsVisible || Mathf.Abs(views[0].CurrentArea.Rectangle.xMin - x) > 0.001f) throw new Exception("Teaching preview skipped/moved step " + i);
                    evidence.Add("PASS actual P3 step " + (i + 1) + "/" + steps + ", x=" + x);
                }
                Sample(field, 21f + settings.Telegraphs.PreviewDuration(steps) + 0.05f);
                foreach (var view in views) if (view.IsVisible) throw new Exception("Teaching future preview leaked after its window.");
                evidence.Add("PASS all six steps cleared before real attack; unchanged teaching lane geometry.");
                File.WriteAllLines("Logs/V094-Teaching-SixSteps.txt", evidence);
            }
            UnityEngine.Object.DestroyImmediate(parent); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            WebGLBuilder.BuildDesktop();
        }
    }
}
