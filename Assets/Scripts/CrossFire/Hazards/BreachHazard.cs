namespace CrossFire
{
    /// <summary>Orange diamond. 3 hits, score 25. Starts spawning in the Alert phase. Reaching
    /// the bottom boundary causes an immediate loss (handled by BottomBoundary, not here) —
    /// a direct collision with the ship does not, by itself, damage the hull.</summary>
    public class BreachHazard : HazardBase
    {
        public override PoolObjectType PoolType => PoolObjectType.BreachHazard;
    }
}
