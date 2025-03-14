using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>CameraDetectionZone</c> class represents the field of view of an isometric camera, used to detect visible objects.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CameraDetectionZone : MonoBehaviour
    {
        [Tooltip("The camera this detection zone is parented to.")]
        [SerializeField] private Camera m_Camera;
        
        private static CameraDetectionZone m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static CameraDetectionZone Instance { get => m_Instance; }

        /// <summary>
        /// The instance IDs of the objects of the player's team that are in the camera's field of view.
        /// </summary>
        private readonly HashSet<ulong> m_VisibleTeamObjectIds = new() { 1 };
        /// <summary>
        /// The number of objects of the player's team that are in the camera's field of view.
        /// </summary>
        public int VisibleObjectsAmount { get =>  m_VisibleTeamObjectIds.Count; }


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

        private void Start() => ResizeDetectionZone();

        private void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponent<Renderer>() || other.gameObject.layer == LayerData.MinimapLayer)
                return;

            other.GetComponent<Renderer>().enabled = true;

            // Count visible units and structures
            Faction faction = PlayerController.Instance.Faction;

            // using network object ID instead of instance ID because it needs to be the same
            // when it is passed from server to client

            if (faction == Faction.RED && other.gameObject.layer == LayerData.FactionLayers[(int)Faction.RED] ||
                faction == Faction.BLUE && other.gameObject.layer == LayerData.FactionLayers[(int)Faction.BLUE])
                m_VisibleTeamObjectIds.Add(other.GetComponent<NetworkObject>().NetworkObjectId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.GetComponent<Renderer>() || other.gameObject.layer == LayerData.MinimapLayer)
                return;

            other.GetComponent<Renderer>().enabled = false;

            Faction faction = PlayerController.Instance.Faction;

            if (faction == Faction.RED && other.gameObject.layer == LayerData.FactionLayers[(int)Faction.RED] ||
                faction == Faction.BLUE && other.gameObject.layer == LayerData.FactionLayers[(int)Faction.BLUE])
                m_VisibleTeamObjectIds.Remove(other.GetComponent<NetworkObject>().NetworkObjectId);
        }

        #endregion


        /// <summary>
        /// Changes the size of the detection zone collider based on the frustum of the camera.
        /// </summary>
        public void ResizeDetectionZone()
        {
            // Calculate the planes from the camera's view frustum
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(m_Camera);

            float sizeX = planes[0].distance + planes[1].distance;
            float sizeY = planes[2].distance + planes[3].distance;
            float sizeZ = planes[4].distance + planes[5].distance;

            BoxCollider collider = GetComponent<BoxCollider>();
            collider.size = new Vector3(sizeX, sizeY, sizeZ);
        }


        #region Visible Objects

        /// <summary>
        /// Removes the object with the given ID from the list of visible objects.
        /// </summary>
        /// <remarks>Used when an object is despawned.</remarks>
        /// <param name="objectId">The Network Object ID of the object that should be removed.</param>
        public void RemoveVisibleObject(ulong objectId)
        {
            if (!m_VisibleTeamObjectIds.Contains(objectId))
                return;

            m_VisibleTeamObjectIds.Remove(objectId);
        }

        #endregion
    }
}