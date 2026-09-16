using UnityEngine;

namespace CrossFire
{
    /// <summary>Travels from the ship's muzzle toward the reticle position it was fired at
    /// (straight-line, not homing). On hitting a hazard, the hazard's own trigger handler
    /// (see HazardBase) is responsible for returning this laser to its pool, so a hit is only
    /// ever resolved once.</summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerLaser : MonoBehaviour, IPoolableObject
    {
        private Vector2 _direction;
        private float _speed;

        public PoolObjectType PoolType => PoolObjectType.PlayerLaser;

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
