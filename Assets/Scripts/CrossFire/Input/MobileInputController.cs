using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace CrossFire
{
    /// <summary>
    /// Single source of truth for both editor (keyboard/mouse) and physical-device (multi-touch)
    /// input, published as per-frame polled state rather than fire-and-forget events, since
    /// movement/aim/fire are all "held" style inputs.
    ///
    /// Input ownership model:
    /// - Player 1 always owns the LEFT half of the screen, Player 2 always owns the RIGHT half.
    ///   That physical ownership never changes.
    /// - What each half CONTROLS (Pilot buttons vs Gunner aim/fire/shield) depends on that
    ///   player's current Role, read from RoleManager at the moment a touch begins.
    /// - Each touch is tracked by its Touch.touchId from the moment it begins until it ends or
    ///   is cancelled, and keeps the control zone it started in even if the finger later drifts
    ///   across the centre line — ownership is decided once, at touch-down, and never reassigned.
    /// - Fire/Shield zones never feed the aim reticle, even while held, since they're resolved
    ///   to a different ControlZone than AimArea at touch-down.
    ///
    /// Editor keyboard/mouse fallback is compiled only into editor/development builds.
    /// </summary>
    public class MobileInputController : MonoBehaviour
    {
        public static MobileInputController Instance { get; private set; }

        private enum ControlZone { None, Left, Right, Boost, AimArea, Fire, Shield }

        private struct TouchOwner
        {
            public PlayerSlot slot;
            public ControlZone zone;
        }

        [Header("Player 1 (left half) zones")]
        [SerializeField] private RectTransform p1Left;
        [SerializeField] private RectTransform p1Right;
        [SerializeField] private RectTransform p1Boost;
        [SerializeField] private RectTransform p1AimArea;
        [SerializeField] private RectTransform p1Fire;
        [SerializeField] private RectTransform p1Shield;

        [Header("Player 2 (right half) zones")]
        [SerializeField] private RectTransform p2Left;
        [SerializeField] private RectTransform p2Right;
        [SerializeField] private RectTransform p2Boost;
        [SerializeField] private RectTransform p2AimArea;
        [SerializeField] private RectTransform p2Fire;
        [SerializeField] private RectTransform p2Shield;

        [SerializeField] private Camera worldCamera;

        // Published, per-frame state consumed by ShipController / WeaponSystem / ShieldSystem.
        public float PilotMoveAxis { get; private set; }
        public Vector3 GunnerAimWorldPosition { get; private set; }
        public bool GunnerFireHeld { get; private set; }

        private bool _boostRequested;
        private bool _shieldRequested;

        private readonly Dictionary<int, TouchOwner> _activeTouches = new Dictionary<int, TouchOwner>(10);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (worldCamera == null) worldCamera = Camera.main;
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            HandleEditorInput();
#endif
            HandleTouchInput();
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        private void HandleEditorInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                float axis = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) axis -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) axis += 1f;
                PilotMoveAxis = axis;

                if (kb.spaceKey.wasPressedThisFrame) _boostRequested = true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && worldCamera != null)
            {
                Vector2 screenPos = mouse.position.ReadValue();
                GunnerAimWorldPosition = ScreenToWorld(screenPos);
                GunnerFireHeld = mouse.leftButton.isPressed;

                if (mouse.rightButton.wasPressedThisFrame) _shieldRequested = true;
            }
        }
