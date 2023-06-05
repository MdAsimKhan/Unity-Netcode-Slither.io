using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Object Pool for networked objects, used for controlling how objects are spawned by Netcode. Netcode by default will allocate new memory when spawning new
/// objects. With this Networked Pool, we're using custom spawning to reuse objects.
/// Boss Room uses this for projectiles. In theory it should use this for imps too, but we wanted to show vanilla spawning vs pooled spawning.
/// Hooks to NetworkManager's prefab handler to intercept object spawning and do custom actions
/// </summary>
public class NetworkObjectPool : NetworkBehaviour
{
    private static NetworkObjectPool _instance;

    public static NetworkObjectPool Singleton { get { return _instance; } }

    [SerializeField]
    List<PoolConfigObject> PooledPrefabsList;

    HashSet<GameObject> prefabs = new HashSet<GameObject>();

    private Dictionary<GameObject, Queue<NetworkObject>> pooledObjects = new Dictionary<GameObject, Queue<NetworkObject>>();
    private Dictionary<GameObject, int> nonPooledObjects = new Dictionary<GameObject, int>();

    private bool m_HasInitialized = false;

    public void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }

    public override void OnNetworkSpawn()
    {
        InitializePool();
    }

    public override void OnNetworkDespawn()
    {
        ClearPool();
    }

    public void OnValidate()
    {
        for (var i = 0; i < PooledPrefabsList.Count; i++)
        {
            var prefab = PooledPrefabsList[i].Prefab;
            if (prefab != null)
            {
                Assert.IsNotNull(prefab.GetComponent<NetworkObject>(), $"{nameof(NetworkObjectPool)}: Pooled prefab \"{prefab.name}\" at index {i.ToString()} has no {nameof(NetworkObject)} component.");
            }
        }
    }

    /// <summary>
    /// Gets an instance of the given prefab from the pool. The prefab must be registered to the pool.
    /// </summary>
    /// <param name="prefab"></param>
    /// <returns></returns>
    public NetworkObject GetNetworkObject(GameObject prefab)
    {
        return GetNetworkObjectInternal(prefab, Vector3.zero, Quaternion.identity);
    }

    /// <summary>
    /// Gets an instance of the given prefab from the pool. The prefab must be registered to the pool.
    /// </summary>
    /// <param name="prefab"></param>
    /// <param name="position">The position to spawn the object at.</param>
    /// <param name="rotation">The rotation to spawn the object with.</param>
    /// <returns></returns>
    public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return GetNetworkObjectInternal(prefab, position, rotation);
    }

    /// <summary>
    /// Return an object to the pool (reset objects before returning).
    /// </summary>
    public void ReturnNetworkObject(NetworkObject networkObject, GameObject prefab)
    {
        // Debug.Log("Returning Object");
        var go = networkObject.gameObject;
        go.SetActive(false);
        pooledObjects[prefab].Enqueue(networkObject);
        nonPooledObjects[prefab]--;
    }

    /// <summary>
    /// Returns how many of the specified prefab have been instantiated but are not in the pool.
    /// </summary>
    public int GetCurrentPrefabCount(GameObject prefab)
    {
        return nonPooledObjects[prefab];
    }

    /// <summary>
    /// Adds a prefab to the list of spawnable prefabs.
    /// </summary>
    /// <param name="prefab">The prefab to add.</param>
    /// <param name="prewarmCount"></param>
    public void AddPrefab(GameObject prefab, int prewarmCount = 0)
    {
        var networkObject = prefab.GetComponent<NetworkObject>();

        Assert.IsNotNull(networkObject, $"{nameof(prefab)} must have {nameof(networkObject)} component.");
        Assert.IsFalse(prefabs.Contains(prefab), $"Prefab {prefab.name} is already registered in the pool.");

        RegisterPrefabInternal(prefab, prewarmCount);
    }

    /// <summary>
    /// Builds up the cache for a prefab.
    /// </summary>
    private void RegisterPrefabInternal(GameObject prefab, int prewarmCount)
    {
        prefabs.Add(prefab);

        var prefabQueue = new Queue<NetworkObject>();
        pooledObjects[prefab] = prefabQueue;
        for (int i = 0; i < prewarmCount; i++)
        {
            var go = CreateInstance(prefab);
            ReturnNetworkObject(go.GetComponent<NetworkObject>(), prefab);
        }

        // Register Netcode Spawn handlers
        NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, new PooledPrefabInstanceHandler(prefab, this));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private GameObject CreateInstance(GameObject prefab)
    {
        return Instantiate(prefab);
    }

    /// <summary>
    /// This matches the signature of <see cref="NetworkSpawnManager.SpawnHandlerDelegate"/>
    /// </summary>
    /// <param name="prefab"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    private NetworkObject GetNetworkObjectInternal(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var queue = pooledObjects[prefab];

        NetworkObject networkObject;
        if (queue.Count > 0)
        {
            networkObject = queue.Dequeue();
        }
        else
        {
            networkObject = CreateInstance(prefab).GetComponent<NetworkObject>();
        }

        nonPooledObjects[prefab]++;

        // Here we must reverse the logic in ReturnNetworkObject.
        var go = networkObject.gameObject;
        go.SetActive(true);

        go.transform.position = position;
        go.transform.rotation = rotation;
        return networkObject;
    }

    /// <summary>
    /// Registers all objects in <see cref="PooledPrefabsList"/> to the cache.
    /// </summary>
    public void InitializePool()
    {
        if (m_HasInitialized) return;
        foreach (var configObject in PooledPrefabsList)
        {
            nonPooledObjects[configObject.Prefab] = 0;
            RegisterPrefabInternal(configObject.Prefab, configObject.PrewarmCount);
            nonPooledObjects[configObject.Prefab] = 0;
        }
        m_HasInitialized = true;
    }

    /// <summary>
    /// Unregisters all objects in <see cref="PooledPrefabsList"/> from the cache.
    /// </summary>
    public void ClearPool()
    {
        foreach (var prefab in prefabs)
        {
            // Unregister Netcode Spawn handlers
            NetworkManager.Singleton.PrefabHandler.RemoveHandler(prefab);
        }
        pooledObjects.Clear();
    }
}

