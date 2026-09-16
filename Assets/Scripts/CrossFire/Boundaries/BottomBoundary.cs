using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Thin trigger sitting at the very bottom edge of the playfield, on the BottomBoundary
    /// layer (collides only with the shared Enemy/Hazard layer). If the object that reached it
    /// is specifically a Breach Hazard, the round is lost immediately. Any hazard reaching this
    /// line without being destroyed — breach or otherwise — is also returned to its pool, since
    /// by definition it escaped play.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BottomBoundary : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.TryGetComponent(out BreachHazard _))
            {
                GameManager.Instance.TriggerLoss();
            }
            PoolCleanupUtility.ReturnIfPoolable(other);
        }
    }
}
