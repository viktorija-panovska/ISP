using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitMidRangeDetector</c> class implements the functionality of the middle collider for detecting other units that a team has.
    /// </summary>
    /// <remarks>The mid range detector is used to check if there is another unit close enough to this unit 
    /// so that it can start chasing it to either combine or battle, depending on the unit state.</remarks>
    public class UnitMidRangeDetector : MonoBehaviour
    {
        private Unit m_Unit;
        private UnitBehavior m_UnitState = UnitBehavior.SETTLE;
        private Team m_Team = Team.NONE;
        private Team m_EnemyTeam = Team.NONE;

        private GameObject m_Target;
        private BoxCollider m_Collider;


        /// <summary>
        /// Sets the properties of the mid range detector.
        /// </summary>
        /// <param name="unit">The <c>Team</c> this follower belongs to.</param>
        /// <param name="tilesPerSide">The size of one side of the detector, in tiles.</param>
        public void Setup(Unit unit, int tilesPerSide)
        {
            m_Unit = unit;
            m_Team = unit.Team;
            m_EnemyTeam = unit.Team == Team.RED ? Team.BLUE : Team.RED;

            m_Collider = GetComponent<BoxCollider>();
            m_Collider.enabled = false;
            m_Collider.size = new Vector3(
                tilesPerSide * Terrain.Instance.UnitsPerTileSide,
                Terrain.Instance.MaxHeight,
                tilesPerSide * Terrain.Instance.UnitsPerTileSide
            );
            m_Collider.center = new Vector3(0, m_Collider.size.y / 4, m_Collider.size.x / 2);
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((m_UnitState == UnitBehavior.FIGHT && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_EnemyTeam])) ||
                (m_UnitState == UnitBehavior.GATHER && (other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_Team])) ||
                (m_Target != null && Vector3.Distance(other.transform.position, transform.position) >= Vector3.Distance(m_Target.transform.position, transform.position))))
                return;

            Settlement settlement = other.GetComponent<Settlement>();
            Unit unit = other.GetComponent<Unit>();

            if ((settlement && (m_UnitState == UnitBehavior.GATHER || settlement.IsRuined)) || (unit && unit.IsInFight))
                return;

            m_Target = other.gameObject;
        }

        private void OnTriggerExit(Collider other)
        {
            if ((m_UnitState == UnitBehavior.FIGHT && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_EnemyTeam])) ||
                (m_UnitState == UnitBehavior.GATHER && (other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_Team]) || other.GetComponent<Unit>() == null)) ||
                other.gameObject != m_Target)
                return;

            RemoveTarget(other.gameObject);
        }

        /// <summary>
        /// Changes the current active state of this unit to the given state.
        /// </summary>
        /// <param name="state">The <c>UnitBehavior</c> that this unit's state should be set to.</param>
        public void StateChange(UnitBehavior state)
        {
            if (state == m_UnitState) return;

            m_UnitState = state;

            if (state == UnitBehavior.GATHER || state == UnitBehavior.FIGHT)
                m_Collider.enabled = true;
            else
                m_Collider.enabled = false;
        }

        public GameObject GetTarget() => m_Target;

        /// <summary>
        /// Removes the target if it matches the given game object.
        /// </summary>
        /// <param name="gameObject">The <c>GameObject</c> that should be checked against the target.</param>
        public void RemoveTarget(GameObject gameObject)
        {
            if (m_Target == gameObject)
            {
                m_Unit.LoseTarget(m_Target);
                m_Target = null;
            }
        }
    }
}