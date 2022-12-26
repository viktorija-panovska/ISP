
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Camera thisCamera;

    private const float movementTime = 50f;
    private const float movementSpeed = 1f;

    private const float zoomTime = 50f;
    private const float zoomSpeed = 1f;
    private const float minZoom = 30f;
    private const float maxZoom = 120f;

    private const float rotationTime = 5f;
    private const float rotationSpeed = 1f;


    void Update()
    {
        MoveCamera();
    }

    private void MoveCamera()
    {
        Vector3 newPosition = thisCamera.transform.position;
        float newZoom = thisCamera.fieldOfView;
        Quaternion newRotation = Quaternion.Euler(Vector3.up * thisCamera.transform.rotation.eulerAngles.y);

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            newPosition += (transform.forward * movementSpeed);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            newPosition += (transform.forward * -movementSpeed);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            newPosition += (transform.right * movementSpeed);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            newPosition += (transform.right * -movementSpeed);

        if (Input.mouseScrollDelta.y != 0)
            newZoom += Input.mouseScrollDelta.y * zoomSpeed;

        if (Input.GetKey(KeyCode.Q))
            newRotation *= Quaternion.Euler(Vector3.up * -rotationSpeed);
        if (Input.GetKey(KeyCode.E))
            newRotation *= Quaternion.Euler(Vector3.up * rotationSpeed);

        newRotation = Quaternion.Euler(newRotation.eulerAngles + Vector3.right * thisCamera.transform.rotation.eulerAngles.x);


        // for smooth camera movement

        if (newPosition != thisCamera.transform.position)
            thisCamera.transform.position = Vector3.Lerp(thisCamera.transform.position, newPosition, Time.deltaTime * movementTime);

        if (newZoom != thisCamera.fieldOfView)
            thisCamera.fieldOfView = Mathf.Clamp(Mathf.Lerp(thisCamera.fieldOfView, newZoom, Time.deltaTime * movementTime), minZoom, maxZoom);

        if (newRotation != thisCamera.transform.rotation)
            thisCamera.transform.rotation = Quaternion.Lerp(thisCamera.transform.rotation, newRotation, Time.deltaTime * rotationTime);
    }
}
