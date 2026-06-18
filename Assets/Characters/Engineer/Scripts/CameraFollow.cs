using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    public float distance = 6f;
    public float height = 3f;

    public float mouseSensitivity = 0.1f;

    public float minPitch = -30f;
    public float maxPitch = 60f;

    [HideInInspector] public bool inputLocked = false;

    private float yaw;
    private float pitch = 20f;

    void Start()
    {
        if (target != null)
            yaw = target.eulerAngles.y;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        bool rightHeld = Mouse.current.rightButton.isPressed;

        if (inputLocked)
        {
            if (rightHeld && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (!rightHeld && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        bool freeLookHeld = inputLocked && rightHeld;

        if (!inputLocked || freeLookHeld)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            yaw += mouseDelta.x * mouseSensitivity;
            pitch -= mouseDelta.y * mouseSensitivity;

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);

        Vector3 offset = rotation * new Vector3(0, 0, -distance);

        transform.position = target.position + Vector3.up * height + offset;

        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}