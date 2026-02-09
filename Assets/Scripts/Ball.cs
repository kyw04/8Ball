using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

public class Ball : MonoBehaviour
{
    public float speed;
    
    private Rigidbody rb;
    public Vector3 direction;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        Debug.Log($"{rb.linearVelocity}\n{rb.angularVelocity}\n{rb.linearDamping}\n{rb.angularDamping}");
    }

    private void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            rb.AddForce(direction * speed, ForceMode.Impulse);
            // Debug.Log(rb.linearVelocity);
        }
        
    }

    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Floor"))
            return;

        var hit = other.contacts[0];
        rb.linearVelocity = Vector3.Reflect(-other.relativeVelocity, hit.normal);
    }
}
