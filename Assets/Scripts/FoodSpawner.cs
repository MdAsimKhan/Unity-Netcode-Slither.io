using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class FoodSpawner : NetworkBehaviour
{
    private const int MaxPrefabCount = 30;
    [SerializeField] private GameObject prefab;

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += SpawnFoodStart;
    }

    private void SpawnFoodStart()
    {
        Debug.Log("SpawnFoodStart");
        NetworkManager.Singleton.OnServerStarted -= SpawnFoodStart;
        NetworkObjectPool.Singleton.InitializePool();

        for (int i = 0; i < 10; i++)
        {
            SpawnFood();
        }
    }

    private void SpawnFood()
    {
        NetworkObject obj = NetworkObjectPool.Singleton.GetNetworkObject(prefab, RandomPosition(), Quaternion.identity);

        Food foodComponent = obj.GetComponent<Food>();
        if (foodComponent != null)
        {
            foodComponent.prefab = prefab;
        }

        if (!obj.IsSpawned)
        {
            obj.Spawn(true);
        }

        StartCoroutine(SpawnOverTime());
    }

    private Vector3 RandomPosition()
    {
        return new Vector3(Random.Range(-9f, 9f), Random.Range(-6f, 6f), 0f);
    }

    private IEnumerator SpawnOverTime()
    {
        while (NetworkManager.Singleton.ConnectedClients.Count > 0)
        {
            yield return new WaitForSeconds(3f);
            if (NetworkObjectPool.Singleton.GetCurrentPrefabCount(prefab) < MaxPrefabCount)
            {
                SpawnFood();
            }
        }
    }
}
