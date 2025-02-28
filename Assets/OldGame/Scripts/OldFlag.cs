using Unity.Netcode;
using UnityEngine;

public enum Team
{
    RED,
    BLUE,
    NONE
}

public class OldFlag : MonoBehaviour
{
    public Team Team;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            OldUnit unit = other.gameObject.GetComponent<OldUnit>();

            if (unit.Team != Team)
                return;

            if (!OldGameController.Instance.HasLeader(unit.Team))
                unit.MakeLeader();

            OldGameController.Instance.RemoveFlag(Team);

            OldGameController.Instance.EndFollowForAll(Team);
        }
    }
}
