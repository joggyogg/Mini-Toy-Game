using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Plain-English purpose:
/// Attach this to a simple player capsule to get basic movement and jumping.
///
/// This component supports two input paths:
/// - Preferred: the project's Input System action asset using the existing Player/Move and Player/Jump actions.
/// - Fallback: direct keyboard input with WASD for movement and Space for jump.
///
/// It uses CharacterController so the player can be moved immediately without setting up a Rigidbody-based controller.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    private const float DefaultGravity = -25f;
    private const float GroundedVerticalVelocity = -2f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float decorateMoveSpeed = 5f;
    [SerializeField] private float airControlPercent = 0.45f;
    [SerializeField] private float rotationSharpness = 14f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = DefaultGravity;

    [Header("Orientation")]
    [SerializeField] private Transform movementReference;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string jumpActionName = "Jump";

    private CharacterController characterController;
    private InputAction moveAction;
    private InputAction jumpAction;
    private Vector3 velocity;
    private Vector2 moveInput;
    private bool jumpRequested;

    // Decorate mode
    private bool inDecorateMode;
    private TerrainGridAuthoring decorateGrid;
    private Vector2Int currentFullTile;
    private Vector3 decorateMoveOrigin;
    private Vector3 decorateMoveTarget;
    private float decorateMoveProgress = 1f; // 0..1; 1 means arrived at target tile

    public bool IsInDecorateMode => inDecorateMode;
    public Vector2Int CurrentFullTile => currentFullTile;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        ResolveInputActions();
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        jumpAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
    }

    private void Update()
    {
        ReadInput();
        if (inDecorateMode)
        {
            UpdateDecorateMovement();
            return;
        }
        UpdateMovement();
        UpdateRotation();
        jumpRequested = false;
    }

    private void ResolveInputActions()
    {
        if (inputActions == null)
        {
            moveAction = null;
            jumpAction = null;
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap(playerActionMapName, false);
        if (playerMap == null)
        {
            moveAction = null;
            jumpAction = null;
            return;
        }

        moveAction = playerMap.FindAction(moveActionName, false);
        jumpAction = playerMap.FindAction(jumpActionName, false);
    }

    private void ReadInput()
    {
        // Prefer the configured Input System asset when it exists. Direct keyboard/gamepad fallback keeps the
        // capsule usable even before the action asset is wired in the inspector.
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }
        else
        {
            moveInput = ReadDirectMoveFallback();
        }

        bool pressedJump;
        if (jumpAction != null)
        {
            pressedJump = jumpAction.WasPressedThisFrame();
        }
        else if (Keyboard.current != null)
        {
            pressedJump = Keyboard.current.spaceKey.wasPressedThisFrame;
        }
        else if (Gamepad.current != null)
        {
            pressedJump = Gamepad.current.buttonSouth.wasPressedThisFrame;
        }
        else
        {
            pressedJump = false;
        }

        if (pressedJump)
        {
            jumpRequested = true;
        }
    }

    private void UpdateMovement()
    {
        Vector3 desiredMove = GetDesiredMoveDirection();
        float controlPercent = characterController.isGrounded ? 1f : airControlPercent;
        Vector3 horizontalVelocity = desiredMove * (moveSpeed * controlPercent);

        if (characterController.isGrounded && velocity.y < 0f)
        {
            velocity.y = GroundedVerticalVelocity;
        }

        if (characterController.isGrounded && jumpRequested)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;

        Vector3 frameMotion = (horizontalVelocity * Time.deltaTime) + (Vector3.up * velocity.y * Time.deltaTime);
        characterController.Move(frameMotion);
    }

    private void UpdateRotation()
    {
        Vector3 desiredMove = GetDesiredMoveDirection();
        if (desiredMove.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(desiredMove, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSharpness * Time.deltaTime);
    }

    private Vector3 GetDesiredMoveDirection()
    {
        Vector3 referenceForward;
        Vector3 referenceRight;

        if (movementReference != null)
        {
            referenceForward = movementReference.forward;
            referenceRight = movementReference.right;
        }
        else
        {
            referenceForward = Vector3.forward;
            referenceRight = Vector3.right;
        }

        referenceForward.y = 0f;
        referenceRight.y = 0f;
        referenceForward.Normalize();
        referenceRight.Normalize();

        Vector3 desiredMove = (referenceRight * moveInput.x) + (referenceForward * moveInput.y);
        if (desiredMove.sqrMagnitude > 1f)
        {
            desiredMove.Normalize();
        }

        return desiredMove;
    }

    private static Vector2 ReadDirectMoveFallback()
    {
        Vector2 value = Vector2.zero;

        if (Keyboard.current != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (Keyboard.current.aKey.isPressed)
            {
                horizontal -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                horizontal += 1f;
            }

            if (Keyboard.current.sKey.isPressed)
            {
                vertical -= 1f;
            }

            if (Keyboard.current.wKey.isPressed)
            {
                vertical += 1f;
            }

            value = new Vector2(horizontal, vertical);
        }

        if (value.sqrMagnitude < 0.0001f && Gamepad.current != null)
        {
            value = Gamepad.current.leftStick.ReadValue();
        }

        return value.sqrMagnitude > 1f ? value.normalized : value;
    }

    // ── Decorate mode ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Switches to tile-locked grid movement over the given terrain.
    /// The player snaps to the nearest walkable full tile immediately.
    /// </summary>
    public void EnterDecorateMode(TerrainGridAuthoring grid)
    {
        if (grid == null) return;
        if (!grid.TryGetNearestWalkableFullTile(transform.position, out Vector2Int tile)) return;

        decorateGrid = grid;
        inDecorateMode = true;
        currentFullTile = tile;
        decorateMoveProgress = 1f;
        velocity = Vector3.zero;

        if (grid.TryGetFullTileCenterWorld(tile.x, tile.y, out Vector3 center))
        {
            decorateMoveOrigin = center;
            decorateMoveTarget = center;
            characterController.Move(center - transform.position);
        }
    }

    /// <summary>
    /// Returns the player to normal free-movement mode.
    /// </summary>
    public void ExitDecorateMode()
    {
        inDecorateMode = false;
        decorateGrid = null;
        velocity = Vector3.zero;
    }

    private void UpdateDecorateMovement()
    {
        if (decorateGrid == null)
        {
            ExitDecorateMode();
            return;
        }

        // Advance step lerp.
        if (decorateMoveProgress < 1f)
        {
            decorateMoveProgress += decorateMoveSpeed * Time.deltaTime;
            float t = Mathf.Clamp01(decorateMoveProgress);
            Vector3 lerpedPos = Vector3.Lerp(decorateMoveOrigin, decorateMoveTarget, t);
            characterController.Move(lerpedPos - transform.position);
            return;
        }

        // Snap precisely to the tile centre once arrived.
        if (transform.position != decorateMoveTarget)
        {
            characterController.Move(decorateMoveTarget - transform.position);
        }

        // Wait for directional input before starting the next step.
        if (moveInput.sqrMagnitude < 0.25f) return;

        Vector2Int nextTile = GetDecorateStepTile();
        if (nextTile == currentFullTile) return;
        if (!decorateGrid.IsFullTileWalkable(nextTile.x, nextTile.y)) return;
        if (!decorateGrid.TryGetFullTileCenterWorld(nextTile.x, nextTile.y, out Vector3 nextCenter)) return;

        decorateMoveOrigin = transform.position;
        decorateMoveTarget = nextCenter;
        decorateMoveProgress = 0f;
        currentFullTile = nextTile;
    }

    private Vector2Int GetDecorateStepTile()
    {
        Vector3 worldDir = GetDesiredMoveDirection();
        if (worldDir.sqrMagnitude < 0.01f) return currentFullTile;

        Transform gridTransform = decorateGrid.SurfaceTransform;
        float dotRight = Vector3.Dot(worldDir, gridTransform.right);
        float dotForward = Vector3.Dot(worldDir, gridTransform.forward);

        int dx, dz;
        if (Mathf.Abs(dotRight) >= Mathf.Abs(dotForward))
        {
            dx = dotRight >= 0f ? 1 : -1;
            dz = 0;
        }
        else
        {
            dx = 0;
            dz = dotForward >= 0f ? 1 : -1;
        }

        return new Vector2Int(currentFullTile.x + dx, currentFullTile.y + dz);
    }
}