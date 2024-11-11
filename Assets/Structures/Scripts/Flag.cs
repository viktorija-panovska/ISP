using UnityEngine;

namespace Populous
{
    public class Flag : Structure
    {
        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();

            if (unit == null || unit.Behavior != UnitBehavior.GO_TO_SYMBOL || unit.Team != m_Team || unit.Class == UnitClass.KNIGHT)
                return;

            unit.SymbolReached();

            if (UnitManager.Instance.GetLeader(m_Team) != null)
                return;

            UnitManager.Instance.SetLeader(unit, unit.Team);
        }
    }
}