using UnityEngine;

public class UnitProximityDetector : MonoBehaviour
{
    public Unit Unit;

    public void OnMouseEnter()
    {
        Debug.Log("Enter");
        Unit.ToggleHealthBarServerRpc(show: true);
    }

    public void OnMouseExit()
    {
        Unit.ToggleHealthBarServerRpc(show: false);
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Swamp"))
            Unit.KillUnit();

        if (Unit.Team != Teams.Red || Unit.IsFighting) return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            var otherUnit = other.GetComponentInParent<Unit>();

            if (Unit.Team != otherUnit.Team && !otherUnit.IsFighting)
            {
                Unit.StartBattle(otherUnit);
                otherUnit.StartBattle(Unit);
                GameController.Instance.AttackUnit(Unit, otherUnit);
            }
        }
    }
}
