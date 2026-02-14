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
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button leaveButton;
    public int maxConnections;
    public string targetSceneName = "InGameScene";

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
            leaveButton.onClick.AddListener(LeaveServer);

            await EnsureServicesReadyAsync();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
    }

    private async void CreateHost()
    {
        if (!_servicesReady) await EnsureServicesReadyAsync();
        if (!_servicesReady) return;

        try
        {            
            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            _transport.SetRelayServerData(allocation.ToRelayServerData(_connectionType));

            if (!_networkManager.StartHost())
                throw new Exception("StartHost Failed");

            if (joinCodeText) joinCodeText.text = joinCode;
            Debug.Log("Join Code: " + joinCode);
            
            ChangeToGameScene();
        }
        catch (Exception e)
        {
            Debug.LogError($"Host Start Failed: {e.Message}");
        }
    }

    private void ChangeToGameScene()
    {
        NetworkManager.Singleton.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
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
        }
        catch (Exception e)
        {
            Debug.LogError($"Client Connect Failed: {e.Message}");
        }
    }

    private void LeaveServer()
    {
        if (_networkManager.IsListening)
        {
            _networkManager.Shutdown();
        }
        if (joinCodeText) joinCodeText.text = "-";
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
