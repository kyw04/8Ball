using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Unity.Netcode;

public class CueStick : NetworkBehaviour
{
    public Image powerUI;
    public Ball target;
    public Transform stick;
    public GameObject model;

    public float hittingPower;
    public float maxPower;

    [SerializeField] private float scrollPowerScale = 0.5f;

    private Mouse _mouse;

    private void Start()
    {
        _mouse = Mouse.current;
    }

    private void Update()
    {
        if (target == null)
            return;

        transform.position = target.transform.position;

        if (!IsOwner)
            return;

        if (_mouse.leftButton.isPressed)
        {
            StickRotation();
        }

        StickPowerUpdater();

        if (_mouse.rightButton.wasPressedThisFrame)
        {
            TryShootRequest(target.rb, stick.forward, hittingPower, Vector3.zero);
        }
    }

    [Rpc(SendTo.Everyone)]
    public void StickOnOffRpc(bool value)
    {
        model.SetActive(value);
    }

    private void TryShootRequest(Rigidbody rb, Vector3 cueDir, float impulse, Vector3 hitOffsetLocal)
    {
        Vector3 dir = cueDir.normalized;
        Vector3 hitPointWorld = rb.transform.TransformPoint(hitOffsetLocal);

        RequestShootRpc(new NetworkObjectReference(target.NetworkObject), dir, impulse, hitPointWorld);
    }

    [Rpc(SendTo.Server)]
    private void RequestShootRpc(NetworkObjectReference ballRef, Vector3 cueDir, float impulse, Vector3 localHitOffset)
    {
        if (!ballRef.TryGet(out NetworkObject ballNetObj))
        {
            Debug.LogWarning("[CueStick] ballRef.TryGet failed");
            return;
        }

        Ball ball = ballNetObj.GetComponent<Ball>();
        Rigidbody rb = ballNetObj.GetComponent<Rigidbody>();

        if (ball == null || rb == null)
        {
            Debug.LogWarning("[CueStick] Ball or Rigidbody missing");
            return;
        }

        cueDir.y = 0f;
        if (cueDir.sqrMagnitude < 0.0001f) return;
        cueDir.Normalize();

        impulse = Mathf.Clamp(impulse, 0f, maxPower);
        if (impulse <= 0.001f) return;

        Vector3 hitPointWorld = rb.transform.TransformPoint(localHitOffset);

        rb.WakeUp();
        rb.AddForceAtPosition(cueDir * impulse, hitPointWorld, ForceMode.Impulse);

        ball.NotifyShotRpc();

        GameManager.instance.EndTurn();
    }

    private void StickRotation()
    {
        Ray ray = Camera.main.ScreenPointToRay(_mouse.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Vector3 pivot = transform.position;       // 공 위치(스틱이 회전하는 중심)
            Vector3 worldPoint = hit.point;
            worldPoint.y = stick.position.y;          // 높이를 스틱 평면에 맞춤

            // 마우스 지점에서 공 쪽으로 향하는 방향(= 공에서 마우스 반대쪽을 겨냥)
            Vector3 aimDir = pivot - worldPoint;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 0.0001f)
                return;

            stick.rotation = Quaternion.LookRotation(aimDir.normalized, Vector3.up);
        }
    }

    private void StickPowerUpdater()
    {
        if (_mouse.scroll.magnitude <= 0)
            return;

        float value = _mouse.scroll.value.y * scrollPowerScale;
        hittingPower = Mathf.Clamp(hittingPower + value, 0, maxPower);

        if (powerUI) powerUI.fillAmount = hittingPower / maxPower;
    }
}
