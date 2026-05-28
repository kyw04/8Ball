using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [SerializeField, Range(0, 15)]
    public int index;
    public bool isGoal;

    public float radius;

    [Header("Friction Tuning")]
    public float slideFrictionMu = 0.20f; // 미끄럼 마찰 계수(튜닝)
    public float rollFrictionMu = 0.01f; // 구름 저항 계수(튜닝)
    public float slipThreshold = 0.03f; // 접점 미끄럼 판정 임계값
    public float rollAlignStrength = 8f; // 구름 상태 스핀 정렬 강도

    [Header("Settle Thresholds")]
    public float settleLinearSpeed = 0.01f;
    public float settleRollSpinSpeed = 0.15f;
    public float settleSideSpinSpeed = 0.10f;
    public float settleLockTimeAfterShot = 0.12f;

    [Header("Hard Stop Thresholds")]
    public float hardStopLinearSpeed = 0.3f;
    public float hardStopAngularSpeed = 0.5f;

    [Header("Collision Response")]
    public float wallRestitution = 0.95f;
    public float floorDampingPerStep = 0.01f;

    private float _settleLockUntil = 0f;

    public Rigidbody rb { get; private set; }
    public NetworkRigidbody netRb { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        netRb = GetComponent<NetworkRigidbody>();
    }

    private void Start()
    {
        rb.maxAngularVelocity = 100f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    private void FixedUpdate()
    {
        if (!IsServer)
            return;

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

            GameManager.instance.RemoveMovingBall(index);
            return;
        }

        Vector3 r = -n * radius;
        Vector3 contactVelocity = v + Vector3.Cross(w, r);
        Vector3 slipVelocity = Vector3.ProjectOnPlane(contactVelocity, n);
        float g = Physics.gravity.magnitude;

        if (slipVelocity.sqrMagnitude <= slipThreshold * slipThreshold)
        {
            // 접점이 거의 구름 상태: 구름 저항 + 스핀 정렬
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
        else
        {
            // 미끄럼 상태: 슬라이딩 마찰력 + 그에 따른 토크
            Vector3 frictionForce = -slipVelocity.normalized * (slideFrictionMu * rb.mass * g);
            rb.AddForce(frictionForce, ForceMode.Force);

            Vector3 frictionTorque = Vector3.Cross(r, frictionForce);
            rb.AddTorque(frictionTorque, ForceMode.Force);
        }

        if (rb.linearVelocity.magnitude < hardStopLinearSpeed &&
            rb.angularVelocity.magnitude < hardStopAngularSpeed)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    [Rpc(SendTo.Server)]
    public void NotifyShotRpc()
    {
        _settleLockUntil = Time.time + settleLockTimeAfterShot;
        rb.WakeUp();
    }

    private void OnCollisionEnter(Collision other)
    {
        if (!IsServer)
            return;

        if (other.gameObject.CompareTag("Floor"))
            return;

        GameManager.instance.AddMoveBall(index);

        var hit = other.contacts[0];

        if (other.gameObject.CompareTag("Wall"))
        {
            // 벽은 직접 반사 처리. 물리 머티리얼 반발은 0으로 두는 것을 전제로 함.
            Vector3 reflected = Vector3.Reflect(-other.relativeVelocity, hit.normal);
            rb.linearVelocity = reflected * wallRestitution;
        }
        else if (other.gameObject.CompareTag("Goal"))
        {
            GameManager.instance.RemoveMovingBall(index);
            GameManager.instance.AddGoalBall(index);
            rb.Sleep();
            isGoal = true;
            gameObject.SetActive(false);
        }
        // 주의: 공-공(Ball) 충돌은 Unity 기본 Rigidbody 솔버에 맡긴다.
        // 직접 속도를 더하면 솔버 임펄스와 이중 적용되어 튜닝이 불가능해진다.
        // 반발 계수가 필요하면 PhysicMaterial.bounciness 로 조정할 것.
    }

    private void OnCollisionStay(Collision other)
    {
        if (!IsServer)
            return;

        if (other.gameObject.CompareTag("Floor"))
        {
            rb.linearVelocity -= rb.linearVelocity * floorDampingPerStep;
            rb.angularVelocity -= rb.angularVelocity * floorDampingPerStep;
        }
    }
}
