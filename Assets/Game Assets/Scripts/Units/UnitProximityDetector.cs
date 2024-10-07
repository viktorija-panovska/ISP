using UnityEngine;

public class UnitProximityDetector : MonoBehaviour
{
    public OldUnit Unit;

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Swamp"))
            Unit.KillUnit();

        if (Unit.Team != Team.RED || Unit.IsFighting) return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            var otherUnit = other.GetComponentInParent<OldUnit>();

            if (Unit.Team != otherUnit.Team && !otherUnit.IsFighting)
            {
                Unit.StartBattle(otherUnit);
                otherUnit.StartBattle(Unit);
                OldGameController.Instance.AttackUnit(Unit, otherUnit);
            }
        }
    }
}
