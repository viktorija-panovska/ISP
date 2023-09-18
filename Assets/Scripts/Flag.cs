using Unity.Netcode;
using UnityEngine;

public class Flag : MonoBehaviour
{
    public Teams Team;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            Unit unit = other.gameObject.GetComponent<Unit>();

            if (unit.Team != Team)
                return;

            if (!GameController.Instance.HasLeader(unit.Team))
                unit.MakeLeader();

            GameController.Instance.RemoveFlag(Team);

            GameController.Instance.EndFollowForAll(Team);
        }
    }
}
