using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting.Dependencies.NCalc;
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
    private GameObject viewZone;

    public Camera MainCamera { get => mainCameraRig.GetComponentInChildren<Camera>(); }
    public Camera MapCamera { get => mapCameraRig.GetComponentInChildren<Camera>(); }

    private (float width, float height) mainCameraDimensions;
    private (float width, float height) mapCameraDimensions;

    // bottom left, bottom right, top left, top right
    private Vector3[] screenCorners;

    private bool isMapCamera;



    public void Awake()
    {
        mainCameraRig = Instantiate(MainCameraRigPrefab);
        mapCameraRig = Instantiate(MapCameraRigPrefab);

        mapCameraDimensions = (2 * MapCamera.orthographicSize * MapCamera.aspect, 2 * MapCamera.orthographicSize);
        mapCameraRig.transform.position = new Vector3(mapCameraDimensions.width / 2, mapCameraRig.transform.position.y, mapCameraDimensions.height / 2);

        screenCorners = new Vector3[] {
            new Vector3(0, 0), new Vector3(0, MainCamera.pixelHeight),
            new Vector3(MainCamera.pixelWidth, 0), new Vector3(MainCamera.pixelWidth, MainCamera.pixelHeight)
        };

    }


    public void Update()
    {
        if (mainCameraDimensions == (0, 0))
        {
            ComputeViewBorders();
            RedrawMap(mainCameraRig.transform.position, mainCameraDimensions);
            return;
        }

        if (!isMapCamera && (ChangePosition(mainCameraRig, (100, WorldMap.WIDTH - 100), (100, WorldMap.WIDTH - 100), MOVEMENT_SPEED) || ChangeZoom()))
            RedrawMap(mainCameraRig.transform.position, mainCameraDimensions);

        if (isMapCamera && ChangePosition(mapCameraRig, (mapCameraDimensions.width / 2, WorldMap.WIDTH - (mapCameraDimensions.width / 2)),
            (mapCameraDimensions.height / 2, WorldMap.WIDTH - (mapCameraDimensions.height / 2)), MAP_SPEED))
            RedrawMap(mapCameraRig.transform.position, mapCameraDimensions);

        if (isMapCamera && Input.GetKey(KeyCode.Space))
            TeleportToLocation(mapCameraRig.transform.position);
    }


    public void ResetCamera(WorldLocation cameraStart)
    {
        if (isMapCamera)
            SwitchCameras(false);

        SetLocation(cameraStart);

        // reset zoom
    }



    #region Setup

    public void SetLocation(WorldLocation location)
    {
        mainCameraRig.transform.position = new Vector3(location.X, mainCameraRig.transform.position.y, location.Z);
        RedrawMap(mainCameraRig.transform.position, mainCameraDimensions);
    }

    public void SetGameHUD(GameHUD gameHUD) => this.gameHUD = gameHUD;

    public void SetViewZone(GameObject viewZone)
    {
        this.viewZone = viewZone;
    }

    #endregion



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

        newPosition.x = Mathf.Clamp(newPosition.x, horizontal_bounds.min, horizontal_bounds.max);
        newPosition.z = Mathf.Clamp(newPosition.z, vertical_bounds.min, vertical_bounds.max);

        if (newPosition != rig.transform.position)
        {
            rig.transform.position = Vector3.Lerp(rig.transform.position, newPosition, Time.deltaTime * MOVEMENT_TIME);
            viewZone.transform.position = Vector3.Lerp(rig.transform.position, newPosition, Time.deltaTime * MOVEMENT_TIME);
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
            ComputeViewBorders();
            return true;
        }

        return false;
    }

    #endregion



    #region Map

    private void ComputeViewBorders()
    {
        Vector3[] viewCorners = new Vector3[4];

        for (int i = 0; i < screenCorners.Length; ++i)
            if (Physics.Raycast(MainCamera.ScreenPointToRay(screenCorners[i]), out RaycastHit hitInfo, Mathf.Infinity))
                viewCorners[i] = hitInfo.point;

        mainCameraDimensions = (Mathf.Abs(viewCorners[1].x - viewCorners[3].x), Mathf.Abs(viewCorners[0].z - viewCorners[1].z));
        ResizeViewZone(viewCorners);
    }


    private void RedrawMap(Vector3 cameraPosition, (float width, float height) viewDimensions)
    {
        float chunksVisibleX = Mathf.CeilToInt(viewDimensions.width / Chunk.WIDTH);
        float chunksVisibleZ = Mathf.CeilToInt(viewDimensions.height / Chunk.WIDTH);

        // Reset last visible chunks
        foreach ((int x, int z) in lastVisibleChunks)
            WorldMap.Instance.GetChunk(x, z).SetVisibility(false);
        lastVisibleChunks = new();

        (int chunk_x, int chunk_z) = WorldMap.Instance.GetChunkIndex(cameraPosition.x, cameraPosition.z);

        int offsetX = Mathf.CeilToInt(chunksVisibleX / 2);
        int offsetZ = Mathf.CeilToInt(chunksVisibleZ / 2);

        for (int zOffset = -offsetZ; zOffset <= offsetZ; ++zOffset)
        {
            for (int xOffset = -offsetX; xOffset <= offsetX; ++xOffset)
            {
                (int x, int z) newChunk = (chunk_x + xOffset, chunk_z + zOffset);

                if (newChunk.x >= 0 && newChunk.z >= 0 && newChunk.x < WorldMap.CHUNK_NUMBER && newChunk.z < WorldMap.CHUNK_NUMBER)
                {
                    Chunk chunk = WorldMap.Instance.GetChunk(newChunk.x, newChunk.z);
                    chunk.SetVisibility(true);
                    lastVisibleChunks.Add(newChunk);
                }
            }
        }
    }
    

    private void ResizeViewZone(Vector3[] corners)
    {
        int height = Chunk.MAX_HEIGHT;
        float centerBoxLength = 0;

        BoxCollider[] colliders = viewZone.GetComponentsInChildren<BoxCollider>();

        foreach (var collider in colliders)
        {
            float length = 0;
            float width = Mathf.Abs(corners[0].z - corners[2].z);

            //if (collider.name == "CenterBox")
            //{
            //    length = Mathf.Abs(corners[3].x - corners[2].x);
            //    centerBoxLength = length;
            //}
            //else if (collider.name == "LeftBox")
            //{
            //    length = Mathf.Abs(corners[3].x - corners[0].x);
            //    collider.transform.position = new Vector3(-(centerBoxLength / 2 + length / 2), 0, 0);
            //}
            //else if (collider.name == "RightBox")
            //{
            //    length = Mathf.Abs(corners[4].x - corners[1].x);
            //    collider.transform.position = new Vector3(centerBoxLength / 2 + length / 2, 0, 0);
            //}

            Vector3 prevSize = collider.bounds.size;
            Vector3 prevScale = collider.transform.localScale;
            collider.transform.localScale = new Vector3(prevScale.x, height * prevScale.y / prevSize.y, prevScale.z);

            if (Physics.Raycast(MainCamera.ScreenPointToRay(new Vector2(Screen.width / 2, Screen.width / 2)), out RaycastHit hitInfo, Mathf.Infinity))
                collider.transform.position = hitInfo.point;
                
        }
    }

    #endregion



    #region Minimap

    public void SwitchCameras(bool isMapCamera)
    {
        this.isMapCamera = isMapCamera;

        if (isMapCamera)
        {
            mapCameraRig.transform.position = mainCameraRig.transform.position;
            RedrawMap(mapCameraRig.transform.position, mapCameraDimensions);
        }
        else
        {
            RedrawMap(mainCameraRig.transform.position, mainCameraDimensions);
        }
    }


    public void TeleportToLocation(Vector3 destination)
    {
        mainCameraRig.transform.position = destination;
        gameHUD.ToggleMap();
    }

    #endregion
}
