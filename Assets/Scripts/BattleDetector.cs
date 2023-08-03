using System.Collections.Generic;
using UnityEngine;

public class BattleDetector : MonoBehaviour
{
    public Unit Unit;
    private GameObject target;

    private void OnTriggerEnter(Collider other)
    {
        if (target && Vector3.Distance(Unit.Position, target.transform.position) <= Vector3.Distance(Unit.Position, other.gameObject.transform.position))
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("Unit"))
        {
            var otherUnit = other.gameObject.GetComponent<Unit>();
            
            if (Unit.Team != otherUnit.Team && !otherUnit.IsFighting)
            {
                target = other.gameObject;

                WorldLocation? next = Pathfinding.FollowUnit(new(Unit.Position.x, Unit.Position.z), new(otherUnit.Position.x, otherUnit.Position.z));

                if (next != null)
                    Unit.MoveUnit(new List<WorldLocation> { next.Value });
            }
        }

        if (other.gameObject.layer == LayerMask.NameToLayer("House"))
        {
            var house = other.gameObject.GetComponent<House>();

            if (house.IsAttackable(Unit.Team))
            {
                target = house.gameObject;

                WorldLocation position = house.GetClosestVertex(Unit.Position);

                List<WorldLocation> path = Pathfinding.FindPath(new WorldLocation(Unit.Position.x, Unit.Position.z), position);

                if (path != null && path.Count > 0)
                    Unit.MoveUnit(path);
            }
        }
    }
}
