using Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>CameraController</c> class is a <c>MonoBehavior</c> that handles the movement of the player's camera.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineVirtualCamera m_VirtualCamera;
        [SerializeField] private Transform m_FollowTarget;
        [SerializeField] private BoxCollider m_DetectionZone;
        [SerializeField] private Camera m_MinimapCamera;

        [Header("Movement")]
        [SerializeField] private float m_MovementSpeed;
        [SerializeField] private float m_RotationSpeed;
        [SerializeField] private float m_ZoomSpeed;
        [SerializeField] private float m_MaxZoomIn;
        [SerializeField] private float m_MaxZoomOut;
        [SerializeField] private float m_CameraHeightDeadzone = 0.1f;


        private static CameraController m_Instance;
        /// <summary>
        /// Gets a singleton instance of this class.
        /// </summary>
        public static CameraController Instance { get => m_Instance; }

        private Vector3 m_MovementDirection;
        /// <summary>
        /// Gets and sets the movement vector of the camera. The vector is normalized when being set.
        /// </summary>
        public Vector3 MovementDirection { get => m_MovementDirection; set { m_MovementDirection = new Vector3(value.x, 0, value.y).normalized; } }

        private int m_RotationDirection;
        /// <summary>
        /// Gets and sets an integer representing the direction of rotation of the camera, with 0 being no rotation, 
        /// 1 being clockwise rotation, and -1 being counter-clockwise rotation
        /// </summary>
        public int RotationDirection { get => m_RotationDirection; set => m_RotationDirection = value; }

        private int m_ZoomDirection;
        /// <summary>
        /// Gets and sets an integer representing the direction of the zoom of the camera, with 0 beinng no zoom,
        /// 1 being zoom out, and -1 being zoom in.
        /// </summary>
        public int ZoomDirection { get => m_ZoomDirection; set => m_ZoomDirection = Mathf.Clamp(value, -1, 1); }


        private void Awake()
        {
            if (m_Instance)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            SetupMinimapCamera();
            ResizeDetectionZone();
        }

        private void Update()
        {
            if (m_MovementDirection != Vector3.zero)
                Move();

            if (m_RotationDirection != 0)
                Rotate();

            if (m_ZoomDirection != 0)
                Zoom();
        }


        /// <summary>
        /// Changes the size of the detection zone collider based on the camera's frustum planes.
        /// </summary>
        private void ResizeDetectionZone()
        {
            // Calculate the planes from the main camera's view frustum
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

            float sizeX = planes[0].distance + planes[1].distance;
            float sizeY = planes[2].distance + planes[3].distance;
            float sizeZ = planes[4].distance + planes[5].distance;

            m_DetectionZone.size = new Vector3(sizeX, sizeY, sizeZ);
            m_DetectionZone.center = new Vector3(m_DetectionZone.center.x, m_DetectionZone.center.y, sizeZ / 2);
        }

        /// <summary>
        /// Sets the position of the follow target, and thus sets the point where the camera is looking.
        /// </summary>
        /// <param name="position">The new position of the follow target.</param>
        public void SetCameraLookPosition(Vector3 position) => m_FollowTarget.transform.position = position;


        #region Main Camera Movement

        /// <summary>
        /// Moves the camera according to the movement vector and speed.
        /// </summary>
        private void Move()
        {
            Vector3 newPosition = m_FollowTarget.position + Quaternion.Euler(0, m_FollowTarget.eulerAngles.y, 0) * m_MovementDirection * m_MovementSpeed * Time.deltaTime;
            if (newPosition.x < 0 || newPosition.x > Terrain.Instance.UnitsPerSide || newPosition.z < 0 || newPosition.z > Terrain.Instance.UnitsPerSide)
                return;

            m_FollowTarget.position = new Vector3(newPosition.x, 0, newPosition.z);
        }

        /// <summary>
        /// Rotates the camera around a point according to the direction and speed of rotation.
        /// </summary>
        private void Rotate()
        {
            m_FollowTarget.eulerAngles = new Vector3(
                m_FollowTarget.eulerAngles.x,
                m_FollowTarget.eulerAngles.y + m_RotationDirection * m_RotationSpeed * Time.deltaTime,
                m_FollowTarget.eulerAngles.z
            );
        }

        /// <summary>
        /// Zooms the camera in and out according to the zoom direction and speed.
        /// </summary>
        private void Zoom()
        {
            m_VirtualCamera.m_Lens.OrthographicSize = Mathf.Clamp(
                m_VirtualCamera.m_Lens.OrthographicSize + m_ZoomDirection * m_ZoomSpeed * Time.deltaTime,
                m_MaxZoomIn,
                m_MaxZoomOut
            );

            ResizeDetectionZone();
        }

        #endregion


        #region Minimap Camera

        public void SetupMinimapCamera()
        {
            m_MinimapCamera.transform.position = new(Terrain.Instance.UnitsPerSide / 2, 300, Terrain.Instance.UnitsPerSide / 2);
            m_MinimapCamera.orthographicSize = Terrain.Instance.UnitsPerSide / 2;
        }

        #endregion

    }
}