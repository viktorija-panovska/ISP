using UnityEngine;

namespace Populous
{
    public class UnitCloseRangeDetector : MonoBehaviour
    {
        private Unit m_Unit;
        private Team m_Team;
        private Team m_EnemyTeam;

        public void Setup(Unit unit)
        {
            m_Unit = unit;
            m_Team = unit.Team;
            m_EnemyTeam = unit.Team == Team.RED ? Team.BLUE : Team.RED;
        }

        private void OnTriggerEnter(Collider other)
        {
            Unit otherUnit = other.GetComponent<Unit>();

            if (!otherUnit || (m_Unit.CurrentState == UnitState.GATHER && (otherUnit.Team != m_Team || m_Unit.Strength < otherUnit.Strength)) ||
                (m_Unit.CurrentState == UnitState.BATTLE && (m_Team == Team.BLUE || otherUnit.Team != m_EnemyTeam || otherUnit.IsBattling)))              
                return;

            if (m_Unit.CurrentState == UnitState.BATTLE)
                UnitManager.Instance.StartBattle(m_Unit, other.GetComponent<Unit>());

            if (m_Unit.CurrentState == UnitState.GATHER)
            {
                m_Unit.AbsorbUnit(otherUnit);
                UnitManager.Instance.DespawnUnit(otherUnit.gameObject);
            }
        }
    }
}