using Unity.Netcode;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [SerializeField, Range(0, 15)]
    public int index;

    public Rigidbody rb { get; private set; }
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (rb.linearVelocity.magnitude <= 0.1f)
        {
            rb.linearVelocity = Vector3.zero;
            GameManager.instance.RemoveMoveBall(index);
        }
    }

    public void Hit(Vector3 dir, float power)
    {
        rb.AddForce(dir * power, ForceMode.Impulse);
    }
    
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Floor"))
            return;
        GameManager.instance.AddMoveBall(index);
        
        var hit = other.contacts[0];
        
        var newVelocity = Vector3.Reflect(-other.relativeVelocity, hit.normal);
        if (other.gameObject.CompareTag("Wall"))
        {
            rb.linearVelocity = newVelocity;
        }
        else // ball
        {
            rb.linearVelocity += newVelocity * 0.6f;
        }
    }
}
