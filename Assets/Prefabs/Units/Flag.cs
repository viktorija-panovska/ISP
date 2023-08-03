using UnityEngine;

public class Flag : MonoBehaviour
{
    public Teams Team;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Enter");
        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            Debug.Log("Unit collided");

            Unit unit = other.gameObject.GetComponent<Unit>();

            if (unit.Team != Team)
                return;

            if (!GameController.Instance.HasLeader(unit.Team))
                unit.MakeLeader();

            GameController.Instance.RemoveFlag(Team);

            // end follow
        }
    }
}
