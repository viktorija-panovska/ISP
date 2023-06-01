using System.Collections.Generic;
using UnityEngine;


public class CameraController : MonoBehaviour
{
    private GameHUD gameHUD;
    private List<(int, int)> lastVisibleChunks = new();

    private const float MOVEMENT_TIME = 50f;
    private const float MOVEMENT_SPEED = 2f;
    private const float MAP_SPEED = 5f;

    private const float ZOOM_TIME = 50f;
    private const float ZOOM_SPEED = 35f;
    private const float MIN_ZOOM = 700f;
    private const float MAX_ZOOM = 1200f;

    public GameObject MainCameraRigPrefab;
    public GameObject MapCameraRigPrefab;

    private GameObject mainCameraRig;
    private GameObject mapCameraRig;

    public Camera MainCamera { get => mainCameraRig.GetComponentInChildren<Camera>(); }
    public Camera MapCamera { get => mapCameraRig.GetComponentInChildren<Camera>(); }

    private (float width, float height) MainCameraDimensions { get => (Screen.width, Screen.height); }
    private (float width, float height) MapCameraDimensions { get => (2 * MapCamera.orthographicSize * MapCamera.aspect, 2 * MapCamera.orthographicSize); }

    private bool isMapCamera;


    public void Awake()
    {
        mainCameraRig = Instantiate(MainCameraRigPrefab);
        mapCameraRig = Instantiate(MapCameraRigPrefab);

        mapCameraRig.transform.position = new Vector3(MapCameraDimensions.width / 2, mapCameraRig.transform.position.y, MapCameraDimensions.height / 2);

        // Set camera
        mainCameraRig.transform.position = new Vector3(0, mainCameraRig.transform.position.y, 0);

        DrawVisibleMap(mainCameraRig.transform.position, MainCameraDimensions.width);
    }

    public void Update()
    {
        if (!isMapCamera && (ChangePosition(mainCameraRig, (0,0), (0,0), MOVEMENT_SPEED) || ChangeZoom()))
            RedrawMap(mainCameraRig.transform.position, MainCameraDimensions, MainCameraDimensions.width);

        if (isMapCamera && ChangePosition(mapCameraRig, 
            (MapCameraDimensions.width / 2, WorldMap.WIDTH - (MapCameraDimensions.width / 2)), 
            (MapCameraDimensions.height / 2, WorldMap.WIDTH - (MapCameraDimensions.height / 2)), 
            MAP_SPEED))
            RedrawMap(mapCameraRig.transform.position, MapCameraDimensions, MapCameraDimensions.width);

        if (isMapCamera && Input.GetKey(KeyCode.Space))
            TeleportToLocation(mapCameraRig.transform.position);
    }

    public void SetGameHUD(GameHUD gameHUD)
    {
        this.gameHUD = gameHUD;
    }



    #region Movement
    private bool ChangePosition(GameObject rig, (float min, float max) horizontal_bounds, (float min, float max) vertical_bounds, float speed)
    {
        Vector3 newPosition = rig.transform.position;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            newPosition += (transform.forward * speed);
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            newPosition += (transform.forward * -speed);
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            newPosition += (transform.right * speed);
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            newPosition += (transform.right * -speed);

        if (horizontal_bounds != (0,0) && vertical_bounds != (0,0))
        {
            newPosition.x = Mathf.Clamp(newPosition.x, horizontal_bounds.min, horizontal_bounds.max);
            newPosition.z = Mathf.Clamp(newPosition.z, vertical_bounds.min, vertical_bounds.max);
        }

        if (newPosition != rig.transform.position)
        {
            rig.transform.position = Vector3.Lerp(rig.transform.position, newPosition, Time.deltaTime * MOVEMENT_TIME);
            return true;
        }

        return false;
    }


    private bool ChangeZoom()
    {
        Vector3 newZoom = MainCamera.transform.localPosition;

        float zoomDirection = Input.mouseScrollDelta.y;

        if (zoomDirection != 0)
            newZoom += new Vector3(0, -zoomDirection, zoomDirection) * ZOOM_SPEED;

        if (newZoom != MainCamera.transform.localPosition)
        {
            newZoom = new Vector3(newZoom.x, Mathf.Clamp(newZoom.y, MIN_ZOOM, MAX_ZOOM), Mathf.Clamp(newZoom.z, -MAX_ZOOM, -MIN_ZOOM));
            MainCamera.transform.localPosition = Vector3.Lerp(MainCamera.transform.localPosition, newZoom, Time.deltaTime * ZOOM_TIME);
            return true;
        }

        return false;
    }
    #endregion



    #region Map
    private void RedrawMap(Vector3 center, (float width, float height) dimensions, float viewDistance)
    {
        float topZ = center.z - (dimensions.height / 2);
        float bottomZ = center.z + (dimensions.height / 2);
        float leftX = center.x - (dimensions.width / 2);
        float rightX = center.x + (dimensions.width / 2);

        //if (topZ % viewDistance == 0 || bottomZ % viewDistance == 0 || leftX % viewDistance == 0 || rightX % viewDistance == 0)
        DrawVisibleMap(center, viewDistance);
    }


    // Draw Map
    private void DrawVisibleMap(Vector3 cameraPosition, float viewDistance)
    {
        int chunksVisible = Mathf.CeilToInt(viewDistance / Chunk.WIDTH);

        foreach ((int x, int z) in lastVisibleChunks)
            WorldMap.Instance.GetChunk(x, z).SetVisibility(false);

        lastVisibleChunks = new();

        (int chunk_x, int chunk_z) = WorldMap.Instance.GetChunkIndex(cameraPosition.x, cameraPosition.z);
  
        int offset = Mathf.FloorToInt(chunksVisible / 2);

        for (int zOffset = -offset; zOffset <= offset; ++zOffset)
        {
            for (int xOffset = -offset; xOffset <= offset; ++xOffset)
            {
                (int x, int z) newChunk = (chunk_x + xOffset, chunk_z + zOffset);

                if (newChunk.x >= 0 && newChunk.z >= 0 &&
                    newChunk.x < WorldMap.CHUNK_NUMBER && newChunk.z < WorldMap.CHUNK_NUMBER)
                {
                    if (WorldMap.Instance.GetChunk(newChunk.x, newChunk.z).DistanceFromPoint(cameraPosition) <= viewDistance)
                    {
                        WorldMap.Instance.GetChunk(newChunk.x, newChunk.z).SetVisibility(true);
                        lastVisibleChunks.Add(newChunk);
                    }
                }
            }
        }
    }
    #endregion



    #region Map

    public void SwitchCameras(bool isMapCamera)
    {
        this.isMapCamera = isMapCamera;

        if (isMapCamera)
        {
            mapCameraRig.transform.position = mainCameraRig.transform.position;
            RedrawMap(mapCameraRig.transform.position, MapCameraDimensions, MapCameraDimensions.width);
        }
        else
        {
            RedrawMap(mainCameraRig.transform.position, MainCameraDimensions, MainCameraDimensions.width);
        }
    }


    public void TeleportToLocation(Vector3 destination)
    {
        mainCameraRig.transform.position = destination;
        gameHUD.ToggleMap();
    }

    #endregion
}
