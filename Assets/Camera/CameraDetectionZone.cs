using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>CameraDetectionZone</c> class represents the field of view of an isometric camera, used to detect visible objects.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CameraDetectionZone : MonoBehaviour
    {
        #region Class Fields

        private static CameraDetectionZone m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static CameraDetectionZone Instance { get => m_Instance; }

        /// <summary>
        /// The instance IDs of the objects of the player's team that are in the camera's field of view.
        /// </summary>
        private readonly HashSet<int> m_VisibleTeamObjectIds = new();
        /// <summary>
        /// The number of objects of the player's team that are in the camera's field of view.
        /// </summary>
        public int VisibleObjectsAmount { get =>  m_VisibleTeamObjectIds.Count; }

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

        private void OnTriggerEnter(Collider other)
        {
            // Handle chunk spawning
            TerrainChunk chunk = other.GetComponent<TerrainChunk>();
            if (chunk)
            {
                chunk.SetVisibility(true);
                return;
            }

            // Count visible units and structures
            Team team = PlayerController.Instance.Team;

            if (team == Team.RED && other.gameObject.layer == LayerData.TeamLayers[(int)Team.RED] ||
                team == Team.BLUE && other.gameObject.layer == LayerData.TeamLayers[(int)Team.BLUE])
                m_VisibleTeamObjectIds.Add(other.gameObject.GetInstanceID());
        }

        private void OnTriggerExit(Collider other)
        {
            // Handle chunk despawning
            TerrainChunk chunk = other.GetComponent<TerrainChunk>();
            if (chunk)
            {
                chunk.SetVisibility(false);
                return;
            }

            // Count visible units and structures
            Team team = PlayerController.Instance.Team;

            if (team == Team.RED && other.gameObject.layer == LayerData.TeamLayers[(int)Team.RED] ||
                team == Team.BLUE && other.gameObject.layer == LayerData.TeamLayers[(int)Team.BLUE])
                m_VisibleTeamObjectIds.Remove(other.gameObject.GetInstanceID());
        }

        #endregion


        #region Visible Objects

        /// <summary>
        /// Removes the object with the given ID from the list of visible objects.
        /// </summary>
        /// <remarks>Used when an object is despawned.</remarks>
        /// <param name="objectId">The instance ID of the object that should be removed.</param>
        public void RemoveVisibleObject(int objectId)
        {
            if (!m_VisibleTeamObjectIds.Contains(objectId))
                return;

            m_VisibleTeamObjectIds.Remove(objectId);
        }

        #endregion
    }
}