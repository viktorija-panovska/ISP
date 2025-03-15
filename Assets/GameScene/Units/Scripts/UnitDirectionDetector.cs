using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitDirectionDetector</c> class represents a unit's largest collider for detecting other units and settlements.
    /// </summary>
    /// <remarks>The direction detector is used to find a direction this unit should move in to find units and settlements when there aren't any close to it.</remarks>
    [RequireComponent(typeof(BoxCollider))]
    public class UnitDirectionDetector : MonoBehaviour
    {
        [Tooltip("The number of tiles per side the collider should cover.")]
        [SerializeField] private int m_TilesPerSide = 30;

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
        /// The unit behavior that the detector is operating under.
        /// </summary>
        private UnitBehavior m_CurrentBehavior;
        /// <summary>
        /// A set of all the GameObjects of one faction (depending on the active behavior) in the detector.
        /// </summary>
        private HashSet<GameObject> m_NearbyObjects = new();


        private void Awake() => GetComponent<Collider>().enabled = false;

        private void OnTriggerEnter(Collider other)
        {
            if ((m_Unit.Behavior == UnitBehavior.FIGHT && other.gameObject.layer != LayerData.FactionLayers[(int)m_EnemyFaction]) ||
                (m_Unit.Behavior == UnitBehavior.GATHER && other.gameObject.layer != LayerData.FactionLayers[(int)m_Unit.Faction]))
                return;

            m_NearbyObjects.Add(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            if ((m_Unit.Behavior == UnitBehavior.FIGHT && other.gameObject.layer != LayerData.FactionLayers[(int)m_EnemyFaction]) ||
                (m_Unit.Behavior == UnitBehavior.GATHER && other.gameObject.layer != LayerData.FactionLayers[(int)m_Unit.Faction]))
                return;

            m_NearbyObjects.Remove(other.gameObject);
        }


        /// <summary>
        /// Sets the properties of the direction detector.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> the detector belongs to.</param>
        /// <param name="tilesPerSide">The size of one side of the collider, in terrain tiles.</param>
        public void Setup(Unit unit)
        {
            m_Unit = unit;
            m_EnemyFaction = m_Unit.Faction == Faction.RED ? Faction.BLUE : Faction.RED;
            m_CurrentBehavior = m_Unit.Behavior;

            // setup collider
            m_Collider = GetComponent<BoxCollider>();
            m_Collider.enabled = false;
            SetDetectorSize(m_TilesPerSide);
            m_Collider.center = new Vector3(0, Terrain.Instance.MaxHeight / 2, 0);

            //m_Collider.enabled = true;
        }

        /// <summary>
        /// Sets whether the collider is enabled or disabled based on the active behavior of the associated unit. 
        /// </summary>
        public void UpdateDetector()
        {
            if (m_CurrentBehavior == m_Unit.Behavior) return;

            m_CurrentBehavior = m_Unit.Behavior;
            m_NearbyObjects = new();

            if (m_CurrentBehavior == UnitBehavior.GATHER || m_CurrentBehavior == UnitBehavior.FIGHT)
                m_Collider.enabled = true;
            else
                m_Collider.enabled = false;
        }

        /// <summary>
        /// Makes the detector's collider cover the given number of tiles on each side.
        /// </summary>
        /// <param name="tilesPerSide">The number of tiles the collider should cover on a side.</param>
        public void SetDetectorSize(int tilesPerSide)
            => m_Collider.size = new Vector3(
                tilesPerSide * Terrain.Instance.UnitsPerTileSide,
                Terrain.Instance.MaxHeight,
                tilesPerSide * Terrain.Instance.UnitsPerTileSide
            );

        /// <summary>
        /// Computes the average vector from the positions of all the units and settlements of the desired type in the vicinity.
        /// </summary>
        /// <returns>A <c>Vector3</c> representing the average direction.</returns>
        public Vector3 GetAverageDirection()
        {
            if (m_NearbyObjects.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;

            foreach (GameObject gameObject in m_NearbyObjects)
                sum += gameObject.transform.position - transform.position;

            return (sum / m_NearbyObjects.Count).normalized;
        }

        /// <summary>
        /// Removes the given object from the list of GameObjects in the detector.
        /// </summary>
        /// <param name="gameObject">The <c>GameObject</c> that should be removed.</param>
        public void RemoveObject(GameObject gameObject)
        {
            if (m_NearbyObjects.Contains(gameObject))
                m_NearbyObjects.Remove(gameObject);
        }
    }
}