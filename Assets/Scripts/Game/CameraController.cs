using UnityEngine;
using Unity.Netcode;


public class CameraController : NetworkBehaviour
{
    public GameObject CameraRig;
    public Camera PlayerCamera;

    public static float ViewDistance = Screen.width;
    public static int ChunksVisible = Mathf.CeilToInt(ViewDistance / Chunk.Width); 

    private const float movementTime = 50f;
    private const float movementSpeed = 1f;

    private const float zoomTime = 50f;
    private const float zoomSpeed = 35f;
    private const float minZoom = 700f;
    private const float maxZoom = 1200f;


    public override void OnNetworkSpawn()
    {
        if (!IsOwner) return;

        // Set camera
        if (OwnerClientId == 0)
            CameraRig.transform.position = new Vector3(0, CameraRig.transform.position.y, 0);
        else
            CameraRig.transform.position = new Vector3(WorldMap.Width, CameraRig.transform.position.y, WorldMap.Width);

        WorldMap.Instance.DrawVisibleMap(CameraRig.transform.position);
    }


    private void Update()
    {
        if (ChangePosition() || ChangeZoom())
            RedrawMap();
    }


    private bool ChangePosition()
    {
        Vector3 newPosition = CameraRig.transform.position;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            newPosition += (transform.forward * movementSpeed);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            newPosition += (transform.forward * -movementSpeed);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            newPosition += (transform.right * movementSpeed);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            newPosition += (transform.right * -movementSpeed);

        if (newPosition != CameraRig.transform.position)
        {
            CameraRig.transform.position = Vector3.Lerp(CameraRig.transform.position, newPosition, Time.deltaTime * movementTime);
            return true;
        }

        return false;
    }


    private bool ChangeZoom()
    {
        Vector3 newZoom = PlayerCamera.transform.localPosition;

        float zoomDirection = Input.mouseScrollDelta.y;

        if (zoomDirection != 0)
            newZoom += new Vector3(0, -zoomDirection, zoomDirection) * zoomSpeed;

        if (newZoom != PlayerCamera.transform.localPosition)
        {
            newZoom = new Vector3(newZoom.x, Mathf.Clamp(newZoom.y, minZoom, maxZoom), Mathf.Clamp(newZoom.z, -maxZoom, -minZoom));
            PlayerCamera.transform.localPosition = Vector3.Lerp(PlayerCamera.transform.localPosition, newZoom, Time.deltaTime * zoomTime);
            return true;
        }

        return false;
    }


    private void RedrawMap()
    {
        if (Physics.Raycast(PlayerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, 0, Screen.height / 2)), out RaycastHit hitInfo, Mathf.Infinity)) 
            WorldMap.Instance.DrawVisibleMap(hitInfo.point);
    }
}
