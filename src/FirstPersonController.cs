/*
  3d-ified version of PlatformerController
  probably works well for third person too, or anything with 3d movement really

  wall jumping is not yet adapted for 3d use
*/
namespace GardenVariety
{
  using UnityEngine;

  public enum ControllerState
  {
    Grounded,
    InAir,
    WallHugging,
    WallJumping,
  }

  enum MovementDirection
  {
    Left,
    Right,
    None,
  }

  class ControllerInput
  {
    static float DEADZONE = 0.2f;

    public float ForwardInput => _forwardInput;
    public float RightInput => _rightInput;

    public bool PressedJump => _pressedJump;
    public bool ReleasedJump => _releasedJump;

    float _forwardInput;
    float _rightInput;

    bool _pressedJump;
    bool _releasedJump;
    bool _holdingJump;

    public void Update()
    {
      float forward = Input.GetAxis("Vertical");
      _forwardInput = Mathf.Abs(forward) >= DEADZONE ? forward : 0.0f;

      float right = Input.GetAxis("Horizontal");
      _rightInput = Mathf.Abs(right) >= DEADZONE ? right : 0.0f;

      _pressedJump = false;
      _releasedJump = false;

      if (!_holdingJump && Input.GetButtonDown("Jump"))
      {
        _pressedJump = true;
        _holdingJump = true;
      }
      else if (_holdingJump && Input.GetButtonUp("Jump"))
      {
        _releasedJump = true;
        _holdingJump = false;
      }
    }
  }

  [RequireComponent(typeof(CharacterController))]
  public class FirstPersonController : MonoBehaviour
  {
    [Header("Basic")]
    public float movementSpeed = 8f;
    public float movementAcceleration = 20.0f;
    public float gravity = 40.0f;

    [Space]
    public float jumpHeight = 2.0f;
    public int jumpGraceFrames = 5;
    public bool variableJump = true;
    public float variableJumpCut = 2.0f;

    [Header("Fancy moves")]
    public bool canAirJump = false;
    [Space]
    public bool canWallHug = false;
    public float wallHugGravity = 5.0f;
    public float wallHugRequiredAirtime = 0.15f;
    [Space]
    public bool canWallJump = true;
    public float wallJumpHeight = 1.0f;
    public float wallJumpTime = 1.0f;

    [Header("Tweaks")]
    public float groundStick = 1.0f;

    [Header("Debug")]
    public bool printDebugLogs = false;

    // api
    public ControllerState State => controllerState;
    public float VelocityForward => velocityForward;
    public float VelocityRight => velocityRight;
    public float VelocityY => velocityY;
    public float MovementSpeed => movementSpeed;

    // private shit
    CharacterController controller;

    ControllerState controllerState = ControllerState.InAir;
    ControllerState previousControllerState;
    ControllerState lastDistinctControllerState;

    ControllerInput inputState;

    float velocityForward = 0.0f;
    float velocityRight = 0.0f;
    float velocityY = 0.0f;

    float jumpGraceTime = 0.0f;
    float airTime = 30.0f;
    bool airJumpAvailable = false;

    MovementDirection wallJumpDirection = MovementDirection.Right;
    float wallJumpTimer = 0.0f;

    // unity methods

    void Start()
    {
      inputState = new ControllerInput();
      controller = GetComponent<CharacterController>();

      previousControllerState = controllerState;
      lastDistinctControllerState = controllerState;
    }

    void Update()
    {
      ComputeDynamicAttributes();

      inputState.Update();
      var flags = ProcessPhysics();

      previousControllerState = controllerState;
      ProcessState(flags);

      if (controllerState != previousControllerState)
      {
        lastDistinctControllerState = controllerState;
        Log($"State change {previousControllerState} -> {controllerState}");
      }
    }

    // controller

    void ComputeDynamicAttributes()
    {
      jumpGraceTime = jumpGraceFrames / 60.0f;
    }

