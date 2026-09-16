using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Gunner-only weapon. Fires a pooled laser toward the current aim world position on a
    /// fixed cadence. The cooldown timer decrements every frame regardless of input state, so
    /// clicking rapidly cannot bypass it — only whether Fire is held when the timer expires
    /// determines whether a shot goes out.
    /// </summary>
    public class WeaponSystem : MonoBehaviour
    {
        [SerializeField] private float fireCooldown = 0.25f;
        [SerializeField] private float laserSpeed = 20f;
        [SerializeField] private Transform muzzlePoint;

        private float _cooldownTimer;

        private void Update()
        {
            if (!GameManager.Instance.IsRoundActive) return;

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (MobileInputController.Instance.GunnerFireHeld && _cooldownTimer <= 0f)
            {
                Fire();
                _cooldownTimer = fireCooldown;
            }
        }

        private void Fire()
        {
            Vector3 muzzlePos = muzzlePoint != null ? muzzlePoint.position : transform.position;
            Vector3 target = MobileInputController.Instance.GunnerAimWorldPosition;

            Vector2 direction = (Vector2)(target - muzzlePos);
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            direction.Normalize();

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);

            GameObject laserObj = ObjectPooler.Instance.SpawnFromPool(PoolObjectType.PlayerLaser, muzzlePos, rotation);
            if (laserObj != null && laserObj.TryGetComponent(out PlayerLaser laser))
            {
                laser.Launch(direction, laserSpeed);
            }
        }
    }
}
