using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Phase-aware spawner for enemy drones, debris and breach hazards. Each hazard type has
    /// its own base interval, independently scaled every tick by
    /// <see cref="GameManager.SpawnIntervalMultiplier"/> so the effect of entering Critical is
    /// immediate. Breach hazards only start once the round has left Patrol.
    /// Uses a simple accumulator (no coroutines), so there is nothing to allocate per spawn tick.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Enemy Drone")]
        [SerializeField] private float enemyBaseInterval = 1.0f;
        [SerializeField] private float enemyBaseSpeed = 3f;

        [Header("Debris")]
        [SerializeField] private float debrisBaseInterval = 2.0f;
        [SerializeField] private float debrisBaseSpeed = 2f;

        [Header("Breach Hazard (Alert phase onward)")]
        [SerializeField] private float breachBaseInterval = 4.0f;
        [SerializeField] private float breachBaseSpeed = 2.2f;

        [SerializeField] private float spawnMarginX = 0.5f;
        [SerializeField] private float spawnMarginY = 0.5f;

        private float _enemyTimer;
        private float _debrisTimer;
        private float _breachTimer;

        private void Start()
        {
            PlayfieldBounds.EnsureInitialized();
            _enemyTimer = enemyBaseInterval;
            _debrisTimer = debrisBaseInterval;
            _breachTimer = breachBaseInterval;
        }

        private void Update()
        {
            if (!GameManager.Instance.IsRoundActive) return;

            float dt = Time.deltaTime;
            float intervalMultiplier = GameManager.Instance.SpawnIntervalMultiplier;

            _enemyTimer -= dt;
            if (_enemyTimer <= 0f)
            {
                SpawnEnemy();
                _enemyTimer = enemyBaseInterval * intervalMultiplier;
            }

            _debrisTimer -= dt;
            if (_debrisTimer <= 0f)
            {
                SpawnDebris();
                _debrisTimer = debrisBaseInterval * intervalMultiplier;
            }

            if (GameManager.Instance.CurrentPhase != GamePhase.Patrol)
            {
                _breachTimer -= dt;
                if (_breachTimer <= 0f)
                {
                    SpawnBreachHazard();
                    _breachTimer = breachBaseInterval * intervalMultiplier;
                }
            }
        }

        private float RandomSpawnX() => Random.Range(PlayfieldBounds.MinX + spawnMarginX, PlayfieldBounds.MaxX - spawnMarginX);
        private Vector3 SpawnPosition() => new Vector3(RandomSpawnX(), PlayfieldBounds.MaxY + spawnMarginY, 0f);

        private void SpawnEnemy()
        {
            GameObject go = ObjectPooler.Instance.SpawnFromPool(PoolObjectType.EnemyDrone, SpawnPosition(), Quaternion.identity);
            if (go != null && go.TryGetComponent(out EnemyDrone drone))
            {
                drone.SetSpeed(enemyBaseSpeed * GameManager.Instance.EnemySpeedMultiplier);
            }
        }

        private void SpawnDebris()
        {
            GameObject go = ObjectPooler.Instance.SpawnFromPool(PoolObjectType.Debris, SpawnPosition(), Quaternion.identity);
            if (go != null && go.TryGetComponent(out Debris debris))
            {
                debris.SetSpeed(debrisBaseSpeed * GameManager.Instance.EnemySpeedMultiplier);
            }
        }

        private void SpawnBreachHazard()
        {
            GameObject go = ObjectPooler.Instance.SpawnFromPool(PoolObjectType.BreachHazard, SpawnPosition(), Quaternion.identity);
            if (go != null && go.TryGetComponent(out BreachHazard hazard))
            {
                hazard.SetSpeed(breachBaseSpeed * GameManager.Instance.EnemySpeedMultiplier);
            }
        }
    }
}
