using UnityEngine;

namespace Populous
{
    public class Flag : Structure
    {
        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();

            if (unit == null || unit.CurrentState != UnitState.GO_TO_FLAG || unit.Team != m_Team || unit.IsKnight)
                return;

            unit.FlagReached();

            if (UnitManager.Instance.GetLeader(m_Team) != null)
                return;

            UnitManager.Instance.SetLeader(unit, unit.Team);
        }
    }
}