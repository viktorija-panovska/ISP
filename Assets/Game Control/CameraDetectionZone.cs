using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>CameraDetectionZone</c> class represents a collider with the size, shape, and position 
    /// of the field of view of an isometric camera, used to detect the visibility of objects on the scene.
    /// </summary>
    public class CameraDetectionZone : MonoBehaviour
    {
        private static CameraDetectionZone m_Instance;
        public static CameraDetectionZone Instance { get => m_Instance; }

        private readonly HashSet<int> m_VisibleTeamObjectIds = new();
        public int VisibleTeamObjects { get =>  m_VisibleTeamObjectIds.Count; }


        private void Awake()
        {
            if (m_Instance)
                Destroy(gameObject);

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


        public void RemoveVisibleObject(int objectId)
        {
            if (!m_VisibleTeamObjectIds.Contains(objectId))
                return;

            m_VisibleTeamObjectIds.Remove(objectId);
        }
    }
}