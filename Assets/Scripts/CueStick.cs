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
        _mouse = Mouse.current;
    }
    
    private void Update()
    {
        if (!IsOwner)
            return;

        if (_mouse.leftButton.isPressed)
        {
            transform.position = target.transform.position;
            StickRotation();
        }

        StickPowerUpdater();
        
        if (_mouse.rightButton.wasPressedThisFrame)
        {
            target.Hit(stick.forward, hittingPower);
            GameManager.instance.EndTurn();
        }
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

    private void StickPowerUpdater()
    {
        if (_mouse.scroll.magnitude <= 0)
            return;
        
        float value = _mouse.scroll.value.y;
        hittingPower = Mathf.Clamp(hittingPower + value, 0, maxPower);
        
        if (powerUI) powerUI.fillAmount = hittingPower / maxPower;
    }
}
