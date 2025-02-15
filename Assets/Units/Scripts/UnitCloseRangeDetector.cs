using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitCloseRangeDetector</c> class represents a unit's smallest collider for detecting other units of its team.
    /// </summary>
    /// <remarks>The close range detector is used to check if there is another unit close enough to this unit 
    /// so that a battle or combining of the two unit can happen, depending on the unit state.</remarks>
    [RequireComponent(typeof(Collider))]
    public class UnitCloseRangeDetector : MonoBehaviour
    {
        /// <summary>
        /// The <c>Unit</c> the detector belongs to.
        /// </summary>
        private Unit m_Unit;
        /// <summary>
        /// The <c>Team</c> of the enemy.
        /// </summary>
        private Team m_EnemyTeam;


        /// <summary>
        /// Sets the properties of the wide range detector.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> the detector belongs to.</param>
        public void Setup(Unit unit)
        {
            m_Unit = unit;
            m_EnemyTeam = unit.Team == Team.RED ? Team.BLUE : Team.RED;
        }


        private void OnTriggerEnter(Collider other)
        {
            Unit otherUnit = other.GetComponent<Unit>();

            if (!otherUnit) return;

            if (otherUnit.Team == m_Unit.Team && !m_Unit.HasMaxStrength() && !m_Unit.IsInFight && 
                (m_Unit.Class == UnitClass.KNIGHT || (otherUnit.Class != UnitClass.KNIGHT && !otherUnit.HasMaxStrength() && m_Unit.Followers >= otherUnit.Followers)))
            {
                m_Unit.GainStrength(otherUnit.Followers);
                UnitManager.Instance.DespawnUnit(otherUnit.gameObject, hasDied: false);
            }

            // make only the red team able to start a fight so both units don't try to do it
            if (otherUnit.Team == m_EnemyTeam && m_Unit.Team == Team.RED && !m_Unit.IsInFight && !otherUnit.IsInFight)
                UnitManager.Instance.StartFight(m_Unit, otherUnit);
        }
    }
}