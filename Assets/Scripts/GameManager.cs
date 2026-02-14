using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    public CueStick cueStick;
    private List<Rigidbody> moveBalls;

    public NetworkVariable<int> Turn = new NetworkVariable<int>(0);
    
    private bool isTurnEnd;
    
    private void Start()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        moveBalls = new List<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        Turn.OnValueChanged += (value, newValue) => 
        {
            
        };
    }

    [Rpc(SendTo.Server)]
    private void NextTurnServerRpc()
    {
        var connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
        Turn.Value = (Turn.Value + 1) % connectedIds.Count;
        cueStick.NetworkObject.ChangeOwnership(connectedIds[Turn.Value]);
    }

    private void Update()
    {
        if (isTurnEnd && moveBalls.Count == 0)
        {
            isTurnEnd =  false;
            NextTurn();
        }
    }

    public void EndTurn()
    {
        isTurnEnd = true;
    }

    private void NextTurn()
    {
        NextTurnServerRpc();
    }
    
    private void Reset()
    {
        Turn.Value = 0;
    }
}
