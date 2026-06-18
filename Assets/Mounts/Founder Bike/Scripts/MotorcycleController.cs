using UnityEngine;
using UnityEngine.InputSystem;

public class MotorcycleController : MonoBehaviour
{
    public float moveSpeed = 8f;

    public float groundCheckDistance = 3f;
    public float groundOffset = 0.15f;
    public float groundSmoothSpeed = 5f;
    public float normalSmoothSpeed = 5f;

    [HideInInspector]
    public GameObject player;

    [HideInInspector]
    public CameraFollow cameraFollow;

    public AudioClip FounderBikeIdle;
    public AudioClip FounderBikeDrive;

    private AudioSource audioSource;
    private Vector3 groundNormal = Vector3.up;

    void OnEnable()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource != null && FounderBikeIdle != null)
        {
            audioSource.loop = true;
            audioSource.clip = FounderBikeIdle;
            audioSource.Play();
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            Dismount();
            return;
        }

        StickToGround();

        Vector2 input = Vector2.zero;

        if (Keyboard.current.wKey.isPressed) input.y += 1;
        if (Keyboard.current.sKey.isPressed) input.y -= 1;
        if (Keyboard.current.aKey.isPressed) input.x -= 1;
        if (Keyboard.current.dKey.isPressed) input.x += 1;

        bool isMoving = input.sqrMagnitude > 0;

        if (isMoving && cameraFollow != null)
        {
            Camera cam = cameraFollow.GetComponent<Camera>();

            Vector3 camForward =
                Vector3.ProjectOnPlane(cam.transform.forward, groundNormal).normalized;

            Vector3 camRight =
                Vector3.ProjectOnPlane(cam.transform.right, groundNormal).normalized;

            Vector3 moveDirection =
                camForward * input.y +
                camRight * input.x;

            moveDirection.Normalize();

            transform.position += moveDirection * moveSpeed * Time.deltaTime;
            transform.rotation = Quaternion.LookRotation(moveDirection, groundNormal);
        }

        HandleAudio(isMoving);
    }

    void StickToGround()
    {
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 1f;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance))
        {
            groundNormal = Vector3.Lerp(
                groundNormal,
                hit.normal,
                normalSmoothSpeed * Time.deltaTime
            ).normalized;

            float targetY = hit.point.y + groundOffset;

            Vector3 targetPosition = new Vector3(
                transform.position.x,
                targetY,
                transform.position.z
            );

            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                groundSmoothSpeed * Time.deltaTime
            );
        }
        else
        {
            groundNormal = Vector3.Lerp(
                groundNormal,
                Vector3.up,
                normalSmoothSpeed * Time.deltaTime
            ).normalized;
        }
    }

    void HandleAudio(bool driving)
    {
        if (audioSource == null) return;

        if (driving)
        {
            if (audioSource.clip != FounderBikeDrive && FounderBikeDrive != null)
            {
                audioSource.clip = FounderBikeDrive;
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.clip != FounderBikeIdle && FounderBikeIdle != null)
            {
                audioSource.clip = FounderBikeIdle;
                audioSource.Play();
            }
        }
    }

    void Dismount()
    {
        if (player != null)
        {
            player.transform.position = transform.position;
            player.transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            player.SetActive(true);
        }

        if (cameraFollow != null && player != null)
        {
            cameraFollow.target = player.transform;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        gameObject.SetActive(false);
    }
}