    CollisionFlags ProcessPhysics()
    {
      switch (controllerState)
      {
        case ControllerState.Grounded:
          UpdateHorizontalAcceleration();
          velocityY = -groundStick;

          break;
        case ControllerState.InAir:
          UpdateHorizontalAcceleration();
          velocityY = Mathf.Max(-gravity, velocityY - gravity * Time.deltaTime);

          break;
        case ControllerState.WallHugging:
          velocityForward = inputState.ForwardInput;
          velocityRight = inputState.RightInput;
          velocityY = Mathf.Max(-wallHugGravity, velocityY - wallHugGravity * Time.deltaTime);

          break;
        case ControllerState.WallJumping:
          // forward, right velocity is fixed
          velocityY = Mathf.Max(-gravity, velocityY - gravity * Time.deltaTime);
          break;
      }

      Vector3 position = transform.position;

      Vector3 forwardMove = transform.forward * (velocityForward * Time.deltaTime + 0.5f * movementSpeed * (Time.deltaTime * Time.deltaTime));
      Vector3 rightMove = transform.right * (velocityRight * Time.deltaTime + 0.5f * movementSpeed * (Time.deltaTime * Time.deltaTime));
      Vector3 yMove = transform.up * (velocityY * Time.deltaTime + 0.5f * gravity * (Time.deltaTime * Time.deltaTime));

      var flags = controller.Move(forwardMove + rightMove + yMove);

      // wall hug ascension hack fix
      if (controllerState == ControllerState.WallHugging && wallHugGravity == 0.0f)
      {
        transform.position = position;
      }

      return flags;
    }

    void ProcessState(CollisionFlags flags)
    {
      switch (controllerState)
      {
        case ControllerState.Grounded:
          if (inputState.PressedJump)
          {
            Jump();
          }

          airTime = 0.0f;
          airJumpAvailable = true;

          if ((flags & CollisionFlags.Below) == 0)
          {
            controllerState = ControllerState.InAir;
          }

          break;
        case ControllerState.InAir:
          airTime += Time.deltaTime;

          if (inputState.PressedJump)
          {
            if (canAirJump && airJumpAvailable)
            {
              Jump();
              airJumpAvailable = false;
            }
            else if (airTime < jumpGraceTime)
            {
              if (canWallJump && lastDistinctControllerState == ControllerState.WallHugging)
              {
                Log($"Grace wall jump, airtime {airTime}s (last state {previousControllerState})");
                WallJump();
              }
              else if (lastDistinctControllerState != ControllerState.WallJumping)
              {
                Log($"Grace jump, airtime {airTime}s (last state {previousControllerState})");
                Jump();
              }
            }
          }
          else if (variableJump && inputState.ReleasedJump && velocityY > 0.0f)
          {
            velocityY /= variableJumpCut;
          }
          else if (canWallHug && (flags & CollisionFlags.Sides) != 0 && airTime >= wallHugRequiredAirtime)
          {
            EnterWallHugState();
          }
          else if ((flags & CollisionFlags.Below) != 0)
          {
            controllerState = ControllerState.Grounded;
          }

          break;
        case ControllerState.WallHugging:
          airTime = 0.0f;

          if (inputState.PressedJump && canWallJump)
          {
            WallJump();
          }
          else if ((flags & CollisionFlags.Below) != 0)
          {
            controllerState = ControllerState.Grounded;
          }
          else if ((flags & CollisionFlags.Sides) == 0)
          {
            controllerState = ControllerState.InAir;
          }

          break;
        case ControllerState.WallJumping:
          wallJumpTimer -= Time.deltaTime;

          if ((flags & CollisionFlags.Sides) != 0)
          {
            EnterWallHugState();
          }
          else if ((flags & CollisionFlags.Below) != 0)
          {
            controllerState = ControllerState.Grounded;
          }
          else if (canAirJump && airJumpAvailable)
          {
            Jump();
            airJumpAvailable = false;
          }
          else if (wallJumpTimer <= 0.0f)
          {
            controllerState = ControllerState.InAir;
          }

          break;
      }
    }

    void UpdateHorizontalAcceleration()
    {
      velocityForward = inputState.ForwardInput * movementSpeed;
      velocityRight = inputState.RightInput * movementSpeed;
    }

    void EnterWallHugState()
    {
      // todo: store velocity here so we can replace left/right walljump directions
      controllerState = ControllerState.WallHugging;
      velocityY = 0.0f;
      wallJumpDirection = velocityForward > 0.0f ? MovementDirection.Left : MovementDirection.Right;
    }

    void Jump()
    {
      controllerState = ControllerState.InAir;
      velocityY = Mathf.Sqrt(-2.0f * -gravity * jumpHeight);
    }

    void WallJump()
    {
      controllerState = ControllerState.WallJumping;
      wallJumpTimer = wallJumpTime;

      velocityForward = wallJumpDirection == MovementDirection.Right ? movementSpeed : -movementSpeed;
      velocityY = Mathf.Sqrt(-2.0f * -gravity * wallJumpHeight);
    }

    void OnDrawGizmos()
    {
      Gizmos.color = new Color(1, 0, 0, 0.5f);
      Gizmos.DrawCube(transform.position, new Vector3(1, 1, 1));
    }

    void Log(string message)
    {
      if (Application.isEditor && printDebugLogs)
      {
        Debug.Log(message);
      }
    }
  }
}
