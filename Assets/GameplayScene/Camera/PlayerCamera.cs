using Cinemachine;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>PlayerCamera</c> class controls the behavior of the camera that captures the Gameplay Scene.
    /// </summary>
    public class PlayerCamera : MonoBehaviour
    {
        #region Inspector Fields

        [SerializeField] private CinemachineVirtualCamera m_VirtualCamera;
        [SerializeField] private Transform m_FollowTarget;

        [Header("Movement")]
        [SerializeField] private float m_MovementSpeed;
        [SerializeField] private float m_RotationSpeed;
        [SerializeField] private float m_ZoomSpeed;
        [SerializeField] private float m_MaxZoomIn;
        [SerializeField] private float m_MaxZoomOut;

        #endregion


        #region Class Fields

        private static PlayerCamera m_Instance;
        /// <summary>
        /// Gets a singleton instance of this class.
        /// </summary>
        public static PlayerCamera Instance { get => m_Instance; }

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

        #endregion


        #region Event Functions

        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
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

        #endregion


        #region Camera Movement

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

            CameraDetectionZone.Instance.ResizeDetectionZone();
        }

        #endregion


        /// <summary>
        /// Sets the position of the follow target, and thus sets the point where the camera is looking.
        /// </summary>
        /// <param name="position">The new position of the follow target.</param>
        public void SetCameraLookPosition(Vector3 position) 
            => m_FollowTarget.transform.position = new(position.x, Terrain.Instance.WaterLevel, position.z);

        /// <summary>
        /// Increases the heigth of the follow target to the water level.
        /// </summary>
        public void RaiseCameraToWaterLevel()
            => m_FollowTarget.transform.position = new(m_FollowTarget.transform.position.x, Terrain.Instance.WaterLevel, m_FollowTarget.transform.position.z);
    }
}