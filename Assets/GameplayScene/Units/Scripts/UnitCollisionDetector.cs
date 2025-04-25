using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitCollisionDetector</c> class represents a unit's smallest collider for detecting other units.
    /// </summary>
    /// <remarks>The collision detector is used to check if there is another unit close enough to this unit 
    /// so that a battle or combining of the two unit can happen, depending on the other unit's faction.</remarks>
    [RequireComponent(typeof(Collider))]
    public class UnitCollisionDetector : MonoBehaviour
    {
        /// <summary>
        /// The <c>Unit</c> the detector belongs to.
        /// </summary>
        private Unit m_Unit;
        /// <summary>
        /// The <c>Faction</c> of the enemy.
        /// </summary>
        private Faction m_EnemyFaction;

        private void Awake() => GetComponent<Collider>().enabled = false;

        private void OnTriggerEnter(Collider other)
        {
            if (m_Unit.IsInFight) return;

            Unit otherUnit = other.GetComponent<Unit>();

            if (!otherUnit || otherUnit.IsInFight) return;

            // make the stronger unit gain strength so both units don't try to do it
            if (otherUnit.Faction == m_Unit.Faction && (
                (m_Unit.Type == UnitType.KNIGHT && otherUnit.Type == UnitType.KNIGHT && m_Unit.Strength >= otherUnit.Strength) ||
                (m_Unit.Type != UnitType.WALKER && otherUnit.Type == UnitType.WALKER) ||
                (m_Unit.Type == UnitType.WALKER && otherUnit.Type == UnitType.WALKER && m_Unit.Strength >= otherUnit.Strength)
               ))
            {
                m_Unit.GainStrength(otherUnit.Strength);
                UnitManager.Instance.DespawnUnit(otherUnit, hasDied: false);
            }

            // make only the red faction able to start a fight so both units don't try to do it
            if (otherUnit.Faction == m_EnemyFaction && m_Unit.Faction == Faction.RED)
                UnitManager.Instance.StartFight(m_Unit, otherUnit);
        }

        /// <summary>
        /// Sets the properties of the collision detector.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> the detector belongs to.</param>
        public void Setup(Unit unit)
        {
            m_Unit = unit;
            m_EnemyFaction = unit.Faction == Faction.RED ? Faction.BLUE : Faction.RED;
            GetComponent<Collider>().enabled = true;
        }
    }
}