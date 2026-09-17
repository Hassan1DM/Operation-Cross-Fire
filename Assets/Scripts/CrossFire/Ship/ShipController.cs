using System;
using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// The one shared interceptor. Handles horizontal movement (clamped to the playfield),
    /// Boost, hull/damage resolution and post-hit invulnerability. Shield state lives in the
    /// sibling <see cref="ShieldSystem"/> component and is only *read* here.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ShipController : MonoBehaviour
    {
        public static ShipController Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float boundsMargin = 0.4f;

        [Header("Boost (Pilot only)")]
        [SerializeField] private float boostMultiplier = 1.75f;
        [SerializeField] private float boostDuration = 1f;
        [SerializeField] private float boostCooldown = 4f;
        [SerializeField] private Color boostTintColor = new Color(1f, 0.8f, 0.2f, 1f); // Warm gold

        [Header("Hull")]
        [SerializeField] private int maxHull = 3;
        [SerializeField] private float invulnerabilityDuration = 1.5f;
        [SerializeField] private float invulnerabilityBlinkInterval = 0.1f;

        private ShieldSystem _shieldSystem;
        private SpriteRenderer _spriteRenderer;

        private bool _isBoosting;
        private float _boostTimer;
        private float _boostCooldownTimer;

        private bool _isInvulnerable;
        private float _invulnerabilityTimer;
        private float _blinkTimer;

        public int Hull { get; private set; }
        public float BoostCooldown01 => boostCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(_boostCooldownTimer / boostCooldown);
        public bool IsBoosting => _isBoosting;

        public event Action<int> OnHullChanged;
        public event Action OnShipDamaged;

        private void Awake()
        {
            Instance = this;
            _shieldSystem = GetComponent<ShieldSystem>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            PlayfieldBounds.EnsureInitialized();
            Hull = maxHull;
            OnHullChanged?.Invoke(Hull);
        }

        private void Update()
        {
            if (!GameManager.Instance.IsRoundActive) return;

            TickTimers();
            HandleBoostRequest();
            HandleMovement();
        }

        private void TickTimers()
        {
            float dt = Time.deltaTime;

            if (_boostCooldownTimer > 0f) _boostCooldownTimer -= dt;

            if (_isBoosting)
            {
                _boostTimer -= dt;
                if (_boostTimer <= 0f) _isBoosting = false;
            }

            // Visual feedback: tint the ship gold while Boosting so the player sees it's active.
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _isBoosting ? boostTintColor : Color.white;
            }

            if (_isInvulnerable)
            {
                _invulnerabilityTimer -= dt;
                _blinkTimer -= dt;
                if (_blinkTimer <= 0f)
                {
                    _blinkTimer = invulnerabilityBlinkInterval;
                    _spriteRenderer.enabled = !_spriteRenderer.enabled;
                }
                if (_invulnerabilityTimer <= 0f)
                {
                    _isInvulnerable = false;
                    _spriteRenderer.enabled = true;
                }
            }
        }

        private void HandleBoostRequest()
        {
            if (_boostCooldownTimer <= 0f && MobileInputController.Instance.TryConsumeBoost())
            {
                _isBoosting = true;
                _boostTimer = boostDuration;
                _boostCooldownTimer = boostCooldown;
            }
        }

        private void HandleMovement()
        {
            float axis = MobileInputController.Instance.PilotMoveAxis;
            float speed = moveSpeed * (_isBoosting ? boostMultiplier : 1f);

            Vector3 pos = transform.position;
            pos.x += axis * speed * Time.deltaTime;
            pos.x = Mathf.Clamp(pos.x, PlayfieldBounds.MinX + boundsMargin, PlayfieldBounds.MaxX - boundsMargin);
            transform.position = pos;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!GameManager.Instance.IsRoundActive) return;

            // Per the brief, hull damage sources are exactly: enemy body, debris body, enemy
            // projectile. Breach hazards only ever cause a loss via the bottom boundary — a
            // direct collision with one does not, by itself, damage the hull. Documented in
            // README > Assumptions.
            if (other.TryGetComponent(out EnemyDrone enemy))
            {
                ApplyHullDamage();
                ObjectPooler.Instance.ReturnToPool(PoolObjectType.EnemyDrone, enemy.gameObject);
            }
            else if (other.TryGetComponent(out Debris debris))
            {
                ApplyHullDamage();
                ObjectPooler.Instance.ReturnToPool(PoolObjectType.Debris, debris.gameObject);
            }
            else if (other.TryGetComponent(out EnemyProjectile projectile))
            {
                ApplyHullDamage();
                ObjectPooler.Instance.ReturnToPool(PoolObjectType.EnemyProjectile, projectile.gameObject);
            }
        }

        private void ApplyHullDamage()
        {
            if (_isInvulnerable) return;
            if (_shieldSystem != null && _shieldSystem.IsShieldActive) return;

            Hull = Mathf.Max(0, Hull - 1);
            OnHullChanged?.Invoke(Hull);
            OnShipDamaged?.Invoke();

            _isInvulnerable = true;
            _invulnerabilityTimer = invulnerabilityDuration;
            _blinkTimer = invulnerabilityBlinkInterval;

            if (Hull <= 0)
            {
                GameManager.Instance.TriggerLoss();
            }
        }
    }
}
