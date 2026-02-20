using Unity.Netcode;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [SerializeField, Range(0, 15)]
    public int index;

    public float radius;
    
    [Header("Friction Tuning")]
    public float slideFrictionMu = 0.20f;   // 미끄럼 마찰 계수(튜닝)
    public float rollFrictionMu = 0.01f;    // 구름 저항 계수(튜닝)
    public float slipThreshold = 0.03f;     // 접점 미끄럼 판정 임계값
    public float rollAlignStrength = 8f;    // 구름 상태 스핀 정렬 강도
    
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
        Vector3 v = rb.linearVelocity;
        Vector3 w = rb.angularVelocity;

        if (v.sqrMagnitude < 0.000001f && w.sqrMagnitude < 0.000001f)
            return;

        Vector3 n = Vector3.up;
        Vector3 r = -n * radius; // 중심 -> 접점 벡터

        // 접점 속도 = 중심 속도 + (각속도 x 반지름 벡터)
        Vector3 contactVelocity = v + Vector3.Cross(w, r);

        // 테이블 평면 성분만 사용
        Vector3 slipVelocity = Vector3.ProjectOnPlane(contactVelocity, n);
        float g = Physics.gravity.magnitude;

       
        if (slipVelocity.magnitude > slipThreshold)  // 1) 미끄럼 중이면: 미끄럼 마찰
        {
            Vector3 frictionForce = -slipVelocity.normalized * (slideFrictionMu * rb.mass * g);

            // 병진 감속
            rb.AddForce(frictionForce, ForceMode.Force);

            // 회전 변화 (토크 = r x F)
            Vector3 frictionTorque = Vector3.Cross(r, frictionForce);
            rb.AddTorque(frictionTorque, ForceMode.Force);
        }
        else // 2) 거의 순수구름이면: 작은 구름 저항 + 스핀 정렬
        {
            Vector3 vPlane = Vector3.ProjectOnPlane(v, n);

            if (vPlane.sqrMagnitude > 0.0000001f)
            {
                // 구름 저항 (작게)
                Vector3 rollingResistance = -vPlane.normalized * (rollFrictionMu * rb.mass * g);
                rb.AddForce(rollingResistance, ForceMode.Force);

                // 순수구름에 맞는 이상적인 각속도(테이블 평면 기준)
                Vector3 desiredOmega = Vector3.Cross(n, vPlane) / radius;

                // 법선 방향 회전(사이드 성분 일부)은 유지하고, 평면 회전만 정렬
                Vector3 omegaPlane = Vector3.ProjectOnPlane(w, n);
                Vector3 omegaCorrection = (desiredOmega - omegaPlane) * rollAlignStrength;

                rb.AddTorque(omegaCorrection, ForceMode.Acceleration);
            }
        }

        // 아주 느려지면 깔끔하게 정지 처리 (잔떨림 방지)
        if (rb.linearVelocity.magnitude < 0.03f && rb.angularVelocity.magnitude < 0.5f)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
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
