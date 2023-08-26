using UnityEngine;
using System.Collections.Generic;

public class UnitEnemyDetector : MonoBehaviour
{
    public Unit Unit;
    private GameObject target;

    private void OnTriggerEnter(Collider other)
    {
        if (target && Vector3.Distance(Unit.Position, target.transform.position) <= Vector3.Distance(Unit.Position, other.gameObject.transform.position))
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            var otherUnit = other.GetComponentInParent<Unit>();

            if (Unit.Team != otherUnit.Team && !otherUnit.IsFighting && otherUnit.ChasedBy == null)
            {
                otherUnit.ChasedBy = Unit;
                target = otherUnit.gameObject;
                Unit.FollowUnit(otherUnit);
            }
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("House"))
        {
            var house = other.gameObject.GetComponent<House>();

            if (house.IsAttackable(Unit.Team))
            {
                target = house.gameObject;

                WorldLocation position = Helpers.GetClosestVertex(Unit.Position, house.Vertices).location;

                List<WorldLocation> path = Pathfinding.FindPath(new WorldLocation(Unit.Position.x, Unit.Position.z), position);

                if (path != null && path.Count > 0)
                    Unit.MoveAlongPath(path);
            }
        }
    }
}
