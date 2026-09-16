using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CrossFire
{
    /// <summary>
    /// Pure display layer — placeholder default-uGUI HUD with zero gameplay logic of its own.
    /// Reads from GameManager / RoleManager / ShipController / ShieldSystem and mirrors their
    /// state into Text/Image components. Per-frame values (timer, hull, score, cooldown fills)
    /// only write to a Text.text when the *displayed* value actually changes, to avoid the
    /// recurring string-allocation cost of updating UI text every frame for values that mostly
    /// don't change between frames (see README > Mobile performance).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Top bar")]
        [SerializeField] private Text timerText;
        [SerializeField] private Text hullText;
        [SerializeField] private Image[] hullIcons;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text phaseText;

        [Header("Role labels")]
        [SerializeField] private Text p1RoleLabel;
        [SerializeField] private Text p2RoleLabel;
        [SerializeField] private Image p1FrameOutline;
        [SerializeField] private Image p2FrameOutline;

        [Header("Center banner")]
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private Text bannerText;
        [SerializeField] private GameObject restartButtonRoot;

        [Header("Player 1 control panels")]
        [SerializeField] private GameObject p1PilotPanel;
        [SerializeField] private GameObject p1GunnerPanel;
        [SerializeField] private Image p1BoostFill;
        [SerializeField] private Image p1ShieldFill;

        [Header("Player 2 control panels")]
        [SerializeField] private GameObject p2PilotPanel;
        [SerializeField] private GameObject p2GunnerPanel;
        [SerializeField] private Image p2BoostFill;
        [SerializeField] private Image p2ShieldFill;

        [SerializeField] private ShieldSystem shieldSystem;

        private int _lastDisplayedSecond = -1;
        private int _lastDisplayedHull = -1;
        private int _lastDisplayedScore = -1;
        private GamePhase _lastDisplayedPhase;
        private bool _phaseInitialized;

        private Coroutine _bannerRoutine;

        private void Start()
        {
            // Subscribing here rather than OnEnable: Unity only guarantees every object's Awake
            // runs before any Start, not that Awake on one object precedes OnEnable on another —
            // OnEnable ordering across different GameObjects is unspecified. All the singletons
            // this HUD depends on assign their static Instance in Awake, so Start is the first
            // point it's safe to read them.
            GameManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            GameManager.Instance.OnScoreChanged += HandleScoreChanged;
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            GameManager.Instance.OnFluxWarning += HandleFluxWarning;
            GameManager.Instance.OnFluxBanner += HandleFluxBanner;
            RoleManager.Instance.OnRolesSwapped += HandleRolesSwapped;
            ShipController.Instance.OnHullChanged += HandleHullChanged;

            // Initialize static UI to the starting state (Patrol, P1=Pilot/P2=Gunner).
            HandleRolesSwapped(RoleManager.Instance.Player1Role, RoleManager.Instance.Player2Role);
            HandleHullChanged(ShipController.Instance.Hull);
            HandleScoreChanged(0);
            SetPhaseText(GamePhase.Patrol);
            if (bannerRoot != null) bannerRoot.SetActive(false);
            if (restartButtonRoot != null) restartButtonRoot.SetActive(false);
        }

        private void OnDisable()
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            GameManager.Instance.OnScoreChanged -= HandleScoreChanged;
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            GameManager.Instance.OnFluxWarning -= HandleFluxWarning;
            GameManager.Instance.OnFluxBanner -= HandleFluxBanner;
            if (RoleManager.Instance != null) RoleManager.Instance.OnRolesSwapped -= HandleRolesSwapped;
            if (ShipController.Instance != null) ShipController.Instance.OnHullChanged -= HandleHullChanged;
        }

        private void Update()
        {
            UpdateTimer();
            UpdateCooldownFills();
        }

        private void UpdateTimer()
        {
            int second = Mathf.CeilToInt(GameManager.Instance.RemainingTime);
            if (second != _lastDisplayedSecond)
            {
                _lastDisplayedSecond = second;
                int minutes = second / 60;
                int seconds = second % 60;
                if (timerText != null) timerText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        private void UpdateCooldownFills()
        {
            float boost01 = ShipController.Instance.BoostCooldown01;
            float shield01 = shieldSystem != null ? shieldSystem.CooldownNormalized : 0f;

            if (p1BoostFill != null) p1BoostFill.fillAmount = boost01;
            if (p2BoostFill != null) p2BoostFill.fillAmount = boost01;
            if (p1ShieldFill != null) p1ShieldFill.fillAmount = shield01;
            if (p2ShieldFill != null) p2ShieldFill.fillAmount = shield01;
        }

        private static readonly Color HullFilledColor = new Color(0.3f, 0.85f, 1f);
        private static readonly Color HullEmptyColor = new Color(0.3f, 0.85f, 1f, 0.15f);

        private void HandleHullChanged(int hull)
        {
            if (hull == _lastDisplayedHull) return;
            _lastDisplayedHull = hull;
            if (hullText != null) hullText.text = $"Hull: {hull}/3";

            if (hullIcons != null)
            {
                for (int i = 0; i < hullIcons.Length; i++)
                {
                    if (hullIcons[i] == null) continue;
                    hullIcons[i].color = i < hull ? HullFilledColor : HullEmptyColor;
                }
            }
        }

        private void HandleScoreChanged(int score)
        {
            if (score == _lastDisplayedScore) return;
            _lastDisplayedScore = score;
            if (scoreText != null) scoreText.text = $"Score: {score}";
        }

        private void HandlePhaseChanged(GamePhase phase) => SetPhaseText(phase);

        private void SetPhaseText(GamePhase phase)
        {
            if (_phaseInitialized && phase == _lastDisplayedPhase) return;
            _phaseInitialized = true;
            _lastDisplayedPhase = phase;
            if (phaseText != null) phaseText.text = $"Phase: {phase}";
        }

        // Role colour follows the role itself, not the player, matching the reference mockups:
        // whichever player is currently Pilot shows cyan, whichever is currently Gunner shows red.
        private static readonly Color PilotColor = new Color(0.3f, 0.85f, 1f);
        private static readonly Color GunnerColor = new Color(1f, 0.4f, 0.45f);

        private void HandleRolesSwapped(Role p1Role, Role p2Role)
        {
            if (p1RoleLabel != null)
            {
                p1RoleLabel.text = $"PLAYER 1 - {(p1Role == Role.Pilot ? "PILOT" : "GUNNER")}";
                p1RoleLabel.color = p1Role == Role.Pilot ? PilotColor : GunnerColor;
            }
            if (p2RoleLabel != null)
            {
                p2RoleLabel.text = $"PLAYER 2 - {(p2Role == Role.Pilot ? "PILOT" : "GUNNER")}";
                p2RoleLabel.color = p2Role == Role.Pilot ? PilotColor : GunnerColor;
            }
            if (p1FrameOutline != null) p1FrameOutline.color = p1Role == Role.Pilot ? PilotColor : GunnerColor;
            if (p2FrameOutline != null) p2FrameOutline.color = p2Role == Role.Pilot ? PilotColor : GunnerColor;

            if (p1PilotPanel != null) p1PilotPanel.SetActive(p1Role == Role.Pilot);
            if (p1GunnerPanel != null) p1GunnerPanel.SetActive(p1Role == Role.Gunner);
            if (p2PilotPanel != null) p2PilotPanel.SetActive(p2Role == Role.Pilot);
            if (p2GunnerPanel != null) p2GunnerPanel.SetActive(p2Role == Role.Gunner);
        }

        private void HandleFluxWarning(int seconds)
        {
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(FluxWarningCountdown(seconds));
        }

        private IEnumerator FluxWarningCountdown(int seconds)
        {
            if (bannerRoot != null) bannerRoot.SetActive(true);
            for (int remaining = seconds; remaining >= 1; remaining--)
            {
                if (bannerText != null) bannerText.text = $"QUANTUM FLUX IN {remaining}...";
                yield return new WaitForSeconds(1f);
            }
        }

        private void HandleFluxBanner(string message)
        {
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(ShowBannerThenHide(message, 2f));
        }

        private void HandleGameStateChanged(GameRoundState state)
        {
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            string message = state == GameRoundState.Won ? "HUMAN VICTORY" : "ORBITAL BREACH - GAME OVER";
            if (bannerRoot != null) bannerRoot.SetActive(true);
            if (bannerText != null) bannerText.text = message;
            // Only shown once the round actually ends (win or loss) - never during the 2s flux
            // banner or the 3-2-1 warning countdown, both of which also use bannerRoot.
            if (restartButtonRoot != null) restartButtonRoot.SetActive(true);
        }

        private IEnumerator ShowBannerThenHide(string message, float duration)
        {
            if (bannerRoot != null) bannerRoot.SetActive(true);
            if (bannerText != null) bannerText.text = message;
            yield return new WaitForSeconds(duration);
            if (GameManager.Instance.IsRoundActive && bannerRoot != null)
            {
                bannerRoot.SetActive(false);
            }
        }
    }
}
