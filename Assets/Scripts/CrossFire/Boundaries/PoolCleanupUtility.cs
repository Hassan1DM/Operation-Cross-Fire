using UnityEngine;

namespace CrossFire
{
    /// <summary>Shared helper so both boundary types return escaped objects the same way.</summary>
    public static class PoolCleanupUtility
    {
        public static void ReturnIfPoolable(Collider2D other)
        {
            if (other.TryGetComponent(out IPoolableObject poolable))
            {
                ObjectPooler.Instance.ReturnToPool(poolable.PoolType, other.gameObject);
            }
        }
    }
}
