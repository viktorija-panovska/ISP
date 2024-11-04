using Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    public class CameraController : NetworkBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera m_VirtualCamera;
        [SerializeField] private Transform m_FollowTarget;
        [SerializeField] private BoxCollider m_DetectionZone;

        [Header("Movement")]
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



        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            ResizeViewport();
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



        #region Camera Movement

        private void Move()
        {
            Vector3 newPosition = m_FollowTarget.position + Quaternion.Euler(0, m_FollowTarget.eulerAngles.y, 0) * m_Movement * m_MovementSpeed * Time.deltaTime;
            if (newPosition.x < 0 || newPosition.x > Terrain.Instance.UnitsPerSide ||
                newPosition.z < 0 || newPosition.z > Terrain.Instance.UnitsPerSide)
                return;

            m_FollowTarget.position = newPosition;
        }

        private void Rotate()
        {
            m_FollowTarget.eulerAngles = new Vector3(
                m_FollowTarget.eulerAngles.x,
                m_FollowTarget.eulerAngles.y + m_RotationDirection * m_RotationSpeed * Time.deltaTime,
                m_FollowTarget.eulerAngles.z
            );
        }

        private void Zoom()
        {
            m_VirtualCamera.m_Lens.OrthographicSize = Mathf.Clamp(
                m_VirtualCamera.m_Lens.OrthographicSize + m_ZoomDirection * m_ZoomSpeed * Time.deltaTime,
                m_MaxZoomIn,
                m_MaxZoomOut
            );

            ResizeViewport();
        }

        #endregion



        private void ResizeViewport()
        {
            // Calculate the planes from the main camera's view frustum
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

            float sizeX = planes[0].distance + planes[1].distance;
            float sizeY = planes[2].distance + planes[3].distance;
            float sizeZ = planes[4].distance + planes[5].distance;

            m_DetectionZone.size = new Vector3(sizeX, sizeY, sizeZ);
            m_DetectionZone.center = new Vector3(m_DetectionZone.center.x, m_DetectionZone.center.y, sizeZ / 2);
        }


        [ClientRpc]
        public void LookAtClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            m_FollowTarget.transform.position = new Vector3(position.x, 0, position.z);
        }
    }
}