using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

namespace RewindRampage
{
    public sealed class RewindHud : MonoBehaviour
    {
        public const string TitleInstructions = "WASD records 3 seconds. SPACE rewinds to attack.\nREC: survive. PLAYBACK: erase your recorded self.\nTHE ARCHIVE: break the core and survive to 00:00.";
        private static readonly string[] ArchivePhases = { "ACT III / ERASE / AWAKENING", "ACT III / ERASE / INTERFERENCE", "ACT III / ERASE / SYSTEM COLLAPSE" };
        [Serializable]
        public sealed class References
        {
            public TextMeshProUGUI score;
            public TMP_Text timer;
            public TMP_Text timerMilliseconds;
            public ArenaClockHud arenaClock;
            public TapeTransportHud tapeTransport;
            public TextMeshProUGUI recordState;
            public TextMeshProUGUI phase;
            public TextMeshProUGUI enemyCount;
            public TextMeshProUGUI tapeTime;
            public TextMeshProUGUI abilityLabel;
            public TextMeshProUGUI instructions;
            public TextMeshProUGUI notice;
            public TextMeshProUGUI bannerTitle;
            public TextMeshProUGUI bannerDetail;
            public TextMeshProUGUI muteLabel;
            public TextMeshProUGUI nodeStatus;
            public TextMeshProUGUI chainLabel;
            public TextMeshProUGUI rewindCombo;
            public UnityEngine.UI.Image recordDot;
            public UnityEngine.UI.Image flash;
            public UnityEngine.UI.Image[] health;
            public UnityEngine.UI.Image[] tapeSegments;
            public RectTransform[] reels;
            public GameObject menu;
            public TextMeshProUGUI menuTag;
            public TextMeshProUGUI menuTitle;
            public TextMeshProUGUI menuLead;
            public TextMeshProUGUI menuInstructions;
            public TextMeshProUGUI menuStats;
            public TextMeshProUGUI menuBest;
            public UnityEngine.UI.Button primary;
            public TextMeshProUGUI primaryLabel;
            public UnityEngine.UI.Button restart;
            public UnityEngine.UI.Button mute;
            public GameObject duelOverlay;
            public UnityEngine.UI.Image duelBackdrop;
            public UnityEngine.UI.Image trackingBar;
            public UnityEngine.UI.RawImage scanlines;
            public TextMeshProUGUI duelTitle;
            public TextMeshProUGUI duelDetail;
            public TextMeshProUGUI duelTapeTime;
            [FormerlySerializedAs("archiveEntry")] public UnityEngine.UI.Button endlessEntry;
            public TextMeshProUGUI roundTimeLabel;
            public UnityEngine.UI.Button godMode;
            public TextMeshProUGUI godModeLabel;
            public GameObject bootOverlay;
            public UnityEngine.UI.Image bootBackdrop;
            public TextMeshProUGUI bootTerminal;
            public TextMeshProUGUI bootHeadline;
            public CanvasGroup[] bootFades;
            public TextMeshProUGUI storyMessage;
            public TextMeshProUGUI storyCapture;
            public UnityEngine.UI.Image[] storyEdges;
            public UnityEngine.UI.Image storyBackdrop;
            public CanvasGroup rewindOverlay;
            public RectTransform[] rewindBands;
            public RectTransform tapeFlowMarker;
            public UnityEngine.UI.Image tapeBackground;
            public UnityEngine.UI.Image tapeTrim;
        }

        [SerializeField] private References view;
        private GameManager game;
        private float bannerRemaining;
        private float noticeRemaining;
        private float uiTime;
        private float damageFlash;
        private float coreFlash;
        private int previousHealth;
        private int displayedScore = -1;
        private int displayedEnemies = -1;
        private int lastPhase = -1;
        private string previousMode;
        private string previousHint;
        private string previousAbility;
        private float chainRemaining;
        private float comboRemaining;
        private float comboPunch, comboStrength;
        private Vector2 comboOrigin;
        private float storyRemaining, storyDuration;
        private Vector2 modeOrigin;
        private float clockVisibility, clockBlinkTime;
        private float rewindBlend, rewindVisualClock;
        private readonly Queue<string> terminalLines = new Queue<string>(5);
        private readonly StringBuilder terminalBuffer = new StringBuilder(512);
        private float terminalCharacters;
        private string previousTerminal;
        private TextMeshProUGUI endlessLabel;
        private float endlessBorderRemaining;
        private Color endlessBorderColor;
        private UnityEngine.UI.Image rewindWash;
        private TMP_Text rewindCaption;
        public void FlashEndlessBorder(Color color, float duration)
        { endlessBorderColor = color; endlessBorderRemaining = duration; }

        public void LayoutTerminal(Camera camera, Vector2 halfSize, int width, int height)
        {
            Vector3 low = camera.WorldToViewportPoint(new Vector3(-halfSize.x, -halfSize.y, 0f));
            Vector3 high = camera.WorldToViewportPoint(new Vector3(halfSize.x, halfSize.y, 0f));
            Rect viewport = camera.rect;
            low = new Vector3(viewport.x + low.x * viewport.width, viewport.y + low.y * viewport.height, 0f);
            high = new Vector3(viewport.x + high.x * viewport.width, viewport.y + high.y * viewport.height, 0f);
            float canvasHeight = GetComponent<UnityEngine.UI.CanvasScaler>().referenceResolution.y;
            float canvasWidth = canvasHeight * width / Mathf.Max(1f, height);
            RectTransform terminal = view.storyMessage.rectTransform;
            terminal.anchorMin = terminal.anchorMax = terminal.pivot = Vector2.zero;
            terminal.anchoredPosition = new Vector2(low.x * canvasWidth + 20f, low.y * canvasHeight + 20f);
            terminal.sizeDelta = new Vector2(Mathf.Min(660f, (high.x - low.x) * canvasWidth * 0.65f), 106f);
            if (view.rewindCombo != null)
            {
                RectTransform combo = view.rewindCombo.rectTransform;
                combo.anchorMin = combo.anchorMax = Vector2.zero;
                combo.pivot = Vector2.one;
                combo.anchoredPosition = new Vector2(high.x * canvasWidth - 22f, high.y * canvasHeight - 20f);
                comboOrigin = combo.anchoredPosition;
                combo.sizeDelta = new Vector2(Mathf.Min(360f, (high.x - low.x) * canvasWidth * 0.35f), 48f);
            }
        }

