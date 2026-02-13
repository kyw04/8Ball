using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Netcode;

public class CueStick : NetworkBehaviour
{
    public Image powerUI;
    public Ball target;
    public Transform stick;
    
    public float hittingPower;
    public float maxPower;

    private Mouse _mouse;

    private void Start()
    {
        transform.position = target.transform.position;
        _mouse = Mouse.current;
    }
    
    private void Update()
    {
        if (!IsOwner)
        {
            if (_mouse.leftButton.isPressed)
            {
                Debug.Log("ChangeOwnership");
                RequestOwnershipServerRpc();
            }
        }

        if (_mouse.leftButton.isPressed)
        {
            Debug.Log("Mouse Left");
            
            transform.position = target.transform.position;
            StickRotation();
        }

        if (_mouse.scroll.magnitude > 0)
        {
            float value = _mouse.scroll.magnitude;
            hittingPower = Mathf.Clamp(hittingPower + value, 0, maxPower);
            
            if (powerUI)
                powerUI.fillAmount = hittingPower / maxPower;
        }
        
        if (_mouse.rightButton.wasPressedThisFrame)
        {
            target.Hitting(stick.forward, hittingPower);
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void RequestOwnershipServerRpc(ServerRpcParams serverRpcParams = default)
    {
        NetworkObject.ChangeOwnership(serverRpcParams.Receive.SenderClientId);
    }
    
    private void StickRotation()
    {
        Ray ray = Camera.main.ScreenPointToRay(_mouse.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 pos = transform.position;
            Vector3 worldPoint = hit.point;
            worldPoint.y = stick.position.y;
            Vector3 lookDir = pos - (worldPoint - pos).normalized;
            
            stick.LookAt(lookDir);
        }
    }
        
}
