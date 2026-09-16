namespace CrossFire
{
    /// <summary>
    /// Identifies every prefab type managed by <see cref="ObjectPooler"/>.
    /// Used as the dictionary key so pooling never relies on string lookups or tags.
    /// </summary>
    public enum PoolObjectType
    {
        PlayerLaser,
        EnemyProjectile,
        EnemyDrone,
        Debris,
        BreachHazard
    }
}
