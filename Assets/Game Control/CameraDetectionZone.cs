using UnityEngine;

namespace Populous
{
    public class CameraDetectionZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Terrain"))
            {
                SetChunkVisibility(other.gameObject.name, true);
                return;
            }

            Team team = PlayerController.Instance.Team;

            if (team == Team.RED && other.gameObject.layer == LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)Team.RED]) ||
                team == Team.BLUE && other.gameObject.layer == LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)Team.BLUE]))
                PlayerController.Instance.VisibleUnitsAndStructures++;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Terrain"))
            {
                SetChunkVisibility(other.gameObject.name, false);
                return;
            }

            Team team = PlayerController.Instance.Team;

            if (team == Team.RED && other.gameObject.layer == LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)Team.RED]) ||
                team == Team.BLUE && other.gameObject.layer == LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)Team.BLUE]))
                PlayerController.Instance.VisibleUnitsAndStructures--;
        }

        private void SetChunkVisibility(string chunkName, bool visible)
        {
            string[] name = chunkName.Split(' ');

            if (int.TryParse(name[1], out int x) && int.TryParse(name[2], out int z))
                Terrain.Instance.GetChunkByIndex((x, z)).SetVisibility(visible);
        }
    }
}