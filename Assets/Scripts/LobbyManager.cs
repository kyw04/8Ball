using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using UnityEngine.UI;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    [SerializeField] RelayNetworkManager relayNetworkManager;

    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private TextMeshProUGUI[] playerNameTexts;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button leaveButton;

    private NetworkVariable<FixedString32Bytes> joinCode;
    private NetworkList<PlayerData> playerDataList;

    // Server-only: maps NGO ulong clientId → PlayerData.clientId (UGS string)
    private readonly Dictionary<ulong, FixedString32Bytes> _ngoToUgsId = new();

    private void Awake()
    {
        joinCode = new NetworkVariable<FixedString32Bytes>();
        playerDataList = new NetworkList<PlayerData>();
    }

    private async void OnServerStarted()
    {
        if (relayNetworkManager == null)
            Debug.LogError("Can't find RelayNetworkManager");

        if (IsHost)
        {
            joinCode.Value = await RelayService.Instance.GetJoinCodeAsync(relayNetworkManager.allocation.AllocationId);
            UpdateUI(new NetworkListEvent<PlayerData>());
        }
    }

    public override void OnNetworkSpawn()
    {
        playerDataList.OnListChanged += UpdateUI;
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;

        if (IsServer)
            NetworkManager.Singleton.OnClientDisconnectedCallback += OnClientDisconnectedServer;

        NetworkManager.Singleton.OnClientConnectedCallback += (ulong id) =>
        {
            AddPlayerListRpc(relayNetworkManager.playerData);

            if (!IsHost)
                startButton.gameObject.SetActive(false);

            // RemoveAllListeners prevents duplicate registration when multiple clients connect
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartButtonClick);

            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(relayNetworkManager.LeaveServer);
        };
    }

    public override void OnNetworkDespawn()
    {
        playerDataList.OnListChanged -= UpdateUI;

        if (IsServer)
            NetworkManager.Singleton.OnClientDisconnectedCallback -= OnClientDisconnectedServer;
    }

    // Fired on server when any client disconnects (voluntary or involuntary)
    private void OnClientDisconnectedServer(ulong ngoClientId)
    {
        if (!_ngoToUgsId.TryGetValue(ngoClientId, out FixedString32Bytes ugsClientId))
            return;

        _ngoToUgsId.Remove(ngoClientId);
        RemovePlayerFromList(ugsClientId);
    }

    private void RemovePlayerFromList(FixedString32Bytes ugsClientId)
    {
        for (int i = 0; i < playerDataList.Count; i++)
        {
            if (playerDataList[i].clientId == ugsClientId)
            {
                playerDataList.RemoveAt(i);
                break;
            }
        }
    }

    private void StartButtonClick()
    {
        if (IsHost)
            NetworkManager.Singleton.SceneManager.LoadScene("InGameScene", LoadSceneMode.Single);
    }

    [Rpc(SendTo.Server)]
    private void AddPlayerListRpc(PlayerData playerData, RpcParams rpcParams = default)
    {
        if (playerDataList.Contains(playerData))
            return;

        _ngoToUgsId[rpcParams.Receive.SenderClientId] = playerData.clientId;
        playerDataList.Add(playerData);
    }

    private void UpdateUI(NetworkListEvent<PlayerData> change)
    {
        UpdateUIRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void UpdateUIRpc()
    {
        for (int i = 0; i < playerNameTexts.Length; i++)
        {
            playerNameTexts[i].text = i < playerDataList.Count
                ? playerDataList[i].playerName.Value
                : string.Empty;
        }

        joinCodeText.text = joinCode.Value.Value;
    }
}
