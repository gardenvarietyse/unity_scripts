/*
  first person camera with mouse/gamepad support
*/

namespace GardenVariety
{
  using UnityEngine;

  public class FirstPersonCamera : MonoBehaviour
  {
    public float lookAngleLimit = 80.0f;

    public float mouseSensitivity = 3.0f;
    public float stickSensitivity = 3.0f;

    float turnAngle = 0.0f;
    float lookAngle = 0.0f;

    new Camera camera;

    void Start()
    {
      camera = GetComponentInChildren<Camera>();

      if (!camera)
      {
        Debug.LogError("FirstPersonCamera requires a child object with a Camera component");
      }

      turnAngle = transform.rotation.eulerAngles.y;
    }

    void Update()
    {
      if (Input.GetKey(KeyCode.Escape))
      {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
      }
      else if (Input.GetMouseButtonDown(0))
      {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
      }

      HandleMouse();
      HandleStick();

      Quaternion turnRotation = Quaternion.Euler(0.0f, turnAngle, 0.0f);
      transform.rotation = turnRotation;

      lookAngle = Mathf.Clamp(lookAngle, -lookAngleLimit, lookAngleLimit);
      Quaternion lookRotation = Quaternion.Euler(lookAngle, 0.0f, 0.0f);

      if (camera != null)
      {

        camera.transform.localRotation = lookRotation;
      }
    }

    void HandleMouse()
    {
      if (Cursor.lockState == CursorLockMode.None)
      {
        return;
      }

      float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
      float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

      turnAngle += mouseX;
      lookAngle -= mouseY;
    }

    void HandleStick()
    {
      float horizontal = Input.GetAxis("Horizontal 2");
      float vertical = Input.GetAxis("Vertical 2");

      turnAngle += horizontal * stickSensitivity;
      lookAngle -= vertical * stickSensitivity;
    }
  }
}
