using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Shared world-space playfield extents, derived once from the main orthographic camera.
    /// Lazily initialized on first access instead of relying on Script Execution Order —
    /// any system (ShipController, EnemySpawner, boundary placement helpers) can safely read
    /// these values the first time it needs them, regardless of Awake/Start ordering.
    /// </summary>
    public static class PlayfieldBounds
    {
        public static float MinX { get; private set; }
        public static float MaxX { get; private set; }
        public static float MinY { get; private set; }
        public static float MaxY { get; private set; }

        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[PlayfieldBounds] No camera tagged MainCamera found in the scene.");
                return;
            }

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 camPos = cam.transform.position;

            MinX = camPos.x - halfWidth;
            MaxX = camPos.x + halfWidth;
            MinY = camPos.y - halfHeight;
            MaxY = camPos.y + halfHeight;

            _initialized = true;
        }

        /// <summary>Forces recalculation next call. Useful if the camera or its orthographic
        /// size can change at runtime (e.g. device orientation change) — not exercised by the
        /// current prototype but kept as the correct extension point.</summary>
        public static void Invalidate() => _initialized = false;
    }
}
