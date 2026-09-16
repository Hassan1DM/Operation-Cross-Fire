using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace CrossFire
{
    /// <summary>
    /// Reloads the active scene when tapped/clicked — a testing convenience so the round can be
    /// replayed without leaving Play Mode. Shown only at round end (see HUDController).
    ///
    /// Deliberately hit-tested by hand against this RectTransform, the same way
    /// MobileInputController resolves its zones, rather than wired through Unity's
    /// EventSystem/Button/GraphicRaycaster. This is a one-button, testing-only affordance —
    /// not a production menu — so it reuses the project's existing manual input pattern instead
    /// of adding an EventSystem + InputSystemUIInputModule for a single click target.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class RestartButton : MonoBehaviour
    {
        private RectTransform _rect;

        private void Awake() => _rect = GetComponent<RectTransform>();

        private void Update()
        {
            if (TryGetTapScreenPosition(out Vector2 screenPos) &&
                RectTransformUtility.RectangleContainsScreenPoint(_rect, screenPos, null))
            {
                Restart();
            }
        }

        private static bool TryGetTapScreenPosition(out Vector2 screenPos)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }

            var touches = Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].phase == TouchPhase.Began)
                {
                    screenPos = touches[i].screenPosition;
                    return true;
                }
            }

            screenPos = default;
            return false;
        }

        private static void Restart()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex);
        }
    }
}
