using UnityEngine;

namespace Populous
{
    public class UnitBattleDetector : MonoBehaviour
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

            if (otherUnit == null || otherUnit.Team != m_EnemyTeam || otherUnit.IsBattling || m_Team == Team.BLUE)
                return;

            Debug.Log("BATTLE");
            //UnitManager.Instance.StartBattle(m_Unit, other.GetComponent<Unit>());
        }
    }
}