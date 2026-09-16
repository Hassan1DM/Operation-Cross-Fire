using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Shared behaviour for every downward-moving, laser-damageable hazard (Enemy Drone,
    /// Debris, Breach Hazard): straight-line downward movement at a spawner-assigned speed,
    /// a hit-count health pool, and score-on-destruction. Concrete subclasses only need to
    /// supply their pool type, max health and score value, plus any extra behaviour
    /// (EnemyDrone additionally fires projectiles).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class HazardBase : MonoBehaviour, IPoolableObject
    {
        [SerializeField] protected int maxHealth = 1;
        [SerializeField] protected int scoreValue = 10;

        protected int currentHealth;
        protected float currentMoveSpeed;

        public abstract PoolObjectType PoolType { get; }

        /// <summary>Called by EnemySpawner immediately after SpawnFromPool to apply the
        /// current phase's speed multiplier.</summary>
        public void SetSpeed(float unitsPerSecond) => currentMoveSpeed = unitsPerSecond;

        protected virtual void Update()
        {
            if (!GameManager.Instance.IsRoundActive) return;
            transform.position += Vector3.down * (currentMoveSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerLaser laser))
            {
                ApplyDamage(1);
                ObjectPooler.Instance.ReturnToPool(PoolObjectType.PlayerLaser, laser.gameObject);
            }
        }

        protected void ApplyDamage(int amount)
        {
            currentHealth -= amount;
            if (currentHealth <= 0)
            {
                GameManager.Instance.AddScore(scoreValue);
                ObjectPooler.Instance.ReturnToPool(PoolType, gameObject);
            }
        }

        public virtual void OnSpawnFromPool()
        {
            currentHealth = maxHealth;
        }

        public virtual void OnDespawnToPool()
        {
            currentMoveSpeed = 0f;
        }
    }
}
