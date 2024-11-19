using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>CameraDetectionZone</c> class represents a collider with the size, shape, and position 
    /// of the field of view of an isometric camera, used to detect the visibility of objects on the scene.
    /// </summary>
    public class CameraDetectionZone : MonoBehaviour
    {
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
                PlayerController.Instance.VisibleUnitsAndStructures++;
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
                PlayerController.Instance.VisibleUnitsAndStructures--;
        }
    }
}