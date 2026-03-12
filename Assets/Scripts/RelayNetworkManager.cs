using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
public class RelayNetworkManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField nicknameInput;

    [SerializeField] private GameObject outdoorCanvas;
    [SerializeField] private GameObject lobbyCanvas;
    [SerializeField] private GameObject defaultGroup;
    [SerializeField] private GameObject joinGroup;

    public int maxConnections;
    public string targetSceneName = "InGameScene";
    public PlayerData playerData;

    public Allocation allocation;
    
    private NetworkManager _networkManager;
    private UnityTransport _transport;
    private bool _servicesReady;
    private readonly string _connectionType = "dtls";

    private async void Start()
    {
        try
        {
            await Task.Yield();
            
            _networkManager = GetComponent<NetworkManager>();
            _transport = GetComponent<UnityTransport>();
            
            hostButton.onClick.AddListener(CreateHost);
            joinButton.onClick.AddListener(JoinByCode);
            await EnsureServicesReadyAsync();
            
            _networkManager.OnClientConnectedCallback += PlayerDataSetting;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    public void ClickMultiplayerButton()
    {
        defaultGroup.SetActive(false);
        joinGroup.SetActive(true);
    }

    public void CanvasGroupChanged(bool value)
    {
        defaultGroup.SetActive(value);
        joinGroup.SetActive(!value);
    }

    private void PlayerDataSetting(ulong clientId)
    {
        playerData.clientId = clientId;
        
        if (string.IsNullOrWhiteSpace(nicknameInput.text))
            playerData.playerName = "unknown_player_" + UnityEngine.Random.Range(0, 999);
        else
            playerData.playerName = nicknameInput.text;
        
        Debug.Log($"Player ID: {playerData.clientId},  Nickname: {playerData.playerName}");
    }
    
    private async void CreateHost()
    {
        if (!_servicesReady) await EnsureServicesReadyAsync();
        if (!_servicesReady) return;

        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            _transport.SetRelayServerData(allocation.ToRelayServerData(_connectionType));

            if (!_networkManager.StartHost())
                throw new Exception("StartHost Failed");

            Debug.Log("Join Code: " + joinCode);
            
            SetCanvasActive(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Host Start Failed: {e.Message}");
        }
    }

    private void SetCanvasActive(bool onLobbyCanvas)
    {
        outdoorCanvas.SetActive(!onLobbyCanvas);
        lobbyCanvas.SetActive(onLobbyCanvas);
    }
    
    private async void JoinByCode()
    {
        if (!_servicesReady) await EnsureServicesReadyAsync();
        if (!_servicesReady) return;

        var code = (joinCodeInput ? joinCodeInput.text : "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogError("join code is empty");
            return;
        }

        try
        {
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            _transport.SetRelayServerData(joinAllocation.ToRelayServerData(_connectionType));

            if (!_networkManager.StartClient())
                throw new Exception("StartClient Failed");

            Debug.Log("Client trying to connect...");
            SetCanvasActive(true);
        }
        catch (Exception e)
        {
            Debug.LogError($"Client Connect Failed: {e.Message}");
        }
    }

    public void LeaveServer()
    {
        Debug.Log("Leave Server");
        if (_networkManager.IsListening)
        {
            _networkManager.Shutdown();
        }

        if (!_networkManager.IsHost)
        {
            LeaveAllClientRpc();
        }
        
        SetCanvasActive(false);
    }

    [Rpc(SendTo.Everyone)]
    private void LeaveAllClientRpc()
    {
        SetCanvasActive(false);
        Debug.Log("Leave All Client");
    }
    
    private async Task EnsureServicesReadyAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            _servicesReady = true;
            Debug.Log($"UGS Ready / {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            _servicesReady = false;
            Debug.Log($"UGS Reset Failed: {e.Message}");
        }
    }
}
