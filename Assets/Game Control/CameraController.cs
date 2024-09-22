using Cinemachine;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera m_VirtualCamera;
    [SerializeField] private Transform m_FollowTarget;
    [SerializeField] private float m_MovementSpeed;
    [SerializeField] private float m_RotationSpeed;
    [SerializeField] private float m_ZoomSpeed;
    [SerializeField] private float m_MaxZoomIn;
    [SerializeField] private float m_MaxZoomOut;

    private static CameraController m_Instance;
    /// <summary>
    /// Gets an instance of the class.
    /// </summary>
    public static CameraController Instance { get => m_Instance; }

    private Vector3 m_Movement;
    /// <summary>
    /// Gets and sets the movement vector of the camera.
    /// </summary>
    public Vector3 Movement { get => m_Movement; set { m_Movement = new Vector3(value.x, 0, value.y).normalized; } }

    private int m_RotationDirection;
    /// <summary>
    /// Gets and sets an integer representing the direction of rotation of the camera, with 0 being no rotation, 
    /// 1 being clockwise rotation, and -1 being counter-clockwise rotation
    /// </summary>
    public int RotationDirection { get => m_RotationDirection; set => m_RotationDirection = value; }

    private int m_ZoomDirection;            // -1 zoom in, 1 zoom out, 0 no zoom
    /// <summary>
    /// Gets and sets an integer representing the direction of the zoom of the camera, with 0 beinng no zoom,
    /// 1 being zoom out, and -1 being zoom in.
    /// </summary>
    public int ZoomDirection { get => m_ZoomDirection; set => m_ZoomDirection = Mathf.Clamp(value, -1, 1); }

    private List<TerrainChunk> m_VisibleTerrainChunks = new();



    #region MonoBehavior

    private void Awake()
    {
        if (m_Instance != null)
            Destroy(gameObject);

        m_Instance = this;
    }

    private void Update()
    {
        if (m_Movement != Vector3.zero)
            Move();

        if (m_RotationDirection != 0)
            Rotate();

        if (m_ZoomDirection != 0)
            Zoom();
    }

    #endregion


    /// <summary>
    /// Finds the terrain chunks which are at least partially captured by the player camera and makes them visible,
    /// making the rest of the terrain chunks invisible.
    /// </summary>
    public void UpdateVisibleTerrainChunks()
    {
        foreach (TerrainChunk chunk in m_VisibleTerrainChunks)
            chunk.SetVisibility(false);
        m_VisibleTerrainChunks = new();

        int leftIndex = Terrain.Instance.ChunksPerSide, bottomIndex = Terrain.Instance.ChunksPerSide;
        int rightIndex = -1, topIndex = -1;
        RaycastHit hit;

        // bottom left
        if (Physics.Raycast(Camera.main.ViewportPointToRay(new Vector3(0, 0, 0)), out hit))
        {
            (int x, int z) = Terrain.Instance.GetChunkIndex(hit.point);
            leftIndex = Mathf.Min(leftIndex, x);
            bottomIndex = Mathf.Min(bottomIndex, z);
        }

        // top right
        if (Physics.Raycast(Camera.main.ViewportPointToRay(new Vector3(1, 1, 0)), out hit))
        {
            (int x, int z) = Terrain.Instance.GetChunkIndex(hit.point);
            rightIndex = Mathf.Max(rightIndex, x);
            topIndex = Mathf.Max(rightIndex, z);
        }

        // bottom right
        if (Physics.Raycast(Camera.main.ViewportPointToRay(new Vector3(1, 0, 0)), out hit))
        {
            (int x, int z) = Terrain.Instance.GetChunkIndex(hit.point);
            rightIndex = Mathf.Max(rightIndex, x);
            bottomIndex = Mathf.Min(bottomIndex, z);
        }

        // top left
        if (Physics.Raycast(Camera.main.ViewportPointToRay(new Vector3(0, 1, 0)), out hit))
        {
            (int x, int z) = Terrain.Instance.GetChunkIndex(hit.point);
            leftIndex = Mathf.Min(leftIndex, x);
            topIndex = Mathf.Max(topIndex, z);
        }

        for (int z = Mathf.Clamp(bottomIndex, 0, Terrain.Instance.ChunksPerSide - 1);
             z <= Mathf.Clamp(topIndex, 0, Terrain.Instance.ChunksPerSide - 1); 
             ++z)
        {
            for (int x = Mathf.Clamp(leftIndex, 0, Terrain.Instance.ChunksPerSide - 1); 
                 x <= Mathf.Clamp(rightIndex, 0, Terrain.Instance.ChunksPerSide - 1); 
                 ++x)
            {
                TerrainChunk chunk = Terrain.Instance.GetChunkByIndex((x, z));
                chunk.SetVisibility(true);
                m_VisibleTerrainChunks.Add(chunk);
            }
        }
    }


    #region Camera Movement

    private void Move()
    {
        Vector3 newPosition = m_FollowTarget.position + Quaternion.Euler(0, m_FollowTarget.eulerAngles.y, 0) * m_Movement * m_MovementSpeed * Time.deltaTime;
        if (newPosition.x < 0 || newPosition.x > Terrain.Instance.UnitsPerSide ||
            newPosition.z < 0 || newPosition.z > Terrain.Instance.UnitsPerSide)
            return;

        m_FollowTarget.position = newPosition;
        UpdateVisibleTerrainChunks();
    }

    private void Rotate()
    {
        m_FollowTarget.eulerAngles = new Vector3(
            m_FollowTarget.eulerAngles.x,
            m_FollowTarget.eulerAngles.y + m_RotationDirection * m_RotationSpeed * Time.deltaTime,
            m_FollowTarget.eulerAngles.z
        );
        UpdateVisibleTerrainChunks();
    }

    private void Zoom()
    {        
        m_VirtualCamera.m_Lens.OrthographicSize = Mathf.Clamp(
            m_VirtualCamera.m_Lens.OrthographicSize + m_ZoomDirection * m_ZoomSpeed * Time.deltaTime, 
            m_MaxZoomIn, 
            m_MaxZoomOut
        );
        UpdateVisibleTerrainChunks();
    }

    #endregion
}