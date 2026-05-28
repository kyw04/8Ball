using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
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

    [SerializeField] private TextMeshProUGUI currentPlayerText;

    public NetworkVariable<int> turn = new NetworkVariable<int>(0);
    public NetworkVariable<bool> isTargetColor = new NetworkVariable<bool>();
    private NetworkVariable<bool> isFirstGoal = new NetworkVariable<bool>(true);

    // Server-only: maps NGO clientId → player name
    private readonly Dictionary<ulong, FixedString32Bytes> _playerNames = new();

    public ulong goalBalls;

    [SerializeField]
    private Ball startBall;
    private bool isTurnEnd;
    private ulong movingBalls;
    private ulong GoalBallsThisTurn;
    private ulong MoveBallsThisTurn;

    // 흰 공이 멈추기를 기다리기 위한 임계값
    [SerializeField] private float settleVelocityThreshold = 0.1f;

    public override void OnNetworkSpawn()
    {
        var relayManager = FindAnyObjectByType<RelayNetworkManager>();
        if (relayManager != null)
            RegisterPlayerRpc(relayManager.playerData);
    }

    public override void OnNetworkDespawn()
    {
        _playerNames.Clear();
    }

    private void Awake()
    {
        // 싱글톤은 Awake 에서 확정한다. Start 에서 잡으면 같은 프레임의
        // 다른 객체가 instance 를 null 로 보게 될 수 있다.
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return; // 파괴 예정 객체에서 이후 초기화가 돌지 않도록 차단
        }
    }

    private void Start()
    {
        if (instance != this)
            return;

        if (IsServer)
        {
            SetPositionBall(0, whiteBallPoint);
            SetPositionBall(8, blackBallPoint);

            List<Transform> positions = new List<Transform>(startPoints);
            for (int i = 0; i < startPoints.Length; i++)
            {
                int posIdx = Random.Range(0, positions.Count);
                int idx = i + 1;
                if (idx == 8) idx = 15;

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
            isTurnEnd = false;
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
        // 슛 직후 힘 적용 전 1프레임은 아직 정지 상태이므로 한 프레임 양보한 뒤
        // "흰 공이 충분히 빠른 동안" 대기하고, 느려지면(멈추면) 턴 종료를 표시한다.
        yield return new WaitForFixedUpdate();

        while (startBall.rb.linearVelocity.magnitude >= settleVelocityThreshold)
            yield return null;

        cueStick.StickOnOffRpc(false);
        isTurnEnd = true;
    }

    private void NextTurn()
    {
        // 8번(검은) 공이 들어갔으면 게임 종료
        if ((goalBalls & ((ulong)1 << 8)) > 0)
        {
            Debug.Log("GameEnd");
            GameEndEveryoneRpc();
            return;
        }

        bool turnAdvanced = false;

        if (isFirstGoal.Value)
        {
            if (GoalBallsThisTurn == 0)
            {
                NextTurnServerRpc();
                turnAdvanced = true;
            }
            else
            {
                SettingBallServerRpc();
            }
        }
        else
        {
            bool isColorGoal = (GoalBallsThisTurn & 0b11111110) > 0;
            bool isLineGoal = (GoalBallsThisTurn & 0b1111111100000000) > 0;

            // 자신의 목표 종류를 못 넣었으면 턴을 넘긴다.
            bool failedColor = isTargetColor.Value && !isColorGoal;
            bool failedLine = !isTargetColor.Value && !isLineGoal;
            if (failedColor || failedLine)
            {
                NextTurnServerRpc();
                turnAdvanced = true;
            }
        }

        // 흰 공 처리: 흰 공이 빠졌거나(파울) 이번 턴에 움직인 공이 없으면
        MoveBallsThisTurn &= ~(ulong)1;
        if (startBall.isGoal || MoveBallsThisTurn == 0)
        {
            startBall.isGoal = false;
            startBall.gameObject.SetActive(true);
            startBall.transform.position = whiteBallPoint.position;

            // 위에서 이미 턴을 넘겼다면 중복 전환하지 않는다.
            if (!turnAdvanced)
            {
                NextTurnServerRpc();
                turnAdvanced = true;
            }
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
        isTargetColor.Value = !isTargetColor.Value;
        Debug.Log(isTargetColor.Value);
        var connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
        turn.Value = (turn.Value + 1) % connectedIds.Count;
        cueStick.NetworkObject.ChangeOwnership(connectedIds[turn.Value]);
        BroadcastCurrentPlayer();
    }

    // Collect each client's player name on game start
    [Rpc(SendTo.Server)]
    private void RegisterPlayerRpc(PlayerData data, RpcParams rpcParams = default)
    {
        _playerNames[rpcParams.Receive.SenderClientId] = data.playerName;

        // Broadcast once all connected players have registered
        if (_playerNames.Count >= NetworkManager.Singleton.ConnectedClientsIds.Count)
            BroadcastCurrentPlayer();
    }

    private void BroadcastCurrentPlayer()
    {
        var connectedIds = NetworkManager.Singleton.ConnectedClientsIds;
        if (turn.Value >= connectedIds.Count) return;

        ulong currentId = connectedIds[turn.Value];
        FixedString32Bytes name = _playerNames.TryGetValue(currentId, out var n)
            ? n
            : (FixedString32Bytes)$"Player {turn.Value + 1}";

        SetCurrentPlayerRpc(name);
    }

    [Rpc(SendTo.Everyone)]
    private void SetCurrentPlayerRpc(FixedString32Bytes playerName)
    {
        if (currentPlayerText == null) return;
        currentPlayerText.text = playerName.Value;
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