[Serializable]
struct PoolConfigObject
{
    public GameObject Prefab;
    public int PrewarmCount;
}

class PooledPrefabInstanceHandler : INetworkPrefabInstanceHandler
{
    GameObject m_Prefab;
    NetworkObjectPool m_Pool;

    public PooledPrefabInstanceHandler(GameObject prefab, NetworkObjectPool pool)
    {
        m_Prefab = prefab;
        m_Pool = pool;
    }

    NetworkObject INetworkPrefabInstanceHandler.Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        var netObject = m_Pool.GetNetworkObject(m_Prefab, position, rotation);
        return netObject;
    }

    void INetworkPrefabInstanceHandler.Destroy(NetworkObject networkObject)
    {
        m_Pool.ReturnNetworkObject(networkObject, m_Prefab);
    }
}
// using System;
// using System.Collections.Generic;
// using Unity.Netcode;
// using UnityEngine;
// using UnityEngine.Assertions;
// using UnityEngine.Pool;

// namespace Unity.BossRoom.Infrastructure
// {
//     /// <summary>
//     /// Object Pool for networked objects, used for controlling how objects are spawned by Netcode. Netcode by default
//     /// will allocate new memory when spawning new objects. With this Networked Pool, we're using the ObjectPool to
//     /// reuse objects.
//     /// Boss Room uses this for projectiles. In theory it should use this for imps too, but we wanted to show vanilla spawning vs pooled spawning.
//     /// Hooks to NetworkManager's prefab handler to intercept object spawning and do custom actions.
//     /// </summary>
//     public class NetworkObjectPool : NetworkBehaviour
//     {
//         public static NetworkObjectPool Singleton { get; private set; }

//         // public GameObject prefab;
//         [SerializeField]
//         List<PoolConfigObject> PooledPrefabsList;

//         HashSet<GameObject> m_Prefabs = new HashSet<GameObject>();

//         Dictionary<GameObject, ObjectPool<NetworkObject>> m_PooledObjects = new Dictionary<GameObject, ObjectPool<NetworkObject>>();
//         Dictionary<GameObject, int> nonPooledObjects = new Dictionary<GameObject, int>();

//         public void Awake()
//         {
//             if (Singleton != null && Singleton != this)
//             {
//                 Destroy(gameObject);
//             }
//             else
//             {
//                 Singleton = this;
//             }
//         }

//         public override void OnNetworkSpawn()
//         {
//             // Registers all objects in PooledPrefabsList to the cache.
//             foreach (var configObject in PooledPrefabsList)
//             {
//                 nonPooledObjects[prefab] = 0; // asim
//                 RegisterPrefabInternal(configObject.Prefab, configObject.PrewarmCount);
//                 nonPooledObjects[prefab] = 0; // asim
//             }
//         }

//         public override void OnNetworkDespawn()
//         {
//             // Unregisters all objects in PooledPrefabsList from the cache.
//             foreach (var prefab in m_Prefabs)
//             {
//                 // Unregister Netcode Spawn handlers
//                 NetworkManager.Singleton.PrefabHandler.RemoveHandler(prefab);
//                 m_PooledObjects[prefab].Clear();
//             }
//             m_PooledObjects.Clear();
//             m_Prefabs.Clear();
//         }

