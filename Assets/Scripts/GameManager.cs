using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class GameManager : NetworkBehaviour
{
    public static GameManager instance;

    public CueStick cueStick;
    public Ball[] balls;
    // public Material[] materials;
    public Transform[] startPoints;
    public Transform whiteBallPoint;
    public Transform blackBallPoint;
    
    public NetworkVariable<int> turn = new NetworkVariable<int>(0);
    public NetworkVariable<bool> isTargetColor = new NetworkVariable<bool>();
    private NetworkVariable<bool> isFirstGoal = new NetworkVariable<bool>(true);
    
    public ulong goalBalls;
    
    [SerializeField]
    private Ball startBall;
    private bool isTurnEnd;
    private ulong movingBalls;
    private ulong GoalBallsThisTurn;
    private ulong MoveBallsThisTurn;
    
    private void Start()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);

        if (IsServer)
        {
            SetPositionBall(0, whiteBallPoint);
            SetPositionBall(8, blackBallPoint);
            
            List<Transform> positions = new List<Transform>(startPoints);
            for (int i = 0; i < startPoints.Length; i++)
            {
                int posIdx = Random.Range(0, positions.Count);
                int idx = i + 1;
                if (idx == 8)  idx = 15;
                
                SetPositionBall(idx, positions[posIdx]);
                positions.RemoveAt(posIdx);
            }
        }
        
        goalBalls = 0;
        movingBalls = 0;
        GoalBallsThisTurn = 0;
        MoveBallsThisTurn = 0;
        
        // cueStick.target = startBall;
        cueStick.StickOnOffRpc(true);
    }

    private void SetPositionBall(int index, Transform pos)
    {
        Ball ball = balls[index];
        ball.transform.position = pos.position;
        ball.transform.position += new Vector3(Random.Range(-0.01f, 0.01f), 0, Random.Range(-0.01f, 0.01f));
    }
    
    private void Update()
    {
        if (isTurnEnd && movingBalls == 0)
        {
            isTurnEnd =  false;
            NextTurn();
        }
    }

    [Rpc(SendTo.Everyone)]
    private void GameEndEveryoneRpc()
    {
        
    }
    
    public void EndTurn()
    {
        AddMoveBall(0);
        StartCoroutine(StartEndTurn());
    }

    private IEnumerator StartEndTurn()
    {
        while (startBall.rb.linearVelocity.magnitude <= 0.1f) yield return null;
        cueStick.StickOnOffRpc(false);
        isTurnEnd = true;
    }

    private void NextTurn()
    {
        if ((goalBalls & ((ulong)1 << 8)) > 0)
        {
            Debug.Log("GameEnd");
            GameEndEveryoneRpc();
            return;
        }

        if (isFirstGoal.Value)
        {
            if (GoalBallsThisTurn == 0)
                NextTurnServerRpc();
            else
                SettingBallServerRpc();
        }
        
        if (!isFirstGoal.Value)
        {
            bool isColorGoal = (GoalBallsThisTurn & 0b11111110) > 0;
            bool isLineGoal = (GoalBallsThisTurn & 0b1111111100000000) > 0;
            if (isTargetColor.Value && !isColorGoal) NextTurnServerRpc();
            if (!isTargetColor.Value && !isLineGoal) NextTurnServerRpc();
        }
        
        MoveBallsThisTurn &= ~(ulong)1;
        if (startBall.isGoal || MoveBallsThisTurn == 0)
        {
            startBall.isGoal = false;
            startBall.gameObject.SetActive(true);
            startBall.transform.position = whiteBallPoint.position;
            NextTurnServerRpc();
        }
        
        cueStick.StickOnOffRpc(true);
        GoalBallsThisTurn = 0;
        MoveBallsThisTurn = 0;
    }

    [Rpc(SendTo.Server)]
    private void SettingBallServerRpc()
    {
        bool isColorGoal = (GoalBallsThisTurn & 0b11111110) > 0;
        bool isLineGoal = (GoalBallsThisTurn & 0b1111111100000000) > 0;
        if ((isColorGoal && isLineGoal) || (!isColorGoal && !isLineGoal))
            return;

        isTargetColor.Value = isColorGoal;
        isFirstGoal.Value = false;
    }
    
    [Rpc(SendTo.Server)]
    private void NextTurnServerRpc()
    {
        var connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
        turn.Value = (turn.Value + 1) % connectedIds.Count;
        cueStick.NetworkObject.ChangeOwnership(connectedIds[turn.Value]);
    }

    public void AddGoalBall(int ballIndex)
    {
        ulong value = (ulong)1 << ballIndex;
        goalBalls |= value;
        GoalBallsThisTurn |= value;
    }

    public void AddMoveBall(int ballIndex)
    {
        ulong value = (ulong)1 << ballIndex;
        movingBalls |= value;
        MoveBallsThisTurn |= value;
    }

    public void RemoveMovingBall(int ballIndex)
    {
        if (!isTurnEnd || (movingBalls & (ulong)1 << ballIndex) == 0)
            return;
        
        movingBalls &= ~((ulong)1 << ballIndex);
    }
}
