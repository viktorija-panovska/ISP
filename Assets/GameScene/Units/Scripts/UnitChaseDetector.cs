using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitChaseDetector</c> class represents a unit's mid-sized collider for detecting other units and enemy settlements.
    /// </summary>
    /// <remarks>The chase detector is used to check if there is another unit or enemy settlement close enough to this unit 
    /// so that it can go after it to either combine or fight if its a unit, or fight if its an enemy settlement.</remarks>
    [RequireComponent(typeof(BoxCollider))]
    public class UnitChaseDetector : MonoBehaviour
    {
        [Tooltip("The number of tiles per side the collider should cover.")]
        [SerializeField] private int m_TilesPerSide = 1;

        /// <summary>
        /// The <c>Unit</c> the detector belongs to.
        /// </summary>
        private Unit m_Unit;
        /// <summary>
        /// The <c>Faction</c> of the enemy.
        /// </summary>
        private Faction m_EnemyFaction;
        /// <summary>
        /// The collider representing the detector.
        /// </summary>
        private BoxCollider m_Collider;
        /// <summary>
        /// The GameObject the collider has detected as a potential chase target for the current unit.
        /// </summary>
        private GameObject m_ChaseTarget;


        private void OnTriggerEnter(Collider other)
        {
            if ((m_Unit.Behavior == UnitBehavior.FIGHT && other.gameObject.layer != LayerData.FactionLayers[(int)m_EnemyFaction]) ||
                (m_Unit.Behavior == UnitBehavior.GATHER && (!other.GetComponent<Unit>() || other.gameObject.layer != LayerData.FactionLayers[(int)m_Unit.Faction]) ||
                (m_ChaseTarget && Vector3.Distance(other.transform.position, transform.position) >= Vector3.Distance(m_ChaseTarget.transform.position, transform.position))))
                return;

            m_ChaseTarget = other.gameObject;
        }

        private void OnTriggerExit(Collider other)
        {
            if ((m_Unit.Behavior == UnitBehavior.FIGHT && other.gameObject.layer != LayerData.FactionLayers[(int)m_EnemyFaction]) ||
                (m_Unit.Behavior == UnitBehavior.GATHER && (!other.GetComponent<Unit>() || other.gameObject.layer != LayerData.FactionLayers[(int)m_Unit.Faction])))
                return;

            RemoveTarget(other.gameObject);
        }


        /// <summary>
        /// Sets the properties of the chase detector.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> the detector belongs to.</param>
        /// <param name="tilesPerSide">The size of one side of the collider, in terrain tiles.</param>
        public void Setup(Unit unit)
        {
            m_Unit = unit;
            m_EnemyFaction = unit.Faction == Faction.RED ? Faction.BLUE : Faction.RED;

            // setup collider
            m_Collider = GetComponent<BoxCollider>();
            m_Collider.enabled = false;
            m_Collider.size = new Vector3(
                m_TilesPerSide * Terrain.Instance.UnitsPerTileSide,
                Terrain.Instance.MaxHeight,
                m_TilesPerSide * Terrain.Instance.UnitsPerTileSide
            );
            m_Collider.center = new Vector3(0, m_Collider.size.y / 4, m_Collider.size.x / 2);
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
        public GameObject GetTarget() => m_ChaseTarget;

        /// <summary>
        /// Removes the target if it matches the given game object.
        /// </summary>
        /// <param name="gameObject">The <c>GameObject</c> that should be checked against the target.</param>
        public void RemoveTarget(GameObject gameObject)
        {
            if (m_ChaseTarget != gameObject) return;

            m_Unit.LoseTarget(gameObject);
            m_ChaseTarget = null;
        }
    }
}