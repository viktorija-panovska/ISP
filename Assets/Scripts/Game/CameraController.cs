
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Camera ThisCamera;
    public static float ViewDistance = Screen.width;
    public static int ChunksVisible = Mathf.RoundToInt(ViewDistance / Chunk.Width); 

    private const float movementTime = 50f;
    private const float movementSpeed = 1f;

    private const float rotationTime = 25f;
    private const float rotationSpeed = 1f;

    private const float zoomTime = 50f;
    private const float zoomSpeed = 35f;
    private const float minZoom = 700f;
    private const float maxZoom = 1200f;



    private void Update()
    {
        MoveCamera();
    }


    private void MoveCamera()
    {
        ChangePosition();
        ChangeRotation();
        ChangeZoom();
    }


    private void ChangePosition()
    {
        Vector3 newPosition = gameObject.transform.position;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            newPosition += (transform.forward * movementSpeed);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            newPosition += (transform.forward * -movementSpeed);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            newPosition += (transform.right * movementSpeed);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            newPosition += (transform.right * -movementSpeed);

        if (newPosition != gameObject.transform.position)
            gameObject.transform.position = Vector3.Lerp(gameObject.transform.position, newPosition, Time.deltaTime * movementTime);
    }


    private void ChangeRotation()
    {
        Quaternion newRotation = gameObject.transform.rotation;

        if (Input.GetKey(KeyCode.Q))
            newRotation *= Quaternion.Euler(Vector3.up * rotationSpeed);
        if (Input.GetKey(KeyCode.E))
            newRotation *= Quaternion.Euler(Vector3.up * -rotationSpeed);

        if (newRotation != gameObject.transform.rotation)
            gameObject.transform.rotation = Quaternion.Lerp(gameObject.transform.rotation, newRotation, Time.deltaTime * rotationTime);
    }


    private void ChangeZoom()
    {
        Vector3 newZoom = ThisCamera.transform.localPosition;

        float zoomDirection = Input.mouseScrollDelta.y;

        if (zoomDirection != 0)
            newZoom += new Vector3(0, -zoomDirection, zoomDirection) * zoomSpeed;

        if (newZoom != ThisCamera.transform.localPosition)
        {
            newZoom = new Vector3(newZoom.x, Mathf.Clamp(newZoom.y, minZoom, maxZoom), Mathf.Clamp(newZoom.z, -maxZoom, -minZoom));
            ThisCamera.transform.localPosition = Vector3.Lerp(ThisCamera.transform.localPosition, newZoom, Time.deltaTime * zoomTime);
        }
    }
}
