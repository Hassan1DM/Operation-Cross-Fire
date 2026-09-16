using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Gunner-only shield. While active, ShipController's damage resolution reads
    /// <see cref="IsShieldActive"/> and skips the hull decrement entirely. Purely visual
    /// feedback is a child GameObject (a circle/outline sprite) toggled on/off.
    /// </summary>
    public class ShieldSystem : MonoBehaviour
    {
        [SerializeField] private float shieldDuration = 1.5f;
        [SerializeField] private float shieldCooldown = 5f;
        [SerializeField] private GameObject shieldVisual;

        private float _activeTimer;
        private float _cooldownTimer;

        public bool IsShieldActive { get; private set; }
        public float CooldownNormalized => shieldCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(_cooldownTimer / shieldCooldown);

        private void Update()
        {
            if (!GameManager.Instance.IsRoundActive)
            {
                if (IsShieldActive) Deactivate();
                return;
            }

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (IsShieldActive)
            {
                _activeTimer -= Time.deltaTime;
                if (_activeTimer <= 0f) Deactivate();
            }
            else if (_cooldownTimer <= 0f && MobileInputController.Instance.TryConsumeShieldActivation())
            {
                Activate();
            }
        }

        private void Activate()
        {
            IsShieldActive = true;
            _activeTimer = shieldDuration;
            _cooldownTimer = shieldCooldown;
            if (shieldVisual != null) shieldVisual.SetActive(true);
        }

        private void Deactivate()
        {
            IsShieldActive = false;
            if (shieldVisual != null) shieldVisual.SetActive(false);
        }
    }
}