        public void Configure(References references) { view = references; }

        public void Initialize(GameManager manager)
        {
            game = manager;
            view.primary.onClick.AddListener(ActivatePrimary);
            view.restart.onClick.AddListener(game.SecondaryAction);
            view.endlessEntry.onClick.AddListener(game.EnterEndlessMode);
            view.mute.onClick.AddListener(game.ToggleMute);
            view.godMode.onClick.AddListener(game.ToggleInvincibility);
            modeOrigin = view.recordState.rectTransform.anchoredPosition;
            previousHealth = game.Player.Health;
        }

        private void ActivatePrimary()
        {
            game.ActivatePrimary();
        }

        public void ShowState()
        {
            bool playing = game.State == SessionState.Playing;
            view.menu.SetActive(!playing);
            view.restart.gameObject.SetActive(game.State == SessionState.Paused || (game.State == SessionState.Results && (game.InEndless || game.InArchive || (game.InDuel && !game.Survived))));
            view.restart.GetComponentInChildren<TextMeshProUGUI>().text = game.InEndless ? game.State == SessionState.Results ? "MAIN MENU" : "RESTART ENDLESS" : game.InArchive ? game.State == SessionState.Results ? "MAIN MENU" : "RESTART ARCHIVE" : game.InDuel ? "NEW RECORDING" : "RESTART TAPE";
            view.endlessEntry.gameObject.SetActive(game.State == SessionState.Title);
            RefreshEndlessEntry();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            bannerRemaining = noticeRemaining = chainRemaining = 0f;
            damageFlash = 0f;
            coreFlash = 0f;
            view.notice.text = view.bannerTitle.text = view.bannerDetail.text = string.Empty;
            view.chainLabel.text = string.Empty;
            previousHealth = game.Player.Health;
            previousHint = previousMode = previousAbility = null;
            displayedScore = displayedEnemies = -1;
            lastPhase = -1;
            if (game.State == SessionState.Title || game.State == SessionState.Results || (playing && game.Elapsed <= 0f)) ShowAttackCombo(0);

            switch (game.State)
            {
                case SessionState.Title:
                    view.menuTag.text = "TAPE 004 / OLD TECH / DEMO v" + Application.version;
                    view.menuTitle.text = "REWIND\n<color=#FFC242>RAMPAGE</color>";
                    view.menuLead.text = "YOUR PAST IS YOUR WEAPON.";
                    view.menuInstructions.text = TitleInstructions;
                    view.menuStats.text = "3 ACTS / 90s REC / 30s PLAYBACK / 90s ARCHIVE";
                    if (game.EndlessUnlocked) view.menuStats.text += "   /   ENDLESS UNLOCKED";
                    view.primaryLabel.text = "PLAY TAPE  >  [ SPACE ]";
                    break;
                case SessionState.Paused:
                    view.menuTag.text = "II PAUSE / TAPE HELD";
                    view.menuTitle.text = "HOLD THE\n<color=#FFC242>SIGNAL</color>";
                    view.menuLead.text = "TAKE A BREATH. THE TAPE CAN WAIT.";
                    view.menuInstructions.text = "TAPE PAUSED\nESC resumes / M toggles sound\nMovement, enemies, rewind and the round timer are paused.";
                    view.menuStats.SetText("SCORE {0:000000}     /     KILLS {1}", game.Score, game.TotalKills);
                    view.primaryLabel.text = "RESUME  >  [ ESC ]";
                    break;
                case SessionState.Results:
                    if (game.InEndless)
                    {
                        view.menuTag.text = "ENDLESS / SIGNAL LOST";
                        view.menuTitle.text = "SIGNAL\n<color=#FF4864>LOST</color>";
                        view.menuLead.text = "THE RECORDING HAS ENDED.";
                        view.menuInstructions.text = "RETRY ENDLESS OR RETURN TO THE MAIN MENU.\nREWIND THE FIELD. KEEP YOUR OWN SIGNAL ALIVE.";
                        if (game.Endless.BossStarted)
                        {
                            float rta = game.Endless.Boss.Rta;
                            view.menuStats.text = "BOSS RTA " + EndlessBossDirector.FormatRta(rta) +
                                " / SCORE " + game.Score.ToString("000000") + " / KILLS " + game.TotalKills;
                        }
                        else view.menuStats.SetText("RUN {0:1}s / SCORE {1:000000} / KILLS {2}", game.Elapsed, game.Score, game.TotalKills);
                        view.primaryLabel.text = "RETRY ENDLESS  >  [ SPACE ]";
                        break;
                    }
                    if (game.InArchive)
                    {
                        view.menuTag.text = game.Survived ? "END OF THE RECORD / ARCHIVE CLOSED" : "THE ARCHIVE / OPERATION TERMINATED";
                        view.menuTitle.text = game.Survived ? "REWIND\n<color=#FFC242>RAMPAGE</color>" : game.Player.Health <= 0 ? "PURGE\n<color=#FF4864>FAILED</color>" : "ERASURE\n<color=#FF4864>COMPLETE</color>";
                        view.menuLead.text = game.Survived ? "BUT THE RECORDING HAS ENDED." : game.Player.Health <= 0 ? "OPERATOR SIGNAL LOST. INSTANCE TERMINATED." : "PURGE COMMAND EXECUTED. OPERATOR INSTANCE ERASED.";
                        view.menuInstructions.text = game.Survived
                            ? "> ORIGINAL SUBJECT: DECEASED.\n> CURRENT INSTANCE: RECONSTRUCTED.\n> ACTIVE RECORDING: NONE.\nENDLESS MODE UNLOCKED."
                            : "One remaining life. Break the core with two rewinds.\nSurvive until 00:00 after destroying it.\nRetry includes the complete temporal collision opening.";
                        view.menuStats.SetText("SURVIVED {0:1}s / ECHOES ERASED {1} / CORE HP {2}/2", game.Elapsed, game.Archive.EchoKills, game.Archive.Core.Health);
                        view.primaryLabel.text = game.Survived ? "MAIN MENU  >  [ SPACE ]" : "RETRY ARCHIVE  >  [ SPACE ]";
                        break;
                    }
                    if (game.InDuel)
                    {
                        view.menuTag.text = game.Survived ? "ACT II COMPLETE / PAST RECORD ERASED" : "ACT II / OPERATION FAILED";
                        view.menuTitle.text = game.Survived ? "PAST RECORD\n<color=#FFC242>ERASED</color>" : "OPERATION\n<color=#FF4864>FAILED</color>";
                        view.menuLead.text = game.Survived ? "THE TAPE IS EMPTY. THE SIGNAL REMAINS." : "OPERATION FAILED.";
                        view.menuInstructions.text = game.Survived
                            ? "Your past self dissolves into magnetic noise.\nA final archive is waiting beyond the tape.\nEnter ACT III to face the remaining echoes."
                            : game.Player.Health <= 0 ? "> OPERATOR SIGNAL LOST.\n> ERASURE INCOMPLETE.\nRetry uses the same final 30-second recording."
                            : "> PLAYBACK COMPLETE.\n> TARGET INSTANCE REMAINS.\n> ERASURE WINDOW EXPIRED.";
                        view.menuStats.SetText("DUEL {0:1}s / PAST SELF HP {1} / ACT I SCORE {2:000000}", game.Duel.DuelTime, game.Duel.Boss.Health, game.Score);
                        view.primaryLabel.text = game.Survived ? "ENTER ACT III  >  [ SPACE ]" : "RETRY DUEL  >  [ SPACE ]";
                        break;
                    }
                    view.menuTag.text = game.Survived ? "END OF TAPE / RECORDING SAVED" : "SIGNAL LOST / RECORDING ENDED";
                    view.menuTitle.text = game.Survived ? "TAPE\n<color=#FFC242>COMPLETE</color>" : "SIGNAL\n<color=#FF4864>LOST</color>";
                    view.menuLead.SetText("FINAL SCORE  {0:000000}", game.Score);
                    view.menuInstructions.text = "KILLS " + game.TotalKills + "    BEST REWIND " + game.BestRewindKills + "    MAX COMBO " + game.BestCombo +
                        "\nSURVIVED " + game.Elapsed.ToString("0.0") + "s    REWINDS " + game.RewindCount + "    LIVES " + game.Player.Health +
                        "\nSPLICE BURSTS " + game.TotalGhostNodeTriggers + "    CHAIN KILLS " + game.TotalChainKills;
                    view.menuStats.text = game.Survived ? "RECORD. LURE. REWIND. REPEAT." : "TRY AGAIN. LURE THE SIGNAL INTO YOUR PAST.";
                    view.primaryLabel.text = "RETRY TAPE  >  [ SPACE ]";
                    break;
            }
            view.menuBest.SetText("PERSONAL BEST  {0:000000}", game.BestScore);
        }

