using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Camera thisCamera;

    private const float panSpeed = 1f;
    private const float movementTime = 5f;
    private const float zoomSpeed = 10f;

    private const float minZoom = 300f;
    private const float maxZoom = 20f;


    void Update()
    {
        MoveCamera();
    }

    private void MoveCamera()
    {
        Vector3 newPosition = transform.position;
        float newZoom = thisCamera.orthographicSize;
        Quaternion newRotation = transform.rotation;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            newPosition += (transform.forward * panSpeed);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) 
            newPosition += (transform.forward * -panSpeed);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            newPosition += (transform.right * panSpeed);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            newPosition += (transform.right * -panSpeed);

        if (Input.mouseScrollDelta.y != 0)
            newZoom += Input.mouseScrollDelta.y * zoomSpeed;



        // for smooth camera movement
        transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * movementTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * movementTime);
        thisCamera.orthographicSize = Mathf.Lerp(thisCamera.orthographicSize, newZoom, Time.deltaTime);
    }
}
