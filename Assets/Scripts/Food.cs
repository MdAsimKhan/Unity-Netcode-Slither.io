using Unity.Netcode;
using UnityEngine;

public class Food : NetworkBehaviour
{
    public GameObject prefab;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(!collision.CompareTag("Player")) return;

        if (!NetworkManager.Singleton.IsServer) return;

        if (collision.TryGetComponent(out PlayerLength playerLength))
        {
            playerLength.AddTail();
        }

        else if (collision.TryGetComponent(out Tail tail))
        {
            tail.networkOwner.GetComponent<PlayerLength>().AddTail();
        }

        NetworkObjectPool.Singleton.ReturnNetworkObject(NetworkObject, prefab);
        
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}
