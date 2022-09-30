using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Camera mainCamera;

    private const float panSpeed = 150f;
    private const float zoomSpeed = 100f;

    void Update()
    {
        MoveCamera();
    }

    private void MoveCamera()
    {
        Vector3 newPosition = transform.position;
        float newZoom = mainCamera.orthographicSize;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            newPosition += (transform.forward * panSpeed);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            newPosition += (transform.forward * -panSpeed);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            newPosition += (transform.right * panSpeed);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            newPosition += (transform.right * -panSpeed);

        newZoom -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed * 100f;
        newZoom = Mathf.Clamp(newZoom, 150f, 300f);

        // for smooth camera movement
        transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime);
        mainCamera.orthographicSize = Mathf.Lerp(mainCamera.orthographicSize, newZoom, Time.deltaTime);
    }
}
