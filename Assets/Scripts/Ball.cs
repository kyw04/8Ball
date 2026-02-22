using Unity.Netcode;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [SerializeField, Range(0, 15)]
    public int index;

    public float radius;
    
    public float slideFrictionMu = 0.20f;   // 미끄럼 마찰 계수(튜닝)
    public float rollFrictionMu = 0.01f;    // 구름 저항 계수(튜닝)
    public float slipThreshold = 0.03f;     // 접점 미끄럼 판정 임계값
    public float rollAlignStrength = 8f;    // 구름 상태 스핀 정렬 강도
    
    public float settleLinearSpeed = 0.01f;
    public float settleRollSpinSpeed = 0.15f;
    public float settleSideSpinSpeed = 0.10f;
    
    public float settleLockTimeAfterShot = 0.12f;
    private float _settleLockUntil = 0f;
    
    public Rigidbody rb { get; private set; }
    
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        rb.maxAngularVelocity = 100f;

        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    private void FixedUpdate()
    {
        Vector3 n = Vector3.up;
        Vector3 v = rb.linearVelocity;
        Vector3 w = rb.angularVelocity;
        
        bool canSettle = Time.time >= _settleLockUntil;
        Vector3 vPlane = Vector3.ProjectOnPlane(v, n);
        Vector3 wPlane = Vector3.ProjectOnPlane(w, n);
        float wNormal = Vector3.Dot(w, n);
        
        if (canSettle &&
            vPlane.magnitude < settleLinearSpeed &&
            wPlane.magnitude < settleRollSpinSpeed &&
            Mathf.Abs(wNormal) < settleSideSpinSpeed)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();

            GameManager.instance.RemoveMoveBall(index);
            return;
        }

        Vector3 r = -n * radius;
        Vector3 contactVelocity = v + Vector3.Cross(w, r);
        Vector3 slipVelocity = Vector3.ProjectOnPlane(contactVelocity, n);
        float g = Physics.gravity.magnitude;
       
        if (slipVelocity.sqrMagnitude <= slipThreshold * slipThreshold)
            return;
        
        if (slipVelocity.magnitude > slipThreshold)
        {
            Vector3 frictionForce = -slipVelocity.normalized * (slideFrictionMu * rb.mass * g);
            rb.AddForce(frictionForce, ForceMode.Force);
            
            Vector3 frictionTorque = Vector3.Cross(r, frictionForce);
            rb.AddTorque(frictionTorque, ForceMode.Force);
        }
        else
        {
            if (vPlane.sqrMagnitude > 0.0000001f)
            {
                Vector3 rollingResistance = -vPlane.normalized * (rollFrictionMu * rb.mass * g);
                rb.AddForce(rollingResistance, ForceMode.Force);
                
                Vector3 desiredOmega = Vector3.Cross(n, vPlane) / radius;
                Vector3 omegaPlane = Vector3.ProjectOnPlane(w, n);
                Vector3 omegaCorrection = (desiredOmega - omegaPlane) * rollAlignStrength;

                rb.AddTorque(omegaCorrection, ForceMode.Acceleration);
            }
        }

        if (rb.linearVelocity.magnitude < 0.3f && rb.angularVelocity.magnitude < 0.5f)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void NotifyShot()
    {
        _settleLockUntil = Time.time + settleLockTimeAfterShot;
        rb.WakeUp();
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

    private void OnCollisionStay(Collision other)
    {
        if (other.gameObject.CompareTag("Floor"))
        {
            rb.linearVelocity -= rb.linearVelocity * 0.01f;
            rb.angularVelocity -= rb.angularVelocity * 0.01f;
        }
    }
}
