using System.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    public CueStick cueStick;
    public Ball ballPrefab;
    
    public NetworkVariable<int> Turn = new NetworkVariable<int>(0);
    
    private Ball startBall;
    private bool isTurnEnd;
    private ulong moveBalls;
    
    private void Start()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        if (IsServer)
        {
            for (int i = 0; i < 16; i++)
            {
                Ball ball = Instantiate(ballPrefab, new Vector3(Random.Range(-20f, 20f), 0.5f, Random.Range(-10, 10)), Quaternion.identity);
                ball.index = i;
                NetworkObject netObj = ball.GetComponent<NetworkObject>();
                netObj.Spawn();
            
                if (i == 0)
                    startBall = ball;
            }
        }
        
        moveBalls = 0;
        cueStick.target = startBall;
    }
    
    private void Update()
    {
        if (isTurnEnd && moveBalls == 0)
        {
            isTurnEnd =  false;
            NextTurn();
        }
    }
    
    public void EndTurn()
    {
        AddMoveBall(0);
        StartCoroutine(StartEndTurn());
    }

    private IEnumerator StartEndTurn()
    {
        while (startBall.rb.linearVelocity.magnitude <= 0.1f) yield return null;
        cueStick.enabled = false;
        isTurnEnd = true;
    }

    private void NextTurn()
    {
        cueStick.enabled = true;
        NextTurnServerRpc();
    }
    
    [Rpc(SendTo.Server)]
    private void NextTurnServerRpc()
    {
        var connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
        Turn.Value = (Turn.Value + 1) % connectedIds.Count;
        cueStick.NetworkObject.ChangeOwnership(connectedIds[Turn.Value]);
    }

    public void AddMoveBall(int ballIndex)
    {
        moveBalls |= (ulong)1 << ballIndex;
    }

    public void RemoveMoveBall(int ballIndex)
    {
        if (!isTurnEnd || (moveBalls & (ulong)1 << ballIndex) == 0)
            return;
        
        moveBalls &= ~((ulong)1 << ballIndex);
    }
}
