using UnityEngine;

namespace CrossFire
{
    /// <summary>Red square. 1 hit, score 10. Moves down and periodically fires a downward
    /// enemy projectile from the pool.</summary>
    public class EnemyDrone : HazardBase
    {
        [SerializeField] private float minFireInterval = 1.2f;
        [SerializeField] private float maxFireInterval = 2.2f;
        [SerializeField] private float projectileBaseSpeed = 6f;

        private float _fireTimer;

        public override PoolObjectType PoolType => PoolObjectType.EnemyDrone;

        public override void OnSpawnFromPool()
        {
            base.OnSpawnFromPool();
            _fireTimer = Random.Range(minFireInterval, maxFireInterval);
        }

        protected override void Update()
        {
            base.Update();
            if (!GameManager.Instance.IsRoundActive) return;

            _fireTimer -= Time.deltaTime;
            if (_fireTimer <= 0f)
            {
                FireProjectile();
                _fireTimer = Random.Range(minFireInterval, maxFireInterval);
            }
        }

        private void FireProjectile()
        {
            GameObject proj = ObjectPooler.Instance.SpawnFromPool(
                PoolObjectType.EnemyProjectile, transform.position, Quaternion.identity);

            if (proj != null && proj.TryGetComponent(out EnemyProjectile projectile))
            {
                float speed = projectileBaseSpeed * GameManager.Instance.ProjectileSpeedMultiplier;
                projectile.Launch(Vector2.down, speed);
            }
        }
    }
}
