using UnityEngine;

namespace Populous
{
    public class CameraDetectionZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            Team team = PlayerController.Instance.Team;

            if (team == Team.RED && other.gameObject.layer == LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)Team.RED]) ||
                team == Team.BLUE && other.gameObject.layer == LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)Team.BLUE]))
                PlayerController.Instance.VisibleUnitsAndStructures++;
        }

        private void OnTriggerExit(Collider other)
        {
            Team team = PlayerController.Instance.Team;

            if (team == Team.RED && other.gameObject.layer == LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)Team.RED]) ||
                team == Team.BLUE && other.gameObject.layer == LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)Team.BLUE]))
                PlayerController.Instance.VisibleUnitsAndStructures--;
        }
    }
}