using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction sprintAction;
    [SerializeField] private InputAction jumpAction;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 7f;
    [SerializeField] private float sprintSpeed = 12f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 25f;

    [Header("Jumping")]
    [SerializeField] private float jumpHeight = 1.8f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private int maxJumps = 2;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckRadius = 0.28f;
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Wall Run")]
    [SerializeField] private float wallRunSpeed = 9f;
    [SerializeField] private float wallJumpUpForce = 7f;
    [SerializeField] private float wallJumpAwayForce = 8f;

    private CharacterController _controller;
    private Vector3 _velocity;
    private Vector3 _smoothMove;
    private int _jumpsRemaining;
    private bool _isGrounded;
    private Collider[] _groundColliders = new Collider[5];

    // Wall run — populated by OnControllerColliderHit each frame
    private bool _isWallRunning;
    private bool _touchingWall;
    private Vector3 _wallNormal;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        // Default bindings if none assigned in Inspector
        if (moveAction.bindings.Count == 0)
        {
            moveAction = new InputAction("Move", binding: "<Keyboard>/w");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up",    "<Keyboard>/w")
                .With("Down",  "<Keyboard>/s")
                .With("Left",  "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
        }

        if (sprintAction.bindings.Count == 0)
            sprintAction = new InputAction("Sprint", binding: "<Keyboard>/leftShift");

        if (jumpAction.bindings.Count == 0)
            jumpAction = new InputAction("Jump", binding: "<Keyboard>/space");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        sprintAction.Enable();
        jumpAction.Enable();
        jumpAction.performed += OnJump;
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJump;
        moveAction.Disable();
        sprintAction.Disable();
        jumpAction.Disable();
    }

    private void Update()
    {
        // Reset wall contact — OnControllerColliderHit will re-set it during Move if still touching
        _touchingWall = false;

        CheckGround();
        ApplyMovement();
        ApplyGravity();

        _controller.Move(_velocity * Time.deltaTime);
        // OnControllerColliderHit fires above ^^^

        // Evaluate wall run AFTER Move so _touchingWall is up to date
        CheckWall();
    }

    // Called by Unity automatically whenever CharacterController collides with geometry
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Only care about walls (surfaces with a mostly horizontal normal)
        float upDot = Vector3.Dot(hit.normal, Vector3.up);
        if (Mathf.Abs(upDot) < 0.25f)
        {
            _touchingWall = true;
            _wallNormal = hit.normal;
        }
    }

    private void CheckGround()
    {
        Vector3 sphereBottom = transform.position + Vector3.down * (_controller.height / 2f - groundCheckRadius);
        bool wasGrounded = _isGrounded;

        int numColliders = Physics.OverlapSphereNonAlloc(sphereBottom, groundCheckRadius + groundCheckDistance, _groundColliders, groundLayers, QueryTriggerInteraction.Ignore);
        _isGrounded = false;
        for (int i = 0; i < numColliders; i++)
        {
            if (_groundColliders[i].gameObject != gameObject)
            {
                _isGrounded = true;
                break;
            }
        }

        if (_isGrounded && !wasGrounded && _velocity.y <= 0f)
        {
            _jumpsRemaining = maxJumps;
            _isWallRunning = false;
        }

        // Only snap velocity on actual ground contact, not just within detection range
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    private void CheckWall()
    {
        // Can't wall-run while grounded
        if (_isGrounded)
        {
            _isWallRunning = false;
            return;
        }

        // Player must be pressing a move direction to wall-run
        Vector2 input = moveAction.ReadValue<Vector2>();
        bool movingForward = input.sqrMagnitude > 0.1f;

        if (_touchingWall && movingForward)
        {
            if (!_isWallRunning)
            {
                // Just started wall running — give back one jump for wall jump
                _jumpsRemaining = 1;
                _isWallRunning = true;
            }
        }
        else
        {
            _isWallRunning = false;
        }
    }

    private void ApplyMovement()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        bool sprinting = sprintAction.ReadValue<float>() > 0.5f;

        float targetSpeed = input.sqrMagnitude > 0.01f
            ? (_isWallRunning ? wallRunSpeed : (sprinting ? sprintSpeed : walkSpeed))
            : 0f;

        Vector3 moveDir = transform.right * input.x + transform.forward * input.y;
        Vector3 targetMove = moveDir.normalized * targetSpeed;

        float blendRate = targetSpeed > _smoothMove.magnitude ? acceleration : deceleration;
        _smoothMove = Vector3.MoveTowards(_smoothMove, targetMove, blendRate * Time.deltaTime);

        _velocity.x = _smoothMove.x;
        _velocity.z = _smoothMove.z;
    }

    private void ApplyGravity()
    {
        if (_isWallRunning)
        {
            // Hold the player completely still vertically while touching the wall
            _velocity.y = 0f;
        }
        else
        {
            _velocity.y += gravity * Time.deltaTime;
        }
    }

    private void OnJump(InputAction.CallbackContext ctx)
    {
        if (_isWallRunning)
        {
            // Launch away from the wall and upward
            _velocity = _wallNormal * wallJumpAwayForce;
            _velocity.y = wallJumpUpForce;
            _isWallRunning = false;
            _jumpsRemaining = 0;
            return;
        }

        if (_isGrounded && _velocity.y <= 0f)
            _jumpsRemaining = maxJumps;

        if (_jumpsRemaining > 0)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpsRemaining--;
        }
    }
}