#endif

        private void HandleTouchInput()
        {
            var touches = Touch.activeTouches;
            for (int i = 0; i < touches.Count; i++)
            {
                Touch touch = touches[i];
                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        RegisterTouch(touch.touchId, touch.screenPosition);
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        UpdateTouch(touch.touchId, touch.screenPosition);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        _activeTouches.Remove(touch.touchId);
                        break;
                }
            }

            if (_activeTouches.Count > 0)
            {
                RecomputeHeldStateFromTouches();
            }
        }

        private void RegisterTouch(int touchId, Vector2 screenPos)
        {
            bool isPlayer1 = screenPos.x < Screen.width / 2f;
            PlayerSlot slot = isPlayer1 ? PlayerSlot.Player1 : PlayerSlot.Player2;
            Role role = RoleManager.Instance != null ? RoleManager.Instance.GetRole(slot) : Role.Pilot;

            ControlZone zone = ResolveZone(slot, role, screenPos);
            _activeTouches[touchId] = new TouchOwner { slot = slot, zone = zone };

            if (zone == ControlZone.Boost) _boostRequested = true;
            if (zone == ControlZone.Shield) _shieldRequested = true;
            if (zone == ControlZone.AimArea) GunnerAimWorldPosition = ScreenToWorld(screenPos);
        }

        private void UpdateTouch(int touchId, Vector2 screenPos)
        {
            if (_activeTouches.TryGetValue(touchId, out TouchOwner owner) && owner.zone == ControlZone.AimArea)
            {
                GunnerAimWorldPosition = ScreenToWorld(screenPos);
            }
        }

        private ControlZone ResolveZone(PlayerSlot slot, Role role, Vector2 screenPos)
        {
            if (role == Role.Pilot)
            {
                if (Contains(slot == PlayerSlot.Player1 ? p1Left : p2Left, screenPos)) return ControlZone.Left;
                if (Contains(slot == PlayerSlot.Player1 ? p1Right : p2Right, screenPos)) return ControlZone.Right;
                if (Contains(slot == PlayerSlot.Player1 ? p1Boost : p2Boost, screenPos)) return ControlZone.Boost;
            }
            else
            {
                if (Contains(slot == PlayerSlot.Player1 ? p1Fire : p2Fire, screenPos)) return ControlZone.Fire;
                if (Contains(slot == PlayerSlot.Player1 ? p1Shield : p2Shield, screenPos)) return ControlZone.Shield;
                if (Contains(slot == PlayerSlot.Player1 ? p1AimArea : p2AimArea, screenPos)) return ControlZone.AimArea;
            }
            return ControlZone.None;
        }

        private static bool Contains(RectTransform rect, Vector2 screenPos)
        {
            return rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, null);
        }

        /// <summary>Re-derives axis/fire-held state from whichever touches are still active.
        /// Handles "holding both Left and Right stops movement" and multi-finger Fire naturally,
        /// without fragile per-event increment/decrement bookkeeping.</summary>
        private void RecomputeHeldStateFromTouches()
        {
            bool leftHeld = false, rightHeld = false, fireHeld = false;
            foreach (TouchOwner owner in _activeTouches.Values)
            {
                if (owner.zone == ControlZone.Left) leftHeld = true;
                else if (owner.zone == ControlZone.Right) rightHeld = true;
                else if (owner.zone == ControlZone.Fire) fireHeld = true;
            }

            if (leftHeld || rightHeld)
            {
                PilotMoveAxis = leftHeld && rightHeld ? 0f : (leftHeld ? -1f : 1f);
            }
            if (fireHeld) GunnerFireHeld = true;
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            if (worldCamera == null) return Vector3.zero;
            Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -worldCamera.transform.position.z));
            world.z = 0f;
            return world;
        }

        public bool TryConsumeBoost()
        {
            if (!_boostRequested) return false;
            _boostRequested = false;
            return true;
        }

        public bool TryConsumeShieldActivation()
        {
            if (!_shieldRequested) return false;
            _shieldRequested = false;
            return true;
        }

        /// <summary>Step 1 of the Quantum Flux sequence: cancels all active movement, firing,
        /// Boost/Shield requests and touch ownership. Called by GameManager.</summary>
        public void CancelAllInputs()
        {
            _activeTouches.Clear();
            PilotMoveAxis = 0f;
            GunnerFireHeld = false;
            _boostRequested = false;
            _shieldRequested = false;
        }
    }
}
