# REWIND RAMPAGE project context

<!-- unity-onboarding:generated:start -->
- Inspected: 2026-10-17. Project root: `LD60-REWIND-RAMPAGE` (renamed at the user's request).
- Confirmed baseline: official empty 2D template, Unity **2022.3.62f3c1**, Built-in rendering (`GraphicsSettings.m_CustomRenderPipeline = 0`), legacy Input Manager (`activeInputHandler = 0`).
- Packages already include uGUI 1.0.0, TextMeshPro 3.0.7 and Unity Test Framework 1.1.33. No networking, custom assemblies, existing gameplay, UXML or prefabs existed at baseline.
- Entry/build scene: `Assets/Scenes/SampleScene.unity`. Runtime source belongs in `Assets/RewindRampage/Scripts`; authoring tools in `Assets/RewindRampage/Editor`.
- No AGENTS.md was found in the workspace or applicable parents. This project has an independent Git repository; the parent workspace and archive are outside it. Local author: YEZISOFTQWQ <154974816+YEZISOFTQWQ@users.noreply.github.com>.
- No Unity MCP/Pipeline provider is configured. Unity CLI is installed locally; an unrestricted process check confirmed no running Editor before authoring.
- Composition: `GameManager` explicitly initializes player, spawner, feedback and HUD. Trail recording, rewind playback and damage rules are plain C# collaborators. Single-player; no persistence beyond a local best-score integer.
- Runtime HUD: uGUI + baked static TMP fonts. v0.2 uses English for all runtime and webpage UI. The README is intentionally unchanged at the user's request.
- v0.2: `TrailIntersectionDetector` observes recorder samples/window events; timestamp-bound ghost nodes unlock at 15s. Rewind snapshots nodes and splits playback at their later intersection timestamps. Only the traversed subsegments and reached nodes deal damage.
- `EnemyController.TakeDamage` marks dead before notifying a single death event. `TrailDamageSystem` owns unified scoring and `ExplosionResolver` drains volatile death explosions iteratively. No delayed visual effect can apply damage. Volatiles unlock at 25s, cap at 3 within the global 35-enemy pool. Inspector switches cover nodes, volatile spawns and secondary chain explosions.
- `GhostNodeController` pools node visuals. `FeedbackController` pools expanding shockwaves, whose visual delays never defer damage or score. Pause/restart/end cancel the appropriate state; results add splice-burst and chain-kill totals.
- Scene/assets are generated with Unity Editor APIs by `RewindRampage.Editor.DemoSceneBuilder.Create`, avoiding handwritten scene YAML. The same menu command supports deliberate regeneration.
- User constraint: **do not run tests**. Compilation, scene generation and WebGL export are allowed; no automated or manual gameplay tests are requested. `docs/` is reserved for the GitHub Pages export; internal documentation is under `Assets/RewindRampage/Documentation`.
- Preserve `../Archive/GhostParry3D_2026-10-17` and its manifest.
- Evidence: ProjectVersion.txt, Packages/manifest.json, GraphicsSettings.asset, ProjectSettings.asset, EditorBuildSettings.asset, original SampleScene.unity and the supplied design document in `Assets/RewindRampage/Documentation/REWIND_RAMPAGE_Design.md`.
<!-- unity-onboarding:generated:end -->
