using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class Player : NetworkBehaviour
{
    private PlayerInput _input;

    private void Awake()
    {
        _input = GetComponent<PlayerInput>();
    }
}
