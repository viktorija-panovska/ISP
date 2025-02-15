using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitMidRangeDetector</c> class represents a unit's middle collider for detecting other units of its team.
    /// </summary>
    /// <remarks>The mid range detector is used to check if there is another unit close enough to this unit 
    /// so that it can start chasing it to either combine or battle, depending on the unit state.</remarks>
    [RequireComponent(typeof(BoxCollider))]
    public class UnitMidRangeDetector : MonoBehaviour
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
        /// The collider representing the detector.
        /// </summary>
        private BoxCollider m_Collider;
        /// <summary>
        /// 
        /// </summary>
        private GameObject m_Target;


        /// <summary>
        /// Sets the properties of the mid range detector.
        /// </summary>
        /// <param name="unit">The <c>Team</c> this follower belongs to.</param>
        /// <param name="tilesPerSide">The size of one side of the collider, in tiles.</param>
        public void Setup(Unit unit, int tilesPerSide)
        {
            m_Unit = unit;
            m_EnemyTeam = unit.Team == Team.RED ? Team.BLUE : Team.RED;

            // setup collider
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
            if ((m_Unit.Behavior == UnitBehavior.FIGHT && other.gameObject.layer != LayerData.TeamLayers[(int)m_EnemyTeam]) ||
                (m_Unit.Behavior == UnitBehavior.GATHER && (other.gameObject.layer != LayerData.TeamLayers[(int)m_Unit.Team]) ||
                (m_Target && Vector3.Distance(other.transform.position, transform.position) >= Vector3.Distance(m_Target.transform.position, transform.position))))
                return;

            m_Target = other.gameObject;
        }

        private void OnTriggerExit(Collider other)
        {
            if ((m_Unit.Behavior == UnitBehavior.FIGHT && other.gameObject.layer != LayerData.TeamLayers[(int)m_EnemyTeam]) ||
                (m_Unit.Behavior == UnitBehavior.GATHER && (other.gameObject.layer != LayerData.TeamLayers[(int)m_Unit.Team])))
                return;

            RemoveTarget(other.gameObject);
        }

        /// <summary>
        /// Sets whether the collider is enabled or disabled based on the active behavior of the associated unit. 
        /// </summary>
        public void UpdateDetector()
        {
            if (m_Unit.Behavior == UnitBehavior.GATHER || m_Unit.Behavior == UnitBehavior.FIGHT)
                m_Collider.enabled = true;
            else
                m_Collider.enabled = false;
        }

        /// <summary>
        /// Returns the object that has been detected as a target to follow.
        /// </summary>
        /// <returns>The target <c>GameObject</c>, null if there is no target.</returns>
        public GameObject GetTarget() => m_Target;

        /// <summary>
        /// Removes the target if it matches the given game object.
        /// </summary>
        /// <param name="gameObject">The <c>GameObject</c> that should be checked against the target.</param>
        public void RemoveTarget(GameObject gameObject)
        {
            if (m_Target != gameObject) return;

            m_Unit.LoseTarget(gameObject);
            m_Target = null;
        }
    }
}