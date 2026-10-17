using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RewindRampage
{
    public sealed class RewindHud : MonoBehaviour
    {
        public const string TitleInstructions = "WASD records 3 seconds. SPACE rewinds to attack.\nREC: survive. PLAYBACK: erase your recorded self.\nTHE ARCHIVE: break the core and survive to 00:00.";
        private static readonly string[] ArchivePhases = { "ACT III / THE ARCHIVE / AWAKENING", "ACT III / THE ARCHIVE / THE HUNT", "ACT III / THE ARCHIVE / COLLAPSE" };
        [Serializable]
        public sealed class References
        {
            public TextMeshProUGUI score;
            public TextMeshProUGUI timer;
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
            public UnityEngine.UI.Button archiveEntry;
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
        private int displayedSeconds = -1;
        private int displayedEnemies = -1;
        private int lastPhase = -1;
        private string previousMode;
        private string previousHint;
        private string previousAbility;
        private float chainRemaining;

        public void Configure(References references) { view = references; }

        public void Initialize(GameManager manager)
        {
            game = manager;
            view.primary.onClick.AddListener(ActivatePrimary);
            view.restart.onClick.AddListener(game.SecondaryAction);
            view.archiveEntry.onClick.AddListener(Act3TestEntry.LoadArchive);
            view.mute.onClick.AddListener(game.ToggleMute);
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
            view.restart.gameObject.SetActive(game.State == SessionState.Paused || (game.State == SessionState.Results && (game.InArchive || (game.InDuel && !game.Survived))));
            view.restart.GetComponentInChildren<TextMeshProUGUI>().text = game.InArchive ? game.State == SessionState.Results ? "MAIN MENU" : "RESTART ARCHIVE" : game.InDuel ? "NEW RECORDING" : "RESTART TAPE";
            view.archiveEntry.gameObject.SetActive(game.State == SessionState.Title);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            bannerRemaining = noticeRemaining = chainRemaining = 0f;
            damageFlash = 0f;
            coreFlash = 0f;
            view.notice.text = view.bannerTitle.text = view.bannerDetail.text = string.Empty;
            view.chainLabel.text = string.Empty;
            previousHealth = game.Player.Health;
            previousHint = previousMode = previousAbility = null;
            displayedScore = displayedSeconds = displayedEnemies = -1;
            lastPhase = -1;

            switch (game.State)
            {
                case SessionState.Title:
                    view.menuTag.text = "TAPE 004 / OLD TECH / DEMO v0.4.1";
                    view.menuTitle.text = "REWIND\n<color=#FFC242>RAMPAGE</color>";
                    view.menuLead.text = "YOUR PAST IS YOUR WEAPON.";
                    view.menuInstructions.text = TitleInstructions;
                    view.menuStats.text = "3 ACTS / 90s REC / 30s PLAYBACK / 90s ARCHIVE";
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
                    if (game.InArchive)
                    {
                        view.menuTag.text = game.Survived ? "ARCHIVE ERASED / FINAL TAPE COMPLETE" : "THE ARCHIVE / SUBJECT REJECTED";
                        view.menuTitle.text = game.Survived ? "TIME\n<color=#FFC242>RELEASED</color>" : "ARCHIVE\n<color=#FF4864>REJECTED</color>";
                        view.menuLead.text = game.Survived ? "NO MORE ECHOES. NO MORE REWINDS." : game.Player.Health <= 0 ? "YOUR SIGNAL WAS ERASED." : "TIME EXPIRED. THE CORE REMAINS.";
                        view.menuInstructions.text = game.Survived
                            ? "Core destroyed. Subject survived until 00:00.\nThe archive collapses into magnetic silence.\nEND OF TAPE."
                            : "Break the core with three separate rewinds.\nDestroying it does not stop the Echoes or the clock.\nRetry restarts THE ARCHIVE at 01:30.";
                        view.menuStats.SetText("SURVIVED {0:1}s / ECHOES ERASED {1} / CORE HP {2}/3", game.Elapsed, game.Archive.EchoKills, game.Archive.Core.Health);
                        view.primaryLabel.text = game.Survived ? "PLAY ARCHIVE AGAIN  >  [ SPACE ]" : "RETRY ARCHIVE  >  [ SPACE ]";
                        break;
                    }
                    if (game.InDuel)
                    {
                        view.menuTag.text = game.Survived ? "ACT II COMPLETE / PAST RECORD ERASED" : "TEMPORAL CONFLICT UNRESOLVED";
                        view.menuTitle.text = game.Survived ? "PAST RECORD\n<color=#FFC242>ERASED</color>" : "TEMPORAL\n<color=#FF4864>CONFLICT</color>";
                        view.menuLead.text = game.Survived ? "THE TAPE IS EMPTY. THE SIGNAL REMAINS." : "CURRENT INSTANCE REJECTED";
                        view.menuInstructions.text = game.Survived
                            ? "Your past self dissolves into magnetic noise.\nA final archive is waiting beyond the tape.\nEnter ACT III to face the remaining echoes."
                            : "Your recorded self survived the paradox.\nRetry Duel uses the same recording.\nNew Recording starts again from ACT I.";
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

        public void Tick(float deltaTime)
        {
            uiTime += deltaTime;
            if (game.Score != displayedScore)
            {
                displayedScore = game.Score;
                view.score.SetText("{0:000000}", game.Score);
            }
            float timeLeft = game.InArchive ? game.Archive.Remaining : game.InDuel ? game.Duel.Remaining : game.Tuning.roundDuration - game.Elapsed;
            if (game.InDuel && game.Duel.State == GhostDuelState.TransitionIn)
                timeLeft = game.Tuning.roundDuration * (1f - game.Duel.TransitionProgress);
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, timeLeft));
            if (seconds != displayedSeconds)
            {
                displayedSeconds = seconds;
                view.timer.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
                view.timer.color = seconds <= 15 ? GameArt.Red : GameArt.White;
            }
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
            for (int i = 0; i < view.health.Length; i++)
                view.health[i].color = i < game.Player.Health ? GameArt.Cyan : new Color(0.14f, 0.2f, 0.24f);
            damageFlash = Mathf.Max(0f, damageFlash - deltaTime);
            Color flashColor = GameArt.Red;
            flashColor.a = damageFlash * 0.2f;
            coreFlash = Mathf.Max(0f, coreFlash - deltaTime);
            if (coreFlash > 0f) flashColor = new Color(0.95f, 0.95f, 1f, coreFlash * 0.65f);
            view.flash.color = flashColor;

            bool rewinding = game.Rewind.IsRewinding;
            bool ready = game.Rewind.IsReady;
            bool cooling = game.Rewind.CooldownRemaining > 0f;
            string mode = game.State == SessionState.Paused ? "II PAUSE" : rewinding ? game.InDuel ? "<< HOLD" : "<< REW" : game.InArchive ? "ARCHIVE" : game.InDuel ? "PLAYBACK" : "REC";
            if (previousMode != mode) { view.recordState.text = mode; previousMode = mode; }
            view.recordState.color = rewinding ? GameArt.Gold : GameArt.Cyan;
            Color dotColor = rewinding ? GameArt.Gold : GameArt.Red;
            dotColor.a = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(uiTime * 3f));
            view.recordDot.color = dotColor;

            float tapeFraction = Mathf.Clamp01(game.Recorder.Duration / game.Tuning.recordDuration);
            for (int i = 0; i < view.tapeSegments.Length; i++)
                view.tapeSegments[i].color = i < Mathf.FloorToInt(tapeFraction * view.tapeSegments.Length)
                    ? rewinding ? GameArt.Gold : GameArt.Cyan : new Color(0.12f, 0.21f, 0.25f);
            view.tapeTime.SetText("{0:1}s / 3.0s", game.Recorder.Duration);
            string ability = rewinding ? "<< REWINDING" : cooling ? "RECHARGING" : ready ? "SPACE / REWIND" : "MOVE TO RECORD";
            if (previousAbility != ability) { view.abilityLabel.text = ability; previousAbility = ability; }
            view.abilityLabel.color = rewinding || ready ? GameArt.Gold : GameArt.Muted;
            if (cooling) view.tapeTime.SetText("COOLDOWN {0:1}s", game.Rewind.CooldownRemaining);

            float reelSpeed = rewinding ? -800f : game.State == SessionState.Playing ? 65f : 12f;
            for (int i = 0; i < view.reels.Length; i++) view.reels[i].Rotate(0f, 0f, reelSpeed * deltaTime);

            int phase = game.Elapsed < 15f ? 0 : game.Elapsed < 25f ? 1 : game.Elapsed < 45f ? 2 : game.Elapsed < 65f ? 3 : 4;
            if (!game.InDuel && !game.InArchive && phase != lastPhase)
            {
                lastPhase = phase;
                view.phase.text = new[] { "ACT I / REC / LEARN THE TAPE", "ACT I / DOUBLE EXPOSURE", "ACT I / VOLATILE SIGNALS", "ACT I / CHAIN REACTION", "ACT I / FINAL TRANSMISSION" }[phase];
                if (game.State == SessionState.Playing && phase == 1 && game.Tuning.enableGhostNodes)
                    ShowNotice("DOUBLE EXPOSURE ONLINE / CROSS YOUR TRAIL TO PLANT A GOLD NODE.");
                if (game.State == SessionState.Playing && phase == 2 && game.Tuning.enableVolatileEnemies)
                    ShowNotice("VOLATILE SIGNALS ONLINE / ORANGE HEXAGONS EXPLODE ON DEATH.");
            }
            if (game.InDuel) view.phase.text = "ACT II / PLAYBACK / FINAL 30 SECONDS";
            if (game.InArchive) view.phase.text = ArchivePhases[game.Archive.Phase];

            string hint = game.InArchive ? rewinding ? "ECHOES FROZEN / THE ARCHIVE CLOCK IS STILL RUNNING." : game.Archive.Core.IsDestroyed
                ? "CORE ERASED / KEEP DODGING. SURVIVE UNTIL 00:00." : game.Archive.Core.State == TimeCoreState.Dormant
                ? "DODGE LOCKED RED PATHS. THE TIME CORE APPEARS AT 00:30."
                : "THREE DISTINCT REWINDS TO BREAK THE CORE. THEN SURVIVE UNTIL 00:00."
                : game.InDuel ? rewinding ? "PAST SELF FROZEN / ONE PARADOX HIT PER REWIND." :
                "ERASE YOUR PAST / RED DASHES WARN OF ATTACKS. THE FAINT PINK LINE SHOWS ITS NEXT SECOND."
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
                else view.nodeStatus.SetText("NODES {0} / TIME CORE HP {1}/3", game.GhostNodeCount, game.Archive.Core.Health);
            }
            else if (game.InDuel)
                view.nodeStatus.SetText("NODES {0} / REPLAY {1:0.0}s / 1x SPEED", game.GhostNodeCount, game.Duel.ReplayTime);
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
        }

        private void TickDuelPresentation()
        {
            bool visible = game.InDuel && game.State == SessionState.Playing && game.Duel.IsTransition;
            view.duelOverlay.SetActive(visible);
            view.scanlines.color = game.InDuel || game.InArchive ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
            if (!visible) return;
            GhostDuelState state = game.Duel.State;
            view.duelTitle.text = state == GhostDuelState.TransitionIn ? "END OF TAPE" : "DUPLICATE SUBJECT DETECTED";
            view.duelDetail.text = state == GhostDuelState.TransitionIn ? "SEEKING THE FINAL 30 SECONDS..." : "ERASE YOUR PAST";
            float tapeTime = state == GhostDuelState.TransitionIn ? Mathf.Lerp(game.Duel.Recording.Duration,
                game.Duel.ReplayStart, game.Duel.TransitionProgress) : game.Duel.ReplayTime;
            int seconds = Mathf.FloorToInt(tapeTime);
            view.duelTapeTime.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            float black = state == GhostDuelState.TransitionIn && game.Duel.TransitionProgress > 0.9f ? 1f : state == GhostDuelState.Preparing ? 0.55f : 0.2f;
            view.duelBackdrop.color = new Color(0.008f, 0.008f, 0.02f, black);
            view.trackingBar.rectTransform.anchoredPosition = new Vector2(0f, Mathf.PingPong(uiTime * 1300f, 1000f) - 500f);
        }

        public void ShowBurst(string title, string detail, Color color, float duration)
        {
            bannerRemaining = duration;
            view.bannerTitle.text = title;
            view.bannerTitle.color = color;
            view.bannerDetail.text = detail;
        }

        public void ShowNotice(string message)
        {
            noticeRemaining = 1.7f;
            view.notice.text = message;
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
                destroyed ? "THE CLOCK IS STILL RUNNING. SURVIVE UNTIL 00:00." : "ONE HIT PER REWIND / KEEP BREAKING THE ARCHIVE.", GameArt.Gold, 1.7f);
        }

        public void SetEnglishPreview()
        {
            view.menuTag.text = "TAPE 004 / OLD TECH / DEMO v0.4.1";
            view.menuTitle.text = "REWIND\n<color=#FFC242>RAMPAGE</color>";
            view.menuLead.text = "YOUR PAST IS YOUR WEAPON.";
            view.menuInstructions.text = TitleInstructions;
            view.menuStats.text = "3 ACTS / 90s REC / 30s PLAYBACK / 90s ARCHIVE";
            view.instructions.text = "WASD TO RECORD A PATH. LURE THE RED CHASERS BEHIND YOU.";
            view.primaryLabel.text = "PLAY TAPE  >  [ SPACE ]";
            view.enemyCount.text = "HOSTILES  00";
            view.notice.text = view.bannerTitle.text = view.bannerDetail.text = view.chainLabel.text = string.Empty;
            view.nodeStatus.text = "REC / PLAYBACK / THE ARCHIVE / DEMO v0.4.1";
        }

        public void SetArchivePreview()
        {
            SetEnglishPreview();
            view.menu.SetActive(false);
            view.archiveEntry.gameObject.SetActive(false);
            view.phase.text = ArchivePhases[0];
            view.recordState.text = "ARCHIVE";
            view.timer.text = "01:30";
            view.enemyCount.text = "ECHOES 2/2 / REWINDING 0";
            view.nodeStatus.text = "CORE ONLINE AT 00:30";
            view.instructions.text = "DODGE LOCKED RED PATHS. BREAK THE CORE, THEN SURVIVE TO 00:00.";
        }

        private void OnDestroy()
        {
            if (game == null || view == null) return;
            view.primary.onClick.RemoveListener(ActivatePrimary);
            view.restart.onClick.RemoveListener(game.SecondaryAction);
            view.archiveEntry.onClick.RemoveListener(Act3TestEntry.LoadArchive);
            view.mute.onClick.RemoveListener(game.ToggleMute);
        }
    }
}