        public void RefreshEndlessEntry()
        {
            if (endlessLabel == null) endlessLabel = view.endlessEntry.GetComponentInChildren<TextMeshProUGUI>(true);
            bool unlocked = game.EndlessUnlocked;
            view.endlessEntry.interactable = unlocked;
            endlessLabel.text = unlocked ? "ENDLESS MODE" : "ENDLESS MODE\nCLEAR STORY TO UNLOCK";
            endlessLabel.fontSize = unlocked ? 18f : 14f;
            endlessLabel.enableWordWrapping = false;
            endlessLabel.color = unlocked ? GameArt.Cyan : GameArt.Muted;
        }

        public void Tick(float deltaTime)
        {
            uiTime += deltaTime;
            TickAttackCombo(deltaTime, game.State == SessionState.Playing);
            if (game.Score != displayedScore)
            {
                displayedScore = game.Score;
                view.score.SetText("SCORE  {0:000000}", game.Score);
            }
            bool reconstructing = game.InDuel && game.Duel.State == GhostDuelState.TransitionIn;
            TickTimer(reconstructing, game.State == SessionState.Playing ? deltaTime : 0f);
            int hostileCount = game.InArchive ? game.Archive.EchoCount : game.InDuel ? game.Duel.Boss.Health : game.EnemyCount;
            if (game.InArchive || hostileCount != displayedEnemies)
            {
                displayedEnemies = hostileCount;
                if (game.InArchive) view.enemyCount.SetText("ECHOES {0}/{1} / REWINDING {2}", displayedEnemies, game.Archive.EchoLimit, game.Archive.AttackingCount);
                else if (game.InDuel) view.enemyCount.SetText("PAST SELF HP  {0} / {1}", displayedEnemies, game.Tuning.ghostBossHealth);
                else view.enemyCount.SetText("HOSTILES  {0:00}", displayedEnemies);
            }

            if (game.Player.Health < previousHealth) damageFlash = 0.55f;
            previousHealth = game.Player.Health;
            int visibleHealth = game.InArchive ? game.Act3Story.VisibleHealth : game.Player.Health;
            for (int i = 0; i < view.health.Length; i++)
                view.health[i].color = i < visibleHealth ? GameArt.Cyan : new Color(0.08f, 0.21f, 0.22f, 0.55f);
            damageFlash = Mathf.Max(0f, damageFlash - deltaTime);
            Color flashColor = GameArt.Red;
            flashColor.a = damageFlash * 0.2f;
            coreFlash = Mathf.Max(0f, coreFlash - deltaTime);
            if (coreFlash > 0f) flashColor = new Color(0.95f, 0.95f, 1f, coreFlash * 0.65f);
            if (game.InArchive && game.Act3Story.ScreenFlash > flashColor.a)
            {
                flashColor = game.Act3Story.FlashColor; flashColor.a = game.Act3Story.ScreenFlash;
            }
            view.flash.color = flashColor;

            bool forced = game.InEndless && game.Endless.ForcedRewinding;
            bool rewinding = game.Rewind.IsRewinding || forced;
            bool ready = game.Rewind.IsReady;
            bool cooling = game.Rewind.CooldownRemaining > 0f;
            string mode = game.State == SessionState.Paused ? "II PAUSE" : rewinding ? game.InDuel ? "<< HOLD" : "<< REW" : game.InArchive ? "ERASE" : game.InDuel ? game.Act2Story.TapeStatus : "REC";
            if (previousMode != mode) { view.recordState.text = mode; previousMode = mode; }
            view.recordState.color = rewinding ? GameArt.Gold : GameArt.Cyan;
            Color dotColor = rewinding ? GameArt.Gold : GameArt.Red;
            dotColor.a = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(uiTime * 3f));
            view.recordDot.color = dotColor;
            bool finalCapture = !game.InArchive && ((!game.InDuel && game.Elapsed >= 60f) || (reconstructing && game.Duel.TransitionElapsed < 6f));
            if (finalCapture)
            {
                view.recordState.text = "REC";
                view.recordState.color = GameArt.Red;
                dotColor = GameArt.Red;
                dotColor.a = reconstructing ? 1f : 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(game.Elapsed * 5f));
                view.recordDot.color = dotColor;
            }
            else view.recordState.text = mode;

