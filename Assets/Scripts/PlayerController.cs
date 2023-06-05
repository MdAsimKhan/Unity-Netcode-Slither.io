using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [SerializeField] private float speed = 3f;
    public static event System.Action GameOverEvent;
    private Camera _mainCamera;
    private bool _canCollide = true;
    private Vector3 _mouseInput = Vector3.zero;
    private PlayerLength _playerLength;
    private readonly ulong[] _targetClientsArray = new ulong[1];

    private void Initialize()
    {
        _mainCamera = Camera.main;
        _playerLength = GetComponent<PlayerLength>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Initialize();
    }

    private void Update()
    {
        if (!IsOwner || !Application.isFocused) return;
        // Movement
        _mouseInput.x = Input.mousePosition.x;
        _mouseInput.y = Input.mousePosition.y;
        _mouseInput.z = _mainCamera.nearClipPlane;
        Vector3 mouseWorldCoordinates = _mainCamera.ScreenToWorldPoint(_mouseInput);
        mouseWorldCoordinates.z = 0f;
        transform.position = Vector3.MoveTowards(transform.position, mouseWorldCoordinates, speed * Time.deltaTime);

        // Rotation
        if(mouseWorldCoordinates != transform.position)
        {
            Vector3 targetDirection = mouseWorldCoordinates - transform.position;
            targetDirection.z = 0f;
            transform.up = targetDirection;
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void DetermineWinnerServerRpc(PlayerData player1, PlayerData player2)
    {
        if(player1.length > player2.length)
        {
            WinInformationServerRpc(player1.Id, player2.Id);
        }
        else
        {
            WinInformationServerRpc(player2.Id, player1.Id);
        }
    }

    [ServerRpc]
    private void WinInformationServerRpc(ulong winnerId, ulong loserId)
    {
        _targetClientsArray[0] = winnerId;
        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = _targetClientsArray
            }
        };
        AtePlayerClientRpc(clientRpcParams);

        _targetClientsArray[0] = loserId;
        clientRpcParams.Send.TargetClientIds = _targetClientsArray;
        GameOverClientRpc(clientRpcParams);
    }

    [ClientRpc]
    private void AtePlayerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if(!IsOwner) return;
        Debug.Log("Ate Player");
    }

    [ClientRpc]
    private void GameOverClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if(!IsOwner) return;
        Debug.Log("Game Over");
        GameOverEvent?.Invoke();
        NetworkManager.Singleton.Shutdown();
    }

    private IEnumerator CollisionDelay()
    {
        _canCollide = false;
        yield return new WaitForSeconds(0.5f);
        _canCollide = true;
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        Debug.Log("Player Collision");
        if(!col.gameObject.CompareTag("Player")) return;
        if(!IsOwner) return;
        if(!_canCollide) return;
        StartCoroutine(CollisionDelay());

        if(col.gameObject.TryGetComponent(out PlayerLength playerLength))
        {
            var player1 = new PlayerData()
            {
                Id = OwnerClientId,
                length = _playerLength._length.Value
            };
            var player2 = new PlayerData()
            {
                Id = playerLength.OwnerClientId,
                length = playerLength._length.Value
            };
            DetermineWinnerServerRpc(player1, player2);
        }
        else if(col.gameObject.TryGetComponent(out Tail tail))
        {
            WinInformationServerRpc(tail.networkOwner.GetComponent<PlayerController>().OwnerClientId, OwnerClientId);
        }
    }

    struct PlayerData : INetworkSerializable
    {
        public ulong Id;
        public ushort length;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Id);
            serializer.SerializeValue(ref length);
        }
    }
}
