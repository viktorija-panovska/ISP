using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitCloseRangeDetector</c> class represents a unit's smallest collider for detecting other units of its team.
    /// </summary>
    /// <remarks>The close range detector is used to check if there is another unit close enough to this unit 
    /// so that a battle or combining of the two unit can happen, depending on the unit state.</remarks>
    public class UnitCloseRangeDetector : MonoBehaviour
    {
        private Unit m_Unit;
        private Team m_Team;
        private Team m_EnemyTeam;


        /// <summary>
        /// Sets the properties of the wide range detector.
        /// </summary>
        /// <param name="unit">The current <c>Unit</c>.</param>
        public void Setup(Unit unit)
        {
            m_Unit = unit;
            m_Team = unit.Team;
            m_EnemyTeam = unit.Team == Team.RED ? Team.BLUE : Team.RED;
        }

        private void OnTriggerEnter(Collider other)
        {
            Unit otherUnit = other.GetComponent<Unit>();

            if (!otherUnit) return;

            if (otherUnit.Team == m_Team && !m_Unit.HasMaxStrength() && (m_Unit.Class == UnitClass.KNIGHT || 
                (otherUnit.Class != UnitClass.KNIGHT && m_Unit.Strength >= otherUnit.Strength)))
            {
                m_Unit.GainStrength(1);
                UnitManager.Instance.DespawnUnit(otherUnit.gameObject);
            }

            if (otherUnit.Team == m_EnemyTeam && m_Team == Team.RED && !otherUnit.IsInFight)
                UnitManager.Instance.StartFight(m_Unit, otherUnit);
        }
    }
}