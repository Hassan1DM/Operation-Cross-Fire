using UnityEngine;

namespace CrossFire
{
    /// <summary>Red rectangle. Travels in a fixed direction (straight down) at a
    /// spawner/shooter-assigned speed. Damages an unshielded ship on contact — that collision
    /// is resolved by ShipController, which also returns this instance to its pool.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnemyProjectile : MonoBehaviour, IPoolableObject
    {
        private Vector2 _direction;
        private float _speed;

        public PoolObjectType PoolType => PoolObjectType.EnemyProjectile;

        public void Launch(Vector2 direction, float speed)
        {
            _direction = direction.normalized;
            _speed = speed;
        }

        private void Update()
        {
            if (!GameManager.Instance.IsRoundActive) return;
            transform.position += (Vector3)(_direction * (_speed * Time.deltaTime));
        }

        public void OnSpawnFromPool() { }

        public void OnDespawnToPool()
        {
            _direction = Vector2.zero;
            _speed = 0f;
        }
    }
}
