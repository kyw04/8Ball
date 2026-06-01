using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    [SerializeField, Range(0, 15)]
    public int index;
    public bool isGoal;

    public float radius = 0.028575f;

    [Header("Friction")]
    public float rollingFrictionMu = 0.1f;    // 롤링 저항 (높을수록 빨리 멈춤)
    public float slidingFrictionMu = 0.2f;    // 슬라이딩 마찰 (충돌 후 미끄러짐→굴림 전환)

    [Header("Settle")]
    public float settleLinearSpeed = 0.05f;
    public float settleLockTimeAfterShot = 0.12f;

    [Header("Collision")]
    public float wallRestitution = 0.75f;
    public float ballRestitution = 0.95f;
    public float ballCollisionLockTime = 0.08f;

    private float _settleLockUntil;

    public Vector3 PrevVelocity { get; private set; }
    public Rigidbody rb { get; private set; }
    public NetworkRigidbody netRb { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        netRb = GetComponent<NetworkRigidbody>();
    }

    private void Start()
    {
        rb.maxAngularVelocity = 200f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void FixedUpdate()
    {
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworked && !IsServer)
            return;
        if (rb.isKinematic)
            return;

        PrevVelocity = rb.linearVelocity;

        Vector3 v = rb.linearVelocity;
        Vector3 vPlane = new Vector3(v.x, 0f, v.z);
        float speed = vPlane.magnitude;

        // 속도가 settle 임계 아래면 각속도 즉시 0 후 처리
        if (speed < settleLinearSpeed)
        {
            rb.angularVelocity = Vector3.zero;

            if (Time.time >= _settleLockUntil)
            {
                rb.linearVelocity = Vector3.zero;
                rb.Sleep();
                GameManager.instance?.RemoveMovingBall(index);
            }
            return;
        }

        float g = Physics.gravity.magnitude;

        // 접촉점 속도 계산: v_contact = vPlane + ω × r_contact
        // r_contact = (0,-R,0) → ω × r = (ωz*R, 0, -ωx*R)
        Vector3 ω = rb.angularVelocity;
        Vector3 contactVel = new Vector3(
            vPlane.x + ω.z * radius,
            0f,
            vPlane.z - ω.x * radius
        );
        float slipSpeed = contactVel.magnitude;

        if (slipSpeed > 0.005f)
        {
            // 슬라이딩: 접촉 마찰이 선속도 감속 + 토크로 회전 가속
            Vector3 slipDir = contactVel / slipSpeed;
            float fMag = slidingFrictionMu * rb.mass * g;
            Vector3 F = -slipDir * fMag;
            rb.AddForce(F, ForceMode.Force);
            // τ = (0,-R,0) × F = (-R*Fz, 0, R*Fx)
            rb.AddTorque(new Vector3(-radius * F.z, 0f, radius * F.x), ForceMode.Force);
        }
        else
        {
            // 순수 롤링: 롤링 저항으로 감속 + 각속도 동기화
            rb.AddForce(-vPlane.normalized * (rollingFrictionMu * rb.mass * g), ForceMode.Force);
            rb.angularVelocity = new Vector3(vPlane.z / radius, 0f, -vPlane.x / radius);
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
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworked && !IsServer)
            return;
        if (rb.isKinematic)
            return;
        if (other.gameObject.CompareTag("Floor"))
            return;

        GameManager.instance?.AddMoveBall(index);

        var hit = other.contacts[0];

        if (other.gameObject.CompareTag("Wall"))
        {
            Vector3 reflected = Vector3.Reflect(PrevVelocity, hit.normal);
            rb.linearVelocity = reflected * wallRestitution;
            // 벽 법선 방향 각속도 성분 제거
            rb.angularVelocity = Vector3.ProjectOnPlane(rb.angularVelocity, hit.normal);
        }
        else if (other.gameObject.CompareTag("Goal"))
        {
            GameManager.instance?.RemoveMovingBall(index);
            GameManager.instance?.AddGoalBall(index);
            rb.Sleep();
            isGoal = true;
            gameObject.SetActive(false);
        }
        else if (other.gameObject.CompareTag("Ball"))
        {
            Ball otherBall = other.gameObject.GetComponent<Ball>();
            if (otherBall == null || index > otherBall.index) return;

            Vector3 n = (other.transform.position - transform.position).normalized;
            Vector3 v1 = PrevVelocity;
            Vector3 v2 = otherBall.PrevVelocity;

            float relVelNormal = Vector3.Dot(v1 - v2, n);
            if (relVelNormal <= 0f) return;

            // 등질량 탄성 충돌: j = (1+e)/2 * dot(v1-v2, n)
            float j = (1f + ballRestitution) * 0.5f * relVelNormal;
            rb.linearVelocity = v1 - j * n;
            otherBall.rb.linearVelocity = v2 + j * n;
            // 각속도는 다음 FixedUpdate의 슬라이딩 물리가 자연스럽게 수렴시킴

            float lockUntil = Time.time + ballCollisionLockTime;
            _settleLockUntil = Mathf.Max(_settleLockUntil, lockUntil);
            otherBall._settleLockUntil = Mathf.Max(otherBall._settleLockUntil, lockUntil);
        }
    }
}
