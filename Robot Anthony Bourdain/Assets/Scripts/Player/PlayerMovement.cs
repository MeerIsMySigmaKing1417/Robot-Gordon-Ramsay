using System;
using Unity.Cinemachine;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // References
    [SerializeField] private Transform playerCam;
    [SerializeField] private Transform feet;
    private PlayerInputActions inputActions;
    private Rigidbody rb;

    // Input System
    private bool jumpInput = false;
    private bool sprintInput = false;
    private bool crouchInput = false;
    private Vector2 moveInput = Vector2.zero;
    private Vector2 lookInput = Vector2.zero;

    [Header("Camera Settings")]
    public CinemachineCamera cinemachineCam;
    [SerializeField] private float mouseSensitivityX = 0.5f;
    [SerializeField] private float mouseSensitivityY = 0.5f;
    [Range(0f, 100f)]
    [SerializeField] private float normalFOV = 60f; // Normal field of view
    [Range(0f, 100f)]
    [SerializeField] private float crouchFOV = 50f; // Field of view when crouching
    [Range(0f, 100f)]
    [SerializeField] private float sprintFOV = 70f; // Field of view when sprinting
    private float xRotation = 0f;
    private float currentFOV;

    [Header("Bobbing Settings")]
    [SerializeField] private float bobFrequency = 2f;
    [SerializeField] private float bobHorizontalAmplitude = 0.1f;
    [SerializeField] private float bobVerticalAmplitude = 0.1f;
    private CinemachineBasicMultiChannelPerlin noise;
    private float timer = 0f;

    [Header("Movement")]
    public LayerMask groundLayerMask = -1;
    [SerializeField] private float feetRadius = 1f;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 10f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float speedTransitionRate = 8f; // How fast to change speeds
    [SerializeField] private float crouchTransitionSpeed = 8f; // How fast to transition between crouch and stand
    public float currentSpeed = 0f; // The actual speed we're using
    private bool isGrounded = false;
    private float standHeight = 1f; // Height when standing
    private float crouchHeight = 0.5f; // Height when crouching
    private Vector3 intitialCameraPosition;
    private float currentHeight;
    private bool isMovingBackward = false;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 5f;
    [SerializeField] private float maxFallSpeed = 12f; // Terminal velocity
    [SerializeField] private float fallMultiplier = 2f; // Faster falling
    [SerializeField] private float lowJumpMultiplier = 3f; // Even faster when not holding jump
    [SerializeField] private float airControl = 0.3f; // Reduced air control (0-1)

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        inputActions = new PlayerInputActions();
        standHeight = currentHeight = transform.localScale.y; // Store the initial height of the player
        intitialCameraPosition = playerCam.localPosition; // Store the initial camera position
        noise = cinemachineCam.GetComponent<CinemachineBasicMultiChannelPerlin>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnEnable()
    {
        inputActions.Enable();
        inputActions.Player.Enable();

        // Movement input
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // Look input
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        // Jump input
        inputActions.Player.Jump.performed += ctx => jumpInput = true;
        inputActions.Player.Jump.canceled += ctx => jumpInput = false;

        // Sprint input
        inputActions.Player.Sprint.performed += ctx => sprintInput = true;
        inputActions.Player.Sprint.canceled += ctx => sprintInput = false;

        // Crouch input
        inputActions.Player.Crouch.performed += ctx => crouchInput = true;
        inputActions.Player.Crouch.canceled += ctx => crouchInput = false;
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    private void LateUpdate()
    {
        FPSCamera();
    }

    private void Update()
    {
        RotatePlayerTowardsCamera();
        ViewBobbing();
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        Movement();
        Gravity();
    }

    private void Movement()
    {
        Vector3 moveDirection = new Vector3(moveInput.x, 0f, moveInput.y).normalized;
        if (moveDirection.magnitude >= 0.1f)
        {
            // Check if moving backwards relative to player's facing direction
            Vector3 worldMoveDirection = transform.TransformDirection(moveDirection);
            isMovingBackward = Vector3.Dot(worldMoveDirection.normalized, transform.forward) < -0.1f;

            float targetSpeed;
            if (crouchInput)
            {
                targetSpeed = crouchSpeed;
            }
            else if (sprintInput && !isMovingBackward)
            {
                targetSpeed = sprintSpeed;
            }
            else
            {
                targetSpeed = moveSpeed;
            }

            // Force faster deceleration when moving backward while trying to sprint
            if (isMovingBackward && sprintInput && currentSpeed > moveSpeed)
            {
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, speedTransitionRate * 3f * Time.fixedDeltaTime);
            }
            else
            {
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, speedTransitionRate * Time.fixedDeltaTime);
            }

            Vector3 moveVelocity = transform.TransformDirection(moveDirection) * currentSpeed;

            if (isGrounded)
            {
                // Full control on ground
                rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);
            }
            else
            {
                // Reduced air control - can't change direction as easily
                Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                Vector3 targetHorizontal = new Vector3(moveVelocity.x, 0, moveVelocity.z);
                Vector3 newHorizontal = Vector3.Lerp(currentHorizontal, targetHorizontal, airControl * Time.fixedDeltaTime * 10f);
                rb.linearVelocity = new Vector3(newHorizontal.x, rb.linearVelocity.y, newHorizontal.z);
            }
        }
        else
        {
            // Gradually slow down when not moving
            currentSpeed = Mathf.Lerp(currentSpeed, 0f, speedTransitionRate * Time.fixedDeltaTime);
            if (isGrounded)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
        }

        // Handle jumping
        if (jumpInput && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            jumpInput = false;
        }

        //crouch
        var targetHeight = crouchInput && isGrounded ? crouchHeight: standHeight;

        var crouchDelta = Time.deltaTime * crouchTransitionSpeed;
        currentHeight = Mathf.Lerp(currentHeight, targetHeight, crouchDelta);

        var halfHeightDifference = new Vector3(0, (standHeight - targetHeight) / 2, 0);
        var newCameraPosition = intitialCameraPosition - halfHeightDifference;

        playerCam.transform.localPosition = newCameraPosition;
        transform.localScale = new Vector3(transform.localScale.x, currentHeight, transform.localScale.z);
    }

    private void Gravity()
    {
        if (rb.linearVelocity.y < 0)
        {
            // Falling - apply extra downward force
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !jumpInput)
        {
            // Going up but not holding jump - fall much faster (variable jump height)
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }

        // Cap the fall speed to prevent slamming
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -maxFallSpeed, rb.linearVelocity.z);
        }
    }

    private void CheckGrounded()
    {
        bool sphereCheck = Physics.CheckSphere(feet.position, feetRadius, groundLayerMask);
        isGrounded = sphereCheck;
    }

    private void FPSCamera()
    {
        float mouseX = lookInput.x * mouseSensitivityX;
        float mouseY = lookInput.y * mouseSensitivityY;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Smooth camera rotation to reduce jitter
        Quaternion targetRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerCam.localRotation = Quaternion.Lerp(playerCam.localRotation, Quaternion.Euler(xRotation, 0f, 0f), Time.deltaTime * 30f);

        // Smooth horizontal rotation too
        transform.Rotate(Vector3.up * mouseX);

        var targetFOV = crouchInput ? crouchFOV : sprintInput && moveInput.magnitude > 0.1f && !isMovingBackward ? sprintFOV : normalFOV;

        currentFOV = Mathf.Lerp(currentFOV, targetFOV, Time.deltaTime * 5f);

        cinemachineCam.Lens.FieldOfView = currentFOV;
    }

    private void ViewBobbing()
    {
        bool isMoving = moveInput.magnitude > 0.1f && isGrounded;

        if (isMoving)
        {
            timer += Time.deltaTime * bobFrequency;

            // Apply bobbing through amplitude and frequency
            noise.AmplitudeGain = bobVerticalAmplitude;
            noise.FrequencyGain = bobFrequency;
        }
        else
        {
            // Gradually reduce bobbing when not moving
            noise.AmplitudeGain = Mathf.Lerp(noise.AmplitudeGain, 0f, Time.deltaTime * 5f);
            noise.FrequencyGain = Mathf.Lerp(noise.FrequencyGain, 0f, Time.deltaTime * 5f);
        }
    }


    private void RotatePlayerTowardsCamera()
    {
        if (playerCam != null)
        {
            Vector3 cameraForward = playerCam.transform.forward;
            cameraForward.y = 0f; // Ignore the y-axis rotation

            if (cameraForward != Vector3.zero)
            {
                Quaternion newRotation = Quaternion.LookRotation(cameraForward);
                transform.rotation = newRotation;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 sphereCenter;
        if (feet != null)
        {
            sphereCenter = feet.position - new Vector3(0, 0.1f, 0);
        }
        else
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                sphereCenter = col.bounds.min + new Vector3(0, 0.1f, 0);
            }
            else
            {
                sphereCenter = transform.position - new Vector3(0, 0.9f, 0);
            }
        }

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(sphereCenter, feetRadius);
    }
}