            bool reconstructingTape = reconstructing && game.Duel.TransitionElapsed >= 2f && game.Duel.TransitionElapsed < 5f;
            bool reversingTape = rewinding || reconstructingTape;
            float tapeFraction = forced ? game.Endless.Boss.ForcedPlaybackFraction : reconstructingTape ? 1f - game.Duel.TransitionProgress : rewinding ? game.Rewind.PlaybackFraction
                : Mathf.Clamp01(game.Recorder.Duration / game.Tuning.recordDuration);
            for (int i = 0; i < view.tapeSegments.Length; i++)
                view.tapeSegments[i].color = i < Mathf.CeilToInt(tapeFraction * view.tapeSegments.Length)
                    ? reversingTape ? GameArt.Gold : GameArt.Cyan : new Color(0.1f, 0.18f, 0.22f);
            view.tapeTime.SetText("{0:1}s / 3.0s", game.Recorder.Duration);
            string ability = reconstructingTape ? "<<< RECONSTRUCTING" : rewinding ? "<<< REWINDING / 5x" : cooling ? "RECHARGING" : ready ? "SPACE / REWIND" : "MOVE TO RECORD";
            if (game.InEndless && game.Endless.Phase != EndlessPhase.BulletStorm)
                ability = game.Endless.PhasePlayhead < 0f ? "P1 TRACE -" + Mathf.Abs(game.Endless.PhasePlayhead).ToString("0.0") + "s"
                    : rewinding ? "<< REWIND / " + (game.Endless.Phase == EndlessPhase.Detonation ? "P2" : "P3")
                    : game.Endless.PlaybackResumed ? "> PLAY / REPLAY" : ability;
            if (game.InEndless && game.Endless.BossStarted)
                ability = rewinding ? "<< REW / P" + game.Endless.Boss.PhaseId : "P" + game.Endless.Boss.PhaseId + " / " +
                    (game.Endless.Boss.Round == 0 ? "MASTER TAPE" : game.Endless.Boss.Status);
            if (forced) ability = "MEMORY LOCK / << REW";
            if (previousAbility != ability) { view.abilityLabel.text = ability; previousAbility = ability; }
            view.abilityLabel.color = rewinding || ready ? GameArt.Gold : GameArt.Muted;
            if (cooling) view.tapeTime.SetText("COOLDOWN {0:1}s", game.Rewind.CooldownRemaining);
            if (rewinding) view.tapeTime.SetText("<< {0:1}s / 3.0s", game.Rewind.RemainingTapeDuration);
            if (forced) view.tapeTime.SetText("FORCED << {0:1}s", game.Endless.Boss.ForcedRemainingHistorySeconds);
            if (reconstructingTape) view.tapeTime.SetText("<< CAPTURE {0:1}s / 30.0s", 30f * tapeFraction);

            float visualDelta = game.State == SessionState.Playing ? deltaTime : 0f;
            TickRewindPresentation(visualDelta, reversingTape);
            bool transportHeld = game.StoryBooting || (game.InDuel && (game.Act2Story.IsEnding || (game.Duel.IsTransition && !reconstructingTape)))
                || (game.InArchive && game.Act3Story.IsCinematic);
            view.tapeTransport.TickMechanism(visualDelta, tapeFraction, reversingTape,
                game.State == SessionState.Playing && !transportHeld, rewindBlend);

            int phase = game.Elapsed < 30f ? 0 : game.Elapsed < 60f ? 1 : game.Elapsed < 80f ? 2 : 3;
            if (!game.InDuel && !game.InArchive && !game.InEndless && phase != lastPhase)
            {
                lastPhase = phase;
                view.phase.text = new[] { "ACT I / RECORD / OBSERVATION", "ACT I / INCREASING PRESSURE", "ACT I / FINAL RECORDING", "ACT I / BUFFER SATURATION" }[phase];
            }
            if (game.InDuel) view.phase.text = game.Act2Story.IsEnding ? "ACT II / IDENTITY VERIFICATION" : game.Duel.DuelTime < 10f
                ? "ACT II / REWIND / PLAYBACK" : game.Duel.DuelTime < 20f ? "ACT II / SOURCE INTERFERENCE" : "ACT II / ERASURE WINDOW";
            if (game.InArchive) view.phase.text = ArchivePhases[game.Archive.Phase];

            string hint = game.InArchive ? rewinding ? "ECHOES FROZEN / THE ARCHIVE CLOCK IS STILL RUNNING." : game.Archive.Core.IsDestroyed
                ? "CORE ERASED / KEEP DODGING. SURVIVE UNTIL 00:00." : game.Archive.Core.State == TimeCoreState.Dormant
                ? "DODGE LOCKED RED PATHS. THE TIME CORE APPEARS AT 00:30."
                : "TWO DISTINCT REWINDS TO BREAK THE CORE. THEN SURVIVE UNTIL 00:00."
                : game.InDuel ? rewinding ? "PAST SELF FROZEN / ONE PARADOX HIT PER REWIND." :
                "ERASE YOUR PAST / RED SHOTS AIM AT THE REPLAY. DODGE THEIR FIXED PATHS."
                : rewinding ? "REWIND / ENEMIES FROZEN. YOU ARE INVULNERABLE." : game.Elapsed < 5f
                ? "WASD TO RECORD A PATH. LURE THE RED CHASERS BEHIND YOU." : game.Elapsed < 12f && game.RewindCount == 0
                ? "SPACE TO REWIND AND ATTACK. THE BLUE TRAIL CANNOT HURT ENEMIES." : cooling
                ? "KEEP MOVING WHILE THE TAPE RECHARGES. PLAN YOUR NEXT ATTACK." : game.GhostNodeCount > 0
                ? "GOLD NODE ARMED / REWIND THROUGH IT TO RELEASE A SPLICE BURST." : game.VolatileCount > 0
                ? "ORANGE HEXAGONS EXPLODE / LURE CHASERS CLOSE, THEN REWIND." : game.Tuning.enableGhostNodes && game.Elapsed >= game.Tuning.ghostNodeUnlockTime
                ? "CROSS YOUR TRAIL AT A CLEAR ANGLE TO PLANT A GOLD NODE." : ready
                ? "SPACE READY / LURE CHASERS NEAR YOUR PATH FOR A BIGGER COMBO." : "KEEP MOVING / RECORD AT LEAST 1 SECOND OF MOVEMENT.";
            if (previousHint != hint) { view.instructions.text = hint; previousHint = hint; }

            noticeRemaining = Mathf.Max(0f, noticeRemaining - deltaTime);
            view.notice.alpha = Mathf.Clamp01(noticeRemaining * 3f);
            bannerRemaining = Mathf.Max(0f, bannerRemaining - deltaTime);
            float bannerAlpha = Mathf.Clamp01(bannerRemaining * 4f);
            view.bannerTitle.alpha = view.bannerDetail.alpha = bannerAlpha;
            view.muteLabel.text = game.Muted ? "M / SOUND OFF" : "M / SOUND ON";
            if (game.InArchive)
            {
                if (game.Archive.Core.IsDestroyed) view.nodeStatus.SetText("NODES {0} / CORE DESTROYED - SURVIVE", game.GhostNodeCount);
                else if (game.Archive.Core.State == TimeCoreState.Dormant) view.nodeStatus.SetText("NODES {0} / CORE ONLINE AT 00:30", game.GhostNodeCount);
                else view.nodeStatus.SetText("NODES {0} / TIME CORE HP {1}/2", game.GhostNodeCount, game.Archive.Core.Health);
            }
            else if (game.InDuel)
            {
                if (game.Duel.IsTransition) view.nodeStatus.text = "FINAL CAPTURE / TEMPORAL DATA SECURED";
                else view.nodeStatus.SetText("NODES {0} / REPLAY {1:0.0}s / 1x SPEED", game.GhostNodeCount, game.Duel.ReplayTime);
            }
            else if (game.Tuning.enableGhostNodes && game.Elapsed < game.Tuning.ghostNodeUnlockTime)
                view.nodeStatus.SetText("DOUBLE EXPOSURE IN {0}s", Mathf.Ceil(game.Tuning.ghostNodeUnlockTime - game.Elapsed));
            else if (game.Tuning.enableVolatileEnemies && game.Elapsed < game.Tuning.volatileUnlockTime)
                view.nodeStatus.SetText("NODES {0} / VOLATILES IN {1}s", game.GhostNodeCount,
                    Mathf.Ceil(game.Tuning.volatileUnlockTime - game.Elapsed));
            else
                view.nodeStatus.SetText("NODES {0} / VOLATILES {1}/{2}", game.GhostNodeCount, game.VolatileCount,
                    game.Tuning.enableVolatileEnemies ? game.Tuning.maximumVolatileEnemies : 0);
            chainRemaining = Mathf.Max(0f, chainRemaining - deltaTime);
            view.chainLabel.alpha = Mathf.Clamp01(chainRemaining * 4f);
            TickDuelPresentation();
            float storyDelta = game.State == SessionState.Playing ? deltaTime : 0f;
            storyRemaining = Mathf.Max(0f, storyRemaining - storyDelta);
            bool cinematic = game.StoryBooting || (game.InDuel && game.Duel.State != GhostDuelState.Fighting)
                || (game.InArchive && game.Act3Story.IsCinematic);
            terminalCharacters = Mathf.Min(view.storyMessage.text.Length, terminalCharacters + storyDelta * 95f);
            view.storyMessage.alpha = game.State == SessionState.Playing && !cinematic ? 0.8f * Mathf.Clamp01(storyRemaining / 1.2f) : 0f;
            view.storyMessage.maxVisibleCharacters = Mathf.FloorToInt(terminalCharacters);
            view.storyBackdrop.color = Color.clear;
            view.godModeLabel.text = game.Invincible ? "F2 / GOD MODE ON" : "F2 / GOD MODE OFF";
            view.godModeLabel.color = game.Invincible ? GameArt.Gold : GameArt.Muted;
            view.bootOverlay.SetActive(game.StoryBooting && game.State == SessionState.Playing);
            if (game.InEndless)
            {
                endlessBorderRemaining = Mathf.Max(0f, endlessBorderRemaining - storyDelta);
                Color border = endlessBorderColor; border.a = Mathf.Clamp01(endlessBorderRemaining * 4f);
                foreach (UnityEngine.UI.Image image in view.storyEdges) image.color = border;
            }
        }

        private void TickTimer(bool reconstructing, float deltaTime)
        {
            view.arenaClock.SetCameraCompensation(game.ClockCameraOffset);
            view.arenaClock.SetPresentationScale(game.ClockCameraScale);
            bool act1Clock = (!game.InDuel && !game.InArchive) || (reconstructing && game.Duel.TransitionElapsed < 6f);
            float time = game.InArchive ? game.Act3Story.ClockTime : game.InDuel ? game.Duel.DuelTime : game.Elapsed;
            if (game.InEndless) time = Mathf.Abs(game.Endless.PhasePlayhead);
            if (reconstructing) time = game.Duel.TransitionElapsed < 6f ? game.Tuning.roundDuration : 0f;
            bool ending = game.InDuel && game.Act2Story.IsEnding;
            float endingTime = ending ? game.Act2Story.EndingElapsed : 0f;
            if (endingTime >= 9.5f) time = Act3Director.Duration;
            float alarm = act1Clock ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - 60f) / 1.1f)) : 1f;
            float milliseconds = act1Clock ? alarm : 1f;
            if (game.InEndless) { alarm = 0f; milliseconds = 1f; }
            if (game.InArchive && game.Act3Story.IsVictory && game.Act3Story.EndingElapsed >= 7f) alarm = milliseconds = 0f;
            clockBlinkTime += deltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin(clockBlinkTime * Mathf.PI * 3f);
            if (game.InDuel && !ending && game.Duel.DuelTime >= 25f) pulse = 0.5f + 0.5f * Mathf.Sin(clockBlinkTime * Mathf.PI * 6f);
            if (game.InArchive && !game.Act3Story.IsCinematic && game.Archive.Remaining <= 10f) pulse = 0.5f + 0.5f * Mathf.Sin(clockBlinkTime * Mathf.PI * 6f);
            if (game.State == SessionState.Title) clockVisibility = 0f;
            else clockVisibility = Mathf.MoveTowards(clockVisibility, game.StoryBooting ? 0f : 1f, deltaTime * 3f);
            bool reverse = game.Rewind.IsRewinding || (game.InEndless && game.Endless.ForcedRewinding) || (reconstructing && game.Duel.TransitionElapsed >= 2f && game.Duel.TransitionElapsed < 5f);
            bool hold = game.State == SessionState.Paused || game.StoryBooting || ending || (game.InArchive && game.Act3Story.IsCinematic) || (game.InDuel && game.Duel.IsTransition && !reverse);
            bool active = game.State == SessionState.Playing || game.State == SessionState.Paused;
            ClockLamps lamps = active ? reverse ? ClockLamps.Rew : game.Act == GameAct.Rec || (game.InEndless && game.Endless.Phase == EndlessPhase.BulletStorm) ? ClockLamps.Rec : ClockLamps.None : ClockLamps.None;
            if (game.State == SessionState.Playing && !reverse && !hold) lamps |= ClockLamps.Play;
            if (hold) lamps |= ClockLamps.Pause;
            bool locked = game.InArchive && game.Archive.Core.State != TimeCoreState.Dormant;
            float transitionOpacity = endingTime < 9f ? 1f : endingTime < 9.5f ? 1f - (endingTime - 9f) * 2f : (endingTime - 9.5f) * 2f;
            float lampBrightness = Mathf.Lerp(1f, Mathf.Lerp(0.48f, 1f, pulse), alarm);
            view.tapeTransport.DisplayLamps(lamps, locked, lampBrightness, clockVisibility * transitionOpacity);
            if (game.InArchive) transitionOpacity *= game.Act3Story.ClockOpacity;
            if (game.InEndless && game.Endless.BossStarted)
                view.arenaClock.DisplayBossRta(game.Endless.Boss.Rta, clockVisibility, reverse, game.Endless.Boss.PhaseId, game.Endless.Boss.PhaseCaption);
            else view.arenaClock.Display(time, alarm, milliseconds, clockVisibility * transitionOpacity * (game.CoreSequence?.HudOpacity ?? 1f), pulse);
        }

        private void TickRewindPresentation(float deltaTime, bool reversing)
        {
            if (rewindWash == null) rewindWash = view.rewindOverlay.transform.Find("Rewind wash").GetComponent<UnityEngine.UI.Image>();
            if (rewindCaption == null) rewindCaption = view.rewindOverlay.transform.Find("Rewind caption").GetComponent<TMP_Text>();
            bool temporal = game.InEndless && game.Endless.Phase != EndlessPhase.BulletStorm;
            rewindCaption.text = game.InEndless && game.Endless.ForcedRewinding ? "<< FORCED REWIND >>" : "<<< REWIND";
            rewindWash.color = temporal ? new Color(0.72f, 0.52f, 0.96f, 0.025f) : new Color(1f, 0.76f, 0.26f, 0.045f);
            rewindCaption.color = temporal ? EndlessVisuals.RewindColor : GameArt.Gold;
            float target = game.State == SessionState.Playing || game.State == SessionState.Paused ? reversing ? 1f : 0f : 0f;
            if (game.State == SessionState.Title || game.State == SessionState.Results) rewindBlend = 0f;
            else rewindBlend = Mathf.MoveTowards(rewindBlend, target, deltaTime * (reversing ? 18f : 5f));
            if (reversing) rewindVisualClock += deltaTime;
            view.rewindOverlay.gameObject.SetActive(rewindBlend > 0.001f);
            view.rewindOverlay.alpha = rewindBlend;
            for (int i = 0; i < view.rewindBands.Length; i++)
            {
                float y = Mathf.Repeat(i * 0.25f - rewindVisualClock * 1.2f, 1f);
                RectTransform band = view.rewindBands[i];
                band.GetComponent<UnityEngine.UI.Image>().color = temporal ? new Color(0.72f, 0.52f, 0.96f, 0.055f) : new Color(1f, 0.8f, 0.4f, 0.075f);
                band.anchorMin = new Vector2(0f, y); band.anchorMax = new Vector2(1f, y);
                band.offsetMin = new Vector2(0f, -6f); band.offsetMax = new Vector2(0f, 6f);
            }
        }

        private void TickDuelPresentation()
        {
            if (game.CinematicFreeze)
            {
                view.duelOverlay.SetActive(true); view.duelTitle.text = view.duelDetail.text = view.duelTapeTime.text = "";
                view.duelBackdrop.color = new Color(0.008f, 0.008f, 0.02f, game.CoreSequence.Darkness);
                view.trackingBar.gameObject.SetActive(false); view.scanlines.color = Color.clear; return;
            }
            bool archiveVisible = game.InArchive && game.State == SessionState.Playing && game.Act3Story.IsCinematic;
            bool ending = game.InDuel && game.Act2Story.IsEnding;
            bool visible = game.InDuel && game.State == SessionState.Playing && (game.Duel.IsTransition || ending);
            view.duelOverlay.SetActive(visible || archiveVisible);
            view.trackingBar.gameObject.SetActive(true);
            view.scanlines.color = new Color(1f, 1f, 1f, 0.14f);
            if (archiveVisible)
            {
                Act3StoryDirector sequence = game.Act3Story;
                bool freeTime = sequence.IsVictory && sequence.EndingElapsed >= 7f && sequence.EndingElapsed < 10f;
                view.duelTitle.text = sequence.Title; view.duelTitle.color = sequence.IsVictory ? GameArt.White : GameArt.Red;
                view.duelDetail.text = sequence.Detail;
                view.duelTapeTime.text = freeTime || (sequence.IsVictory && sequence.EndingElapsed >= 10f) ? "" : sequence.IsOpening ? "ERASE / 01:30" : "TAPE TRANSPORT STOPPED";
                float darkness = sequence.IsVictory && sequence.EndingElapsed >= 10f ? Mathf.Clamp01((sequence.EndingElapsed - 10f) / 2f) : freeTime ? 0f : 0.12f;
                view.duelBackdrop.color = new Color(0.008f, 0.008f, 0.02f, darkness);
                view.trackingBar.gameObject.SetActive(!freeTime && !(sequence.IsVictory && sequence.EndingElapsed >= 4f));
                view.trackingBar.rectTransform.anchoredPosition = new Vector2(0f, Mathf.PingPong(uiTime * 1900f, 1000f) - 500f);
                return;
            }
            if (!visible)
            {
                return;
            }
            if (ending)
            {
                view.duelTitle.text = game.Act2Story.EndingTitle;
                view.duelTitle.color = game.Act2Story.EndingElapsed >= 3.4f ? GameArt.Red : GameArt.White;
                view.duelDetail.text = game.Act2Story.EndingDetail;
                view.duelTapeTime.text = game.Act2Story.EndingElapsed < 4f ? "PLAYBACK TERMINATED" : "ERASE / ARCHIVE TAPE";
                view.duelBackdrop.color = new Color(0.008f, 0.008f, 0.02f, 0.16f);
                view.trackingBar.rectTransform.anchoredPosition = new Vector2(0f, Mathf.PingPong(uiTime * 1300f, 1000f) - 500f);
                return;
            }
            view.duelTitle.color = GameArt.White;
            GhostDuelState state = game.Duel.State;
            float time = game.Duel.TransitionElapsed;
            view.duelTitle.text = state != GhostDuelState.TransitionIn ? "ELIMINATE THE PREVIOUS INSTANCE." : time < 1f ? "RECORDING COMPLETE."
                : time < 6.8f ? string.Empty : "ELIMINATE THE PREVIOUS INSTANCE.";
            view.duelDetail.text = state != GhostDuelState.TransitionIn ? "> RECORDING LOADED.\n> PLAYBACK READY.\n> DUPLICATE INSTANCE DETECTED." : time < 1f ? string.Empty : time < 2f
                ? "> TEMPORAL DATA SECURED.\n> RECONSTRUCTING SUBJECT..." : time < 5f
                ? "> REWINDING SUBJECT DATA...\n> T-30.00 SEC" : time < 6f
                ? "> OPERATOR INSTANCE CREATED.\n> DUPLICATE IDENTITY DETECTED." : "> RECORDING LOADED.\n> PLAYBACK READY.\n> DUPLICATE INSTANCE DETECTED.";
            float tapeTime = state == GhostDuelState.TransitionIn ? Mathf.Lerp(game.Duel.Recording.Duration,
                game.Duel.ReplayStart, game.Duel.TransitionProgress) : game.Duel.ReplayTime;
            int seconds = Mathf.FloorToInt(tapeTime);
            view.duelTapeTime.text = state != GhostDuelState.TransitionIn || time >= 6f ? "PLAY / 00:00" : (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            float black = state == GhostDuelState.Preparing ? 0.4f : time >= 2f && time < 6f ? 0.03f : 0.2f;
            view.duelBackdrop.color = new Color(0.008f, 0.008f, 0.02f, black);
            view.trackingBar.rectTransform.anchoredPosition = new Vector2(0f, Mathf.PingPong(uiTime * 1300f, 1000f) - 500f);
            if (state == GhostDuelState.TransitionIn)
            {
                SetStoryEffects(time < 6f, time >= 6f && time < 6.3f ? 0.3f : 0f, time >= 6f && time < 6.2f ? 0.2f : 0f);
                view.storyCapture.text = time < 6f ? "REC / RECORDING COMPLETE" : "PLAYBACK / FINAL 30 SECONDS";
                if (time >= 5f && time < 5.2f) view.flash.color = new Color(1f, 1f, 1f, (5.2f - time) * 3.5f);
            }
            else SetStoryEffects(false, 0f, 0f);
        }

        public void SetArchiveHudOpacity(float opacity)
        {
            foreach (CanvasGroup group in view.bootFades) group.alpha = opacity;
        }

        public void SetArchiveIntegrity(int health, float opacity)
        {
            view.storyCapture.SetText("HP: {0}/3 / ERASE", health);
            view.storyCapture.color = health <= 1 ? GameArt.Red : GameArt.White;
            view.storyCapture.alpha = opacity;
        }

        public void SetBootPresentation(float time)
        {
            view.bootOverlay.SetActive(game == null || game.State == SessionState.Playing);
            view.bootBackdrop.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01((time - 0.35f) / 1.6f));
            view.bootTerminal.text = time < 0.8f ? "> TEMPORAL OBSERVATION SYSTEM" : time < 1.6f
                ? "> TEMPORAL OBSERVATION SYSTEM\n> INITIALIZING SUBJECT..." : time < 2.4f
                ? "> TEMPORAL OBSERVATION SYSTEM\n> INITIALIZING SUBJECT...\n> SUBJECT DETECTED."
                : "> SUBJECT DETECTED.\n> BEGINNING RECOVERY TEST.";
            view.bootTerminal.alpha = time < 3.8f ? 1f : Mathf.Clamp01((4.5f - time) / 0.7f);
            view.bootHeadline.text = time >= 3f ? "SURVIVE FOR 90 SECONDS." : string.Empty;
            view.bootHeadline.alpha = Mathf.Clamp01((4.5f - time) / 0.5f);
            foreach (CanvasGroup group in view.bootFades) group.alpha = Mathf.Clamp01((time - 1.7f) / 1.3f);
        }

        public void HideBootPresentation()
        {
            view.bootOverlay.SetActive(false);
            foreach (CanvasGroup group in view.bootFades) group.alpha = 1f;
        }

        public void ShowStoryMessage(string message, float duration)
        {
            if (string.IsNullOrWhiteSpace(message) || message == previousTerminal) return;
            previousTerminal = message;
            int addedCharacters = 0;
            foreach (string raw in message.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (!line.StartsWith(">")) line = "> " + line;
                terminalLines.Enqueue(line); addedCharacters += line.Length + 1;
                while (terminalLines.Count > 4) terminalLines.Dequeue();
            }
            terminalBuffer.Clear();
            foreach (string line in terminalLines) { if (terminalBuffer.Length > 0) terminalBuffer.Append('\n'); terminalBuffer.Append(line); }
            view.storyMessage.text = terminalBuffer.ToString();
            terminalCharacters = Mathf.Max(0, terminalBuffer.Length - addedCharacters);
            view.storyMessage.maxVisibleCharacters = Mathf.FloorToInt(terminalCharacters);
            storyRemaining = storyDuration = Mathf.Max(duration, 5f) + 3f;
        }

        public void SetStoryEffects(bool capturing, float pulse, float glitch)
        {
            view.storyCapture.text = capturing ? "REC / FINAL CAPTURE WINDOW" : string.Empty;
            view.storyCapture.alpha = capturing ? 1f : 0f;
            float alpha = capturing ? 0.4f : 0f;
            Color edge = new Color(1f, 0.15f, 0.3f, Mathf.Max(alpha, Mathf.Clamp01(pulse * 2f)));
            foreach (UnityEngine.UI.Image image in view.storyEdges) image.color = edge;
            float shift = glitch > 0f ? Mathf.Sin(uiTime * 107f) * 4f : 0f;
            if (game != null)
            {
                view.arenaClock.SetGlitch(shift * 0.025f);
                view.recordState.rectTransform.anchoredPosition = modeOrigin + new Vector2(-shift, 0f);
            }
        }

        public void ClearStoryPresentation()
        {
            HideBootPresentation(); storyRemaining = 0f;
            view.storyMessage.text = string.Empty;
            terminalLines.Clear(); terminalCharacters = 0f; previousTerminal = null;
            SetStoryEffects(false, 0f, 0f);
        }

        public void ShowBurst(string title, string detail, Color color, float duration, bool important = false)
        {
            if (!important) return;
            bannerRemaining = duration;
            view.bannerTitle.text = title;
            view.bannerTitle.color = color;
            view.bannerDetail.text = detail;
        }

        public void ShowAttackCombo(int hits)
        {
            comboRemaining = hits > 0 ? 2f : 0f;
            comboPunch = hits > 0 ? 1f : 0f;
            comboStrength = hits > 0 ? Mathf.Clamp01(Mathf.Log(hits + 1f, 2f) / 6f) : 0f;
            if (view.rewindCombo == null) return;
            if (hits > 0) view.rewindCombo.SetText("{0}x COMBO", hits);
            else view.rewindCombo.text = string.Empty;
            TickAttackCombo(0f, true);
        }

        private void TickAttackCombo(float deltaTime, bool playing)
        {
            if (playing)
            {
                comboRemaining = Mathf.Max(0f, comboRemaining - deltaTime);
                comboPunch = Mathf.Max(0f, comboPunch - deltaTime / 0.32f);
            }
            if (view.rewindCombo == null) return;
            RectTransform rect = view.rewindCombo.rectTransform;
            float age = 1f - comboPunch;
            float spring = comboPunch > 0f ? Mathf.Exp(-age * 7f) * Mathf.Cos(age * 16f) : 0f;
            float scale = comboRemaining > 0f ? 1f + comboStrength * 0.12f + Mathf.Lerp(0.10f, 0.28f, comboStrength) * spring : 1f;
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = comboOrigin + new Vector2(Mathf.Sin(age * 60f), Mathf.Sin(age * 12f) * 1.5f) *
                (comboPunch > 0f ? Mathf.Exp(-age * 9f) * comboStrength * 4f : 0f);
            Color color = Color.Lerp(GameArt.Gold, GameArt.White, comboPunch * Mathf.Lerp(0.3f, 0.85f, comboStrength));
            color.a = playing ? Mathf.Clamp01(comboRemaining / 0.25f) : 0f;
            view.rewindCombo.color = color;
            if (comboRemaining <= 0f) view.rewindCombo.text = string.Empty;
        }

        public void ShowNotice(string message)
        {
            ShowStoryMessage(message, 3f);
        }

        public void ShowChain(int kills)
        {
            chainRemaining = 1.8f;
            view.chainLabel.SetText("CHAIN REACTION / CHAIN x{0}", kills);
            view.chainLabel.color = GameArt.Orange;
        }

        public void ShowCoreImpact(bool destroyed)
        {
            coreFlash = destroyed ? 0.65f : 0.4f;
            ShowBurst(destroyed ? "TIME CORE ERASED" : "CORE FRACTURE",
                destroyed ? "THE CLOCK IS STILL RUNNING. SURVIVE UNTIL 00:00." : "ONE HIT PER REWIND / KEEP BREAKING THE ARCHIVE.", GameArt.Gold, 1.7f, true);
        }

        public void SetEnglishPreview()
        {
            view.menuTag.text = "TAPE 004 / OLD TECH / DEMO v0.8.6";
            view.menuTitle.text = "REWIND\n<color=#FFC242>RAMPAGE</color>";
            view.menuLead.text = "YOUR PAST IS YOUR WEAPON.";
            view.menuInstructions.text = TitleInstructions;
            view.menuStats.text = "3 ACTS / 90s REC / 30s PLAYBACK / 90s ARCHIVE";
            view.instructions.text = "WASD TO RECORD A PATH. LURE THE RED CHASERS BEHIND YOU.";
            view.primaryLabel.text = "PLAY TAPE  >  [ SPACE ]";
            view.enemyCount.text = "HOSTILES  00";
            view.timer.text = "00:00";
            view.timerMilliseconds.text = ".000";
            view.timerMilliseconds.alpha = 0f;
            view.roundTimeLabel.text = "ELAPSED TIME";
            view.phase.text = "ACT I / RECORD / OBSERVATION";
            view.godModeLabel.text = "F2 / GOD MODE OFF";
            ClearStoryPresentation();
            view.notice.text = view.bannerTitle.text = view.bannerDetail.text = view.chainLabel.text = string.Empty;
            view.nodeStatus.text = "REC / PLAYBACK / THE ARCHIVE / DEMO v0.8.6";
        }

        public void SetArchivePreview()
        {
            SetEnglishPreview();
            view.menu.SetActive(false);
            view.endlessEntry.gameObject.SetActive(false);
            view.phase.text = ArchivePhases[0];
            view.recordState.text = "ARCHIVE";
            view.timer.text = "01:30";
            view.roundTimeLabel.text = "TIME LEFT";
            view.enemyCount.text = "ECHOES 2/2 / REWINDING 0";
            view.nodeStatus.text = "CORE ONLINE AT 00:30";
            view.instructions.text = "DODGE LOCKED RED PATHS. BREAK THE CORE, THEN SURVIVE TO 00:00.";
        }

        private void OnDestroy()
        {
            if (game == null || view == null) return;
            view.primary.onClick.RemoveListener(ActivatePrimary);
            view.restart.onClick.RemoveListener(game.SecondaryAction);
            view.endlessEntry.onClick.RemoveListener(game.EnterEndlessMode);
            view.mute.onClick.RemoveListener(game.ToggleMute);
            view.godMode.onClick.RemoveListener(game.ToggleInvincibility);
        }
    }
}
