using System;
using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Single source of truth for round time, phase, difficulty multipliers, score and
    /// win/loss state. Drives the Quantum Flux transition as ONE state machine (one round
    /// timer, one phase index) rather than separate role-swap and flux timers, per the brief.
    ///
    /// Quantum Flux sequence (executed atomically inside <see cref="ExecuteFluxTransition"/>):
    ///   1. Cancel active input      -> MobileInputController.Instance.CancelAllInputs()
    ///   2. Swap roles                -> RoleManager.Instance.SwapRoles()
    ///   3. Apply new phase values    -> ApplyPhaseMultipliers(newPhase)
    ///   4. Update UI                 -> HUD listens to OnPhaseChanged / OnRolesSwapped
    ///   5. Show banner               -> OnFluxBanner event, consumed by HUDController
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Round Timing")]
        [SerializeField] private float roundDuration = 60f;
        [SerializeField] private float alertPhaseStart = 20f;
        [SerializeField] private float criticalPhaseStart = 40f;
        [SerializeField] private float fluxWarningLeadTime = 3f;

        [Header("Difficulty Multipliers (read-only at runtime, set per phase below)")]
        [SerializeField] private float alertEnemySpeedMultiplier = 1.25f;
        [SerializeField] private float criticalSpawnIntervalMultiplier = 0.70f;
        [SerializeField] private float criticalProjectileSpeedMultiplier = 1.50f;

        public float ElapsedTime { get; private set; }
        public float RemainingTime => Mathf.Max(0f, roundDuration - ElapsedTime);
        public GamePhase CurrentPhase { get; private set; } = GamePhase.Patrol;
        public GameRoundState State { get; private set; } = GameRoundState.Playing;
        public bool IsRoundActive => State == GameRoundState.Playing;
        public int Score { get; private set; }

        // Difficulty scalars consumed by EnemySpawner / hazard scripts.
        public float EnemySpeedMultiplier { get; private set; } = 1f;
        public float SpawnIntervalMultiplier { get; private set; } = 1f;
        public float ProjectileSpeedMultiplier { get; private set; } = 1f;

        public event Action<GamePhase> OnPhaseChanged;
        public event Action<int> OnScoreChanged;
        public event Action<GameRoundState> OnGameStateChanged;
        /// <summary>Fired once, fluxWarningLeadTime seconds before a transition, with the
        /// countdown length in whole seconds (e.g. 3).</summary>
        public event Action<int> OnFluxWarning;
        /// <summary>Fired at the moment of transition with the banner text to display.</summary>
        public event Action<string> OnFluxBanner;

        private int _phaseIndex; // 0 = Patrol, 1 = Alert, 2 = Critical
        private bool _alertWarningFired;
        private bool _criticalWarningFired;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            PlayfieldBounds.EnsureInitialized();
        }

        private void Update()
        {
            if (!IsRoundActive) return;

            ElapsedTime += Time.deltaTime;

            // --- Flux warnings (3s before each transition) ---
            if (!_alertWarningFired && ElapsedTime >= alertPhaseStart - fluxWarningLeadTime)
            {
                _alertWarningFired = true;
                OnFluxWarning?.Invoke(Mathf.CeilToInt(fluxWarningLeadTime));
            }
            if (!_criticalWarningFired && ElapsedTime >= criticalPhaseStart - fluxWarningLeadTime)
            {
                _criticalWarningFired = true;
                OnFluxWarning?.Invoke(Mathf.CeilToInt(fluxWarningLeadTime));
            }

            // --- Phase transitions ---
            if (_phaseIndex == 0 && ElapsedTime >= alertPhaseStart)
            {
                _phaseIndex = 1;
                ExecuteFluxTransition(GamePhase.Alert);
            }
            else if (_phaseIndex == 1 && ElapsedTime >= criticalPhaseStart)
            {
                _phaseIndex = 2;
                ExecuteFluxTransition(GamePhase.Critical);
            }

            // --- Win condition ---
            if (ElapsedTime >= roundDuration)
            {
                TriggerWin();
            }
        }

        private void ExecuteFluxTransition(GamePhase newPhase)
        {
            // 1. Cancel active input (movement, firing, Boost, Shield, touches).
            if (MobileInputController.Instance != null)
            {
                MobileInputController.Instance.CancelAllInputs();
            }

            // 2. Swap Pilot/Gunner assignments.
            if (RoleManager.Instance != null)
            {
                RoleManager.Instance.SwapRoles();
            }

            // 3. Apply the new phase's difficulty values.
            ApplyPhaseMultipliers(newPhase);
            CurrentPhase = newPhase;

            // 4. Notify listeners (HUD updates role labels/control panels here).
            OnPhaseChanged?.Invoke(CurrentPhase);

            // 5. Banner.
            OnFluxBanner?.Invoke("QUANTUM FLUX -- ROLES REVERSED");
        }

        private void ApplyPhaseMultipliers(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Patrol:
                    EnemySpeedMultiplier = 1f;
                    SpawnIntervalMultiplier = 1f;
                    ProjectileSpeedMultiplier = 1f;
                    break;
                case GamePhase.Alert:
                    // Enemy speed becomes base x1.25; breach hazards begin (handled by EnemySpawner
                    // checking CurrentPhase != Patrol). Spawn interval and projectile speed unchanged.
                    EnemySpeedMultiplier = alertEnemySpeedMultiplier;
                    SpawnIntervalMultiplier = 1f;
                    ProjectileSpeedMultiplier = 1f;
                    break;
                case GamePhase.Critical:
                    // Alert's enemy-speed increase carries forward; spawn interval and projectile
                    // speed additionally ramp up. See README "Assumptions" for why this is cumulative.
                    EnemySpeedMultiplier = alertEnemySpeedMultiplier;
                    SpawnIntervalMultiplier = criticalSpawnIntervalMultiplier;
                    ProjectileSpeedMultiplier = criticalProjectileSpeedMultiplier;
                    break;
            }
        }

        public void AddScore(int amount)
        {
            if (!IsRoundActive) return;
            Score += amount;
            OnScoreChanged?.Invoke(Score);
        }

        private void TriggerWin()
        {
            if (!IsRoundActive) return;
            State = GameRoundState.Won;
            ElapsedTime = roundDuration;
            OnGameStateChanged?.Invoke(State);
        }

        /// <summary>Called by ShipController when hull reaches 0, or by the bottom boundary
        /// when a breach hazard reaches it.</summary>
        public void TriggerLoss()
        {
            if (!IsRoundActive) return;
            State = GameRoundState.Lost;
            OnGameStateChanged?.Invoke(State);
        }
    }
}
