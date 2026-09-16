namespace CrossFire
{
    /// <summary>
    /// Contract every pooled prefab must implement. <see cref="ObjectPooler"/> calls
    /// <see cref="OnSpawnFromPool"/> after re-activating an instance and
    /// <see cref="OnDespawnToPool"/> right before it is deactivated and returned to its queue.
    /// Implementations must fully reset their own gameplay state here (health, timers,
    /// velocity, collision flags, visual state) — the pooler itself only handles
    /// transform placement and GameObject activation.
    /// </summary>
    public interface IPoolableObject
    {
        /// <summary>Which pool this instance belongs to. Lets boundary/cleanup code
        /// return an object without needing to know its concrete type.</summary>
        PoolObjectType PoolType { get; }

        void OnSpawnFromPool();
        void OnDespawnToPool();
    }
}
