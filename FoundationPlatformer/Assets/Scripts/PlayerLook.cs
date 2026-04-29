using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputAction lookAction;

    [Header("References")]
    [Tooltip("Assign the Camera (child of the player) here.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Sensitivity")]
    [SerializeField] private float mouseSensitivity = 0.15f;
    [SerializeField] private float gamepadSensitivity = 120f;

    [Header("Vertical Clamp")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private float _pitch;

    private void Awake()
    {
        if (lookAction.bindings.Count == 0)
            lookAction = new InputAction("Look", binding: "<Mouse>/delta");

        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>()?.transform;
    }

    private void OnEnable()
    {
        lookAction.Enable();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        lookAction.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        Vector2 look = lookAction.ReadValue<Vector2>();

        bool usingMouse = Mouse.current != null && Mouse.current.delta.IsActuated();
        float scale = usingMouse ? mouseSensitivity : gamepadSensitivity * Time.deltaTime;

        float yaw = look.x * scale;
        float pitchDelta = look.y * scale;

        transform.Rotate(Vector3.up, yaw, Space.World);

        _pitch = Mathf.Clamp(_pitch - pitchDelta, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}
