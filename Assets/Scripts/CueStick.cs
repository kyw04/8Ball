using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class CueStick : MonoBehaviour
{
    public Transform taget;
    public Transform stick;
    
    public float rotationSpeed;
    
    private void Update()
    {
        if (Mouse.current.leftButton.isPressed)
        {
            Vector2 mouseDelta = -Mouse.current.delta.value;
            float rot = Mathf.Abs(mouseDelta.x) > Mathf.Abs(mouseDelta.y) ? mouseDelta.x : mouseDelta.y;
            stick.Rotate(new Vector3(0, rot * rotationSpeed, 0));
        }
    }
}