//         public void OnValidate()
//         {
//             for (var i = 0; i < PooledPrefabsList.Count; i++)
//             {
//                 var prefab = PooledPrefabsList[i].Prefab;
//                 if (prefab != null)
//                 {
//                     Assert.IsNotNull(prefab.GetComponent<NetworkObject>(), $"{nameof(NetworkObjectPool)}: Pooled prefab \"{prefab.name}\" at index {i.ToString()} has no {nameof(NetworkObject)} component.");
//                 }
//             }
//         }

//         /// <summary>
//         /// Gets an instance of the given prefab from the pool. The prefab must be registered to the pool.
//         /// </summary>
//         /// <remarks>
//         /// To spawn a NetworkObject from one of the pools, this must be called on the server, then the instance
//         /// returned from it must be spawned on the server. This method will then also be called on the client by the
//         /// PooledPrefabInstanceHandler when the client receives a spawn message for a prefab that has been registered
//         /// here.
//         /// </remarks>
//         /// <param name="prefab"></param>
//         /// <param name="position">The position to spawn the object at.</param>
//         /// <param name="rotation">The rotation to spawn the object with.</param>
//         /// <returns></returns>
//         public NetworkObject GetNetworkObject(GameObject prefab, Vector3 position, Quaternion rotation)
//         {
//             var networkObject = m_PooledObjects[prefab].Get();

//             var noTransform = networkObject.transform;
//             noTransform.position = position;
//             noTransform.rotation = rotation;
//             nonPooledObjects[prefab]++; // asim
//             return networkObject;
//         }

//         /// <summary>
//         /// Return an object to the pool (reset objects before returning).
//         /// </summary>
//         public void ReturnNetworkObject(NetworkObject networkObject, GameObject prefab)
//         {
//             m_PooledObjects[prefab].Release(networkObject);
//             nonPooledObjects[prefab]--; // asim
//         }
//         // asim defined
//         public int GetCurrentPrefabCount(NetworkObject networkObject)
//         {
//             return nonPooledObjects[prefab];
//         }

//         /// <summary>
//         /// Builds up the cache for a prefab.
//         /// </summary>
//         void RegisterPrefabInternal(GameObject prefab, int prewarmCount)
//         {
//             NetworkObject CreateFunc()
//             {
//                 return Instantiate(prefab).GetComponent<NetworkObject>();
//             }

//             void ActionOnGet(NetworkObject networkObject)
//             {
//                 networkObject.gameObject.SetActive(true);
//             }

//             void ActionOnRelease(NetworkObject networkObject)
//             {
//                 networkObject.gameObject.SetActive(false);
//             }

//             void ActionOnDestroy(NetworkObject networkObject)
//             {
//                 Destroy(networkObject.gameObject);
//             }

//             m_Prefabs.Add(prefab);

//             // Create the pool
//             m_PooledObjects[prefab] = new ObjectPool<NetworkObject>(CreateFunc, ActionOnGet, ActionOnRelease, ActionOnDestroy, defaultCapacity: prewarmCount);

//             // Populate the pool
//             var prewarmNetworkObjects = new List<NetworkObject>();
//             for (var i = 0; i < prewarmCount; i++)
//             {
//                 prewarmNetworkObjects.Add(m_PooledObjects[prefab].Get());
//             }
//             foreach (var networkObject in prewarmNetworkObjects)
//             {
//                 m_PooledObjects[prefab].Release(networkObject);
//             }

//             // Register Netcode Spawn handlers
//             NetworkManager.Singleton.PrefabHandler.AddHandler(prefab, new PooledPrefabInstanceHandler(prefab, this));
//         }
//     }

//     [Serializable]
//     struct PoolConfigObject
//     {
//         public GameObject Prefab;
//         public int PrewarmCount;
//     }

//     class PooledPrefabInstanceHandler : INetworkPrefabInstanceHandler
//     {
//         GameObject m_Prefab;
//         NetworkObjectPool m_Pool;

//         public PooledPrefabInstanceHandler(GameObject prefab, NetworkObjectPool pool)
//         {
//             m_Prefab = prefab;
//             m_Pool = pool;
//         }

//         NetworkObject INetworkPrefabInstanceHandler.Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
//         {
//             return m_Pool.GetNetworkObject(m_Prefab, position, rotation);
//         }

//         void INetworkPrefabInstanceHandler.Destroy(NetworkObject networkObject)
//         {
//             m_Pool.ReturnNetworkObject(networkObject, m_Prefab);
//         }
//     }

// }