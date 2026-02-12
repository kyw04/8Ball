using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CueStick : MonoBehaviour
{
    public Image powerUI;
    public Ball target;
    public Transform stick;
    
    public float rotationSpeed;
    public float hittingPower;
    public float maxPower;

    private Mouse _mouse;

    private void Start()
    {
        _mouse = Mouse.current;
    }

    private void Update()
    {
        if (_mouse.leftButton.isPressed)
        {
            transform.position = target.transform.position;
            Vector2 mouseDelta = -Mouse.current.delta.value;
            float rot = Mathf.Abs(mouseDelta.x) > Mathf.Abs(mouseDelta.y) ? mouseDelta.x : mouseDelta.y;
            stick.Rotate(new Vector3(0, rot * rotationSpeed, 0));
        }

        if (_mouse.scroll.magnitude > 0)
        {
            float value = _mouse.scroll.magnitude;
            hittingPower = Mathf.Clamp(hittingPower + value, 0, maxPower);
            powerUI.fillAmount = hittingPower / maxPower;
        }
        
        if (_mouse.rightButton.wasPressedThisFrame)
        {
            target.Hitting(stick.forward, hittingPower);
        }
    }
}
