namespace CrossFire
{
    /// <summary>Grey circle. 2 hits, score 15. Moves down, damages the ship on contact
    /// (handled generically by ShipController.OnTriggerEnter2D).</summary>
    public class Debris : HazardBase
    {
        public override PoolObjectType PoolType => PoolObjectType.Debris;
    }
}
