using System.Collections;
using System.Collections.Generic;
using Unity.Burst.Intrinsics;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    public CueStick cueStick;
    public Ball ballPrefab;
    public Material[] materials;
    public Transform[] startPoints;
    public Transform whiteBallPoint;
    public Transform blackBallPoint;
    
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
            startBall = CreateBall(0, whiteBallPoint);
            CreateBall(8, blackBallPoint);
            
            List<Transform> positions = new List<Transform>(startPoints);
            for (int i = 0; i < startPoints.Length; i++)
            {
                int posIdx = Random.Range(0, positions.Count);
                int idx = i + 1;
                if (idx == 8)  idx = 15;
                
                CreateBall(idx, positions[posIdx]);
                positions.RemoveAt(posIdx);
            }
        }
        
        moveBalls = 0;
        cueStick.target = startBall;
        cueStick.StickOnOff(true);
    }

    private Ball CreateBall(int index, Transform parent)
    {
        Ball ball = Instantiate(ballPrefab, parent.position, parent.rotation);
        ball.transform.position += new Vector3(Random.Range(-0.01f, 0.01f), 0, Random.Range(-0.01f, 0.01f));
        
        ball.index = index;
        MeshRenderer meshRenderer = ball.GetComponentInChildren<MeshRenderer>();
        meshRenderer.material = materials[index];
        NetworkObject netObj = ball.GetComponent<NetworkObject>();
        netObj.Spawn();

        return ball;
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
        cueStick.StickOnOff(false);
        isTurnEnd = true;
    }

    private void NextTurn()
    {
        if (startBall.isGoal)
        {
            startBall.isGoal = false;
            startBall.gameObject.SetActive(true);
            startBall.transform.position = whiteBallPoint.position;
        }
        
        cueStick.StickOnOff(true);
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
