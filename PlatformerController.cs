/*
  decent controller for side scrolling games
  supports basic movement, single/double jumping, wall hug/slide, wall jump

  assumes left/right movement is along the X axis
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
    public float XInput { get { return _xInput; } }
    public bool PressedJump { get { return _pressedJump; } }
    public bool ReleasedJump { get { return _releasedJump; } }

    float _xInput;
    bool _pressedJump;
    bool _releasedJump;
    bool _holdingJump;

    public void Update()
    {
      float horizontal = Input.GetAxis("Horizontal");
      float deadzone = 0.2f;

      _xInput = Mathf.Abs(horizontal) >= deadzone ? horizontal : 0.0f;

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
  public class PlatformerController : MonoBehaviour
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
    public ControllerState State { get { return controllerState; } }
    public float VelocityX { get { return velocityX; } }
    public float VelocityY { get { return velocityY; } }
    public float MovementSpeed { get { return movementSpeed; } }

    // private shit
    CharacterController controller;

    ControllerState controllerState = ControllerState.InAir;
    ControllerState previousControllerState;
    ControllerState lastDistinctControllerState;

    ControllerInput inputState;

    float velocityX = 0.0f;
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

      ProcessInput();
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

    void ProcessInput()
    {
      inputState.Update();
    }

    CollisionFlags ProcessPhysics()
    {
      switch (controllerState)
      {
        case ControllerState.Grounded:
          UpdateXAcceleration();
          velocityY = -groundStick;

          break;
        case ControllerState.InAir:
          UpdateXAcceleration();
          velocityY = Mathf.Max(-gravity, velocityY - gravity * Time.deltaTime);

          break;
        case ControllerState.WallHugging:
          velocityX = inputState.XInput;
          velocityY = Mathf.Max(-wallHugGravity, velocityY - wallHugGravity * Time.deltaTime);

          break;
        case ControllerState.WallJumping:
          // x velocity is fixed
          velocityY = Mathf.Max(-gravity, velocityY - gravity * Time.deltaTime);
          break;
      }

      Vector3 position = transform.position;

      Vector3 xMove = transform.right * (velocityX * Time.deltaTime + 0.5f * movementSpeed * (Time.deltaTime * Time.deltaTime));
      Vector3 yMove = transform.up * (velocityY * Time.deltaTime + 0.5f * gravity * (Time.deltaTime * Time.deltaTime));

      var flags = controller.Move(xMove + yMove);

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

    void UpdateXAcceleration()
    {
      if (inputState.XInput > 0.0f)
      {
        velocityX = Mathf.Min(movementSpeed, velocityX + movementAcceleration * Time.deltaTime);
      }
      else if (inputState.XInput < 0.0f)
      {
        velocityX = Mathf.Max(-movementSpeed, velocityX - movementAcceleration * Time.deltaTime);
      }
      else
      {
        velocityX = Mathf.Lerp(velocityX, 0.0f, Time.deltaTime * movementAcceleration * 2.0f);

        if (Mathf.Abs(velocityX) < 0.1f)
        {
          velocityX = 0.0f;
        }
      }
    }

    void EnterWallHugState()
    {
      controllerState = ControllerState.WallHugging;
      velocityY = 0.0f;
      wallJumpDirection = velocityX > 0.0f ? MovementDirection.Left : MovementDirection.Right;
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

      velocityX = wallJumpDirection == MovementDirection.Right ? movementSpeed : -movementSpeed;
      velocityY = Mathf.Sqrt(-2.0f * -gravity * wallJumpHeight);
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
