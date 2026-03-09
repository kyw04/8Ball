using TMPro;
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
   
    private string playerNames;
    
    private async void Start()
    {
        relayNetworkManager = GameObject.Find("RelayNetworkManager").GetComponent<RelayNetworkManager>();
        if (relayNetworkManager == null)
            Debug.LogError("Can't find RelayNetworkManager");
        
        if (IsHost)
        {
            joinCodeText.text = await RelayService.Instance.GetJoinCodeAsync(relayNetworkManager.allocation.AllocationId);
            leaveButton.onClick.AddListener(relayNetworkManager.LeaveServer);
        }

        startButton.onClick.AddListener(StartButtonClick);
        AddPlayerListRpc(relayNetworkManager.nickname);
    }
    
    private void StartButtonClick()
    {
        if (IsHost)
            NetworkManager.Singleton.SceneManager.LoadScene("InGameScene", LoadSceneMode.Single);
    }
    
    [Rpc(SendTo.Server)]
    private void AddPlayerListRpc(string nickname)
    {
        int index = NetworkManager.Singleton.ConnectedClientsIds.Count - 1;
        if (playerNameTexts.Length <= index)
            return;
    
        playerNameTexts[index].text = nickname;
    }
}
