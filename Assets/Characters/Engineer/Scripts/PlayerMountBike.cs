using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMountBike : MonoBehaviour
{
    public GameObject motorcycle;
    public CameraFollow cameraFollow;

    void Update()
    {
        if (Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            MountBike();
        }
    }

    void MountBike()
    {
        motorcycle.transform.position = transform.position;
        motorcycle.transform.rotation = transform.rotation;

        MotorcycleController controller =
            motorcycle.GetComponent<MotorcycleController>();

        if (controller != null)
        {
            controller.player = gameObject;
            controller.cameraFollow = cameraFollow;
        }

        motorcycle.SetActive(true);

        if (cameraFollow != null)
        {
            cameraFollow.target = motorcycle.transform;
        }

        gameObject.SetActive(false);
    }
}