/*
  super basic toggle run/walk support to go along with FirstPersonController
*/

namespace GardenVariety
{
  using UnityEngine;

  public class RunWalkController : MonoBehaviour
  {
    float walkSpeed = 3.0f;
    float runSpeed = 8.0f;

    private FirstPersonController controller;

    void Start()
    {
      controller = GetComponent<FirstPersonController>();

      if (controller == null)
      {
        Debug.LogError("RunWalkController requires a FirstPersonController component");
      }
    }

    void Update()
    {
      if (controller == null)
      {
        return;
      }

      if (Input.GetButtonDown("ToggleRun"))
      {
        if (controller.movementSpeed > walkSpeed)
        {
          controller.movementSpeed = walkSpeed;
        }
        else
        {

          controller.movementSpeed = runSpeed;
        }
      }
      else if (Mathf.Abs(controller.VelocityForward + controller.VelocityRight) < 0.1f)
      {
        controller.movementSpeed = walkSpeed;
      }
    }
  }
}