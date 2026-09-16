using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Generic off-screen cleanup trigger, placed on the CleanupBoundary layer around the
    /// top/left/right edges of the playfield (and mirrored below BottomBoundary for anything
    /// that isn't a hazard). Catches player lasers and enemy projectiles that miss everything
    /// and fly off-screen, returning them to their pool instead of leaking active instances.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CleanupBoundary : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other) => PoolCleanupUtility.ReturnIfPoolable(other);
    }
}
