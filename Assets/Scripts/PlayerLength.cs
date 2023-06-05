// using System.Collections.Generic;
// using UnityEngine;
// using Unity.Netcode;

// public class PlayerLength : NetworkBehaviour
// {
//     [SerializeField] private GameObject tailPrefab;

//     public static event System.Action<ushort> ChangedLengthEvent;

//     private List<GameObject> _tails;
//     private Transform _lastTail;
//     private Collider2D _collider2D;

//     public NetworkVariable<ushort> _length = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

//     public override void OnNetworkSpawn()
//     {
//         base.OnNetworkSpawn();
//         _tails = new List<GameObject>();
//         _lastTail = transform;
//         _collider2D = GetComponent<Collider2D>();
//         if(!IsServer) _length.OnValueChanged += OnLengthChanged;
//         if (IsOwner) return;
//         for (int i = 0; i < _length.Value - 1; ++i)
//             InstantiateTail();
//     }

//     public override void OnNetworkDespawn()
//     {
//         base.OnNetworkDespawn();
//         DestroyTails();
//     }
//     private void DestroyTails()
//     {
//         while(_tails.Count != 0)
//         {
//             GameObject tail = _tails[0];
//             _tails.RemoveAt(0);
//             Destroy(tail);
//         }
//     }
    
//     // This will be called on the server only
//     public void AddTail()
//     {
//         _length.Value++;
//         LengthChanged();

//     }

//     // This will be called on the client only to sync tails from other clients
//     private void OnLengthChanged(ushort previousValue, ushort newValue)
//     {
//         LengthChanged();
//     }

//     private void LengthChanged()
//     {
//         InstantiateTail();

//         if(!IsOwner) return;
//         ChangedLengthEvent?.Invoke(_length.Value);
//         ClientEatSound.Instance.PlayEatSound();
//     }

//     private void InstantiateTail()
//     {
//         GameObject tailGO = Instantiate(tailPrefab, transform.position, Quaternion.identity);
//         tailGO.GetComponent<SpriteRenderer>().sortingOrder = -_length.Value;
//         if(tailGO.TryGetComponent(out Tail tail))
//         {
//             tail.networkOwner = transform;
//             tail.followTransform = _lastTail;
//             _lastTail = tailGO.transform;
//             Physics2D.IgnoreCollision(_collider2D, tailGO.GetComponent<Collider2D>());
//         }
//         _tails.Add(tailGO);
//     }
// }
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerLength : NetworkBehaviour
{
    [SerializeField] private GameObject tailPrefab;

    public static event System.Action<ushort> ChangedLengthEvent;

    private List<GameObject> _tails;
    private Transform _lastTail;
    private Collider2D _collider2D;

    public NetworkVariable<ushort> _length = new NetworkVariable<ushort>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _tails = new List<GameObject>();
        _lastTail = transform;
        _collider2D = GetComponent<Collider2D>();
        if (!IsServer) _length.OnValueChanged += OnLengthChanged;
        if (IsOwner)
        {
            for (int i = 0; i < _length.Value - 1; ++i)
            {
                InstantiateTail();
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        DestroyTails();
    }

    private void DestroyTails()
    {
        foreach (GameObject tail in _tails)
        {
            Destroy(tail);
        }
        _tails.Clear();
    }

    // This will be called on the server only
    public void AddTail()
    {
        _length.Value++;
        LengthChanged();
    }

    // This will be called on the client only to sync tails from other clients
    private void OnLengthChanged(ushort previousValue, ushort newValue)
    {
        LengthChanged();
    }

    private void LengthChanged()
    {
        InstantiateTail();

        if (!IsOwner) return;

        ChangedLengthEvent?.Invoke(_length.Value);
        ClientEatSound.Instance.PlayEatSound();
    }

    private void InstantiateTail()
    {
        GameObject tailGO = Instantiate(tailPrefab, transform.position, Quaternion.identity);
        tailGO.GetComponent<SpriteRenderer>().sortingOrder = -_length.Value;

        if (tailGO.TryGetComponent(out Tail tail))
        {
            tail.networkOwner = transform;
            tail.followTransform = _lastTail;
            _lastTail = tailGO.transform;
            Physics2D.IgnoreCollision(_collider2D, tailGO.GetComponent<Collider2D>());
        }

        _tails.Add(tailGO);
    }
}
