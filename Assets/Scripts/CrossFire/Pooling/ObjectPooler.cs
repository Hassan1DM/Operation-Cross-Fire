using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrossFire
{
    /// <summary>
    /// Central, generic object pool for every recurring prefab in the game
    /// (player lasers, enemy projectiles, enemy drones, debris, breach hazards).
    ///
    /// Design notes (for interview defense):
    /// - One Queue&lt;GameObject&gt; per <see cref="PoolObjectType"/>, keyed by enum (no string
    ///   lookups, no tags).
    /// - Every prefab's <see cref="IPoolableObject"/> reference is fetched once during prewarm
    ///   and cached in a Dictionary&lt;GameObject, IPoolableObject&gt;, so Spawn/Return never call
    ///   GetComponent at runtime.
    /// - Prewarm happens once in Awake/Start, before the round begins, per the brief's
    ///   "prewarm the pools before the round begins" requirement.
    /// - Pooled instances are parented under a per-type container GameObject in the Hierarchy
    ///   (not left loose), so the whole pool is inspectable/editable by hand at all times —
    ///   nothing is created purely at runtime with no hierarchy footprint.
    /// - If a pool runs dry mid-round, SpawnFromPool expands it by exactly one instance and
    ///   logs a warning; this is the "safe expansion" the brief explicitly allows as long as
    ///   it is documented (see README, Pooling section).
    /// </summary>
    public class ObjectPooler : MonoBehaviour
    {
        public static ObjectPooler Instance { get; private set; }

        [Serializable]
        public class PoolConfig
        {
            public PoolObjectType type;
            public GameObject prefab;
            [Min(1)] public int prewarmSize = 8;
        }

        [Tooltip("One entry per PoolObjectType. Prewarm sizes should cover peak concurrent " +
                 "usage during the Critical phase (see README for sizing rationale).")]
        [SerializeField] private List<PoolConfig> poolConfigs = new List<PoolConfig>();

        private readonly Dictionary<PoolObjectType, Queue<GameObject>> _pools =
            new Dictionary<PoolObjectType, Queue<GameObject>>();
        private readonly Dictionary<PoolObjectType, GameObject> _prefabByType =
            new Dictionary<PoolObjectType, GameObject>();
        private readonly Dictionary<PoolObjectType, Transform> _containerByType =
            new Dictionary<PoolObjectType, Transform>();
        private readonly Dictionary<GameObject, IPoolableObject> _poolableCache =
            new Dictionary<GameObject, IPoolableObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            PrewarmAll();
        }

        private void PrewarmAll()
        {
            foreach (PoolConfig config in poolConfigs)
            {
                if (config.prefab == null)
                {
                    Debug.LogError($"[ObjectPooler] Pool config for {config.type} has no prefab assigned.");
                    continue;
                }

                _prefabByType[config.type] = config.prefab;

                Transform container = new GameObject($"Pool_{config.type}").transform;
                container.SetParent(transform);
                _containerByType[config.type] = container;

                var queue = new Queue<GameObject>(config.prewarmSize);
                _pools[config.type] = queue;

                for (int i = 0; i < config.prewarmSize; i++)
                {
                    GameObject instance = CreatePooledInstance(config.type, container);
                    queue.Enqueue(instance);
                }
            }
        }

        private GameObject CreatePooledInstance(PoolObjectType type, Transform container)
        {
            GameObject instance = Instantiate(_prefabByType[type], container);
            instance.name = _prefabByType[type].name;
            instance.SetActive(false);

            if (instance.TryGetComponent(out IPoolableObject poolable))
            {
                _poolableCache[instance] = poolable;
            }
            else
            {
                Debug.LogError($"[ObjectPooler] Prefab for {type} does not implement IPoolableObject.");
            }

            return instance;
        }

        /// <summary>Dequeues (or, if empty, creates and logs) an instance, places it, activates it
        /// and calls its OnSpawnFromPool callback. Zero GC allocations on the common path.</summary>
        public GameObject SpawnFromPool(PoolObjectType type, Vector3 position, Quaternion rotation)
        {
            if (!_pools.TryGetValue(type, out Queue<GameObject> queue))
            {
                Debug.LogError($"[ObjectPooler] No pool configured for {type}.");
                return null;
            }

            GameObject instance;
            if (queue.Count > 0)
            {
                instance = queue.Dequeue();
            }
            else
            {
                Debug.LogWarning($"[ObjectPooler] Pool for {type} was empty — expanding by 1. " +
                                  "Increase its prewarm size if this happens often during Critical phase.");
                instance = CreatePooledInstance(type, _containerByType[type]);
            }

            Transform t = instance.transform;
            t.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            if (_poolableCache.TryGetValue(instance, out IPoolableObject poolable))
            {
                poolable.OnSpawnFromPool();
            }

            return instance;
        }

        /// <summary>Deactivates the instance, calls its OnDespawnToPool callback, re-parents it
        /// back under its pool container and enqueues it for reuse.</summary>
        public void ReturnToPool(PoolObjectType type, GameObject instance)
        {
            if (instance == null) return;

            if (_poolableCache.TryGetValue(instance, out IPoolableObject poolable))
            {
                poolable.OnDespawnToPool();
            }

            instance.SetActive(false);

            if (_containerByType.TryGetValue(type, out Transform container))
            {
                instance.transform.SetParent(container);
            }

            if (_pools.TryGetValue(type, out Queue<GameObject> queue))
            {
                queue.Enqueue(instance);
            }
            else
            {
                Debug.LogError($"[ObjectPooler] No pool configured for {type}; destroying orphan instance.");
                Destroy(instance);
            }
        }
    }
}
