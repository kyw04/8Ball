using TMPro;
using Unity.Collections;
using UnityEngine.UI;
using UnityEngine;
using Unity.Services.Relay;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class LobbyManager : NetworkBehaviour
{
    RelayNetworkManager relayNetworkManager;
    
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField]  private TextMeshProUGUI[] playerNameTexts;
    [SerializeField]  private Button startButton;
    [SerializeField]  private Button leaveButton;
   
    private NetworkVariable<FixedString32Bytes> joinCode;
    private NetworkList<PlayerData> playerDataList;

    private void Awake()
    {
        joinCode = new NetworkVariable<FixedString32Bytes>();
        playerDataList =  new NetworkList<PlayerData>();
    }

    private async void Start()
    {
        relayNetworkManager = GameObject.Find("RelayNetworkManager").GetComponent<RelayNetworkManager>();
        if (relayNetworkManager == null)
            Debug.LogError("Can't find RelayNetworkManager");

        if (IsHost)
        {
            joinCode.Value = await RelayService.Instance.GetJoinCodeAsync(relayNetworkManager.allocation.AllocationId);
        }
        
        startButton.onClick.AddListener(StartButtonClick);
        leaveButton.onClick.AddListener(() => OnClientDisconnected(relayNetworkManager.playerData.clientId));
        leaveButton.onClick.AddListener(relayNetworkManager.LeaveServer);
        AddPlayerListRpc(relayNetworkManager.playerData);
    }

    public override void OnNetworkSpawn()
    {
        playerDataList.OnListChanged += UpdateUI;
    }

    public override void OnNetworkDespawn()
    {
        playerDataList.OnListChanged -= UpdateUI;
    }
    
    private void StartButtonClick()
    {
        if (IsHost)
            NetworkManager.Singleton.SceneManager.LoadScene("InGameScene", LoadSceneMode.Single);
    }
    
    void OnClientDisconnected(ulong clientId)
    {
        for (int i = 0; i < playerDataList.Count; i++)
        {
            if (playerDataList[i].clientId == clientId)
            {
                playerDataList.RemoveAt(i);
                break;
            }
        }
    }
    
    [Rpc(SendTo.Server)]
    private void AddPlayerListRpc(PlayerData playerData)
    {
        playerDataList.Add(playerData);
    }

    private void UpdateUI(NetworkListEvent<PlayerData> change)
    {
        UpdateUIRpc();
    }
    
    [Rpc(SendTo.Everyone)]
    private void UpdateUIRpc()
    {
        for (int i = 0; i < playerDataList.Count; i++)
        {
            if (i < playerNameTexts.Length)
            {
                playerNameTexts[i].text = playerDataList[i].playerName.Value;
            }
        }

        joinCodeText.text = joinCode.Value.Value;
    }
}
