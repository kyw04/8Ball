using UnityEngine;

public class Ball : MonoBehaviour
{
    private Rigidbody rb;
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void Hitting(Vector3 dir, float power)
    {
        rb.AddForce(dir * power, ForceMode.Impulse);
    }
    
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Floor"))
            return;

        var hit = other.contacts[0];
        
        var newVelocity = Vector3.Reflect(-other.relativeVelocity, hit.normal);
        if (other.gameObject.CompareTag("Wall"))
        {
            rb.linearVelocity = newVelocity;
        }
        else
        {
            rb.linearVelocity += newVelocity * 0.6f;
        }
    }
}
