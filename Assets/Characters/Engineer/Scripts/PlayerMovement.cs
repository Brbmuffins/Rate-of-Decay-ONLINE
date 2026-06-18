using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float sprintSpeed = 9f;
    public float jumpForce = 6f;
    public float rotationSpeed = 12f;
    public Camera cam;

    private Animator anim;
    private Rigidbody rb;

    private bool isGrounded = true;
    private bool inWater = false;

    private Vector3 moveDirection = Vector3.zero;
    private Quaternion targetRotation;
    private bool wantsMove = false;
    private bool jumpRequested = false;
    private float currentSpeed;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();

        targetRotation = transform.rotation;
        currentSpeed = moveSpeed;

        anim.SetBool("isGrounded", true);
        anim.SetBool("inWater", false);
        anim.SetBool("isBackwards", false);
    }

    void Update()
    {
        Vector2 input = Vector2.zero;

        bool pressingW = Keyboard.current.wKey.isPressed;
        bool pressingS = Keyboard.current.sKey.isPressed;
        bool pressingA = Keyboard.current.aKey.isPressed;
        bool pressingD = Keyboard.current.dKey.isPressed;

        if (pressingW) input.y += 1;
        if (pressingS) input.y -= 1;
        if (pressingA) input.x -= 1;
        if (pressingD) input.x += 1;

        bool bothMouseHeld = Mouse.current.leftButton.isPressed && Mouse.current.rightButton.isPressed;
        if (bothMouseHeld && input.sqrMagnitude == 0)
        {
            input.y = 1;
        }

        bool isSprinting = Keyboard.current.leftShiftKey.isPressed && input.sqrMagnitude > 0 && !pressingS;
        bool isMoving = input.sqrMagnitude > 0;
        bool isBackwards = pressingS && !bothMouseHeld;

        anim.SetBool("isMoving", isMoving);
        anim.SetBool("isSprinting", isSprinting);
        anim.SetBool("isBackwards", isBackwards);

        if (Keyboard.current.spaceKey.wasPressedThisFrame && isGrounded)
        {
            jumpRequested = true;
        }

        wantsMove = isMoving;
        currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

        if (isMoving)
        {
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;

            camForward.y = 0;
            camRight.y = 0;

            camForward.Normalize();
            camRight.Normalize();

            moveDirection = camForward * input.y + camRight * input.x;
            moveDirection.Normalize();

            Vector3 faceDirection = moveDirection;

            // If S is involved, face opposite the movement direction
            if (pressingS)
            {
                faceDirection = -moveDirection;
            }

            targetRotation = Quaternion.LookRotation(faceDirection);
        }
    }

    void FixedUpdate()
    {
        if (jumpRequested)
        {
            jumpRequested = false;
            isGrounded = false;
            anim.SetBool("isGrounded", false);
            anim.SetTrigger("Jump");

            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }

        if (wantsMove)
        {
            Vector3 targetPosition = rb.position + moveDirection * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(targetPosition);
        }

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            isGrounded = true;
            anim.SetBool("isGrounded", true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            inWater = true;
            anim.SetBool("inWater", true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Water"))
        {
            inWater = false;
            anim.SetBool("inWater", false);
        }
    }
}