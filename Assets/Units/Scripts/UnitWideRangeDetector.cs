using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitCloseRangeDetector</c> class represents a unit's largest collider for detecting other units of its team.
    /// </summary>
    /// <remarks>The wide range detector is used to find a direction this team should move in to find other team when there aren't any close to it.</remarks>
    public class UnitWideRangeDetector : MonoBehaviour
    {
        private Unit m_Unit;
        private Team m_Team = Team.NONE;
        private Team m_EnemyTeam = Team.NONE;

        private HashSet<GameObject> m_NearbyObjects = new();
        private BoxCollider m_Collider;

        /// <summary>
        /// Sets the properties of the wide range detector.
        /// </summary>
        /// <param name="team">The <c>Team</c> this unit belongs to.</param>
        /// <param name="tilesPerSide">The size of one side of the detector, in tiles.</param>
        public void Setup(Unit unit, int tilesPerSide)
        {
            m_Unit = unit;
            m_Team = unit.Team;
            m_EnemyTeam = m_Team == Team.RED ? Team.BLUE : Team.RED;

            SetDetectorSize(tilesPerSide);
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((m_Unit.Behavior == UnitBehavior.FIGHT && other.gameObject.layer != LayerData.TeamLayers[(int)m_EnemyTeam]) ||
                (m_Unit.Behavior == UnitBehavior.GATHER && other.gameObject.layer != LayerData.TeamLayers[(int)m_Team]))
                return;

            if (other.gameObject.GetComponent<Unit>() || other.gameObject.GetComponent<Settlement>())
                m_NearbyObjects.Add(other.gameObject);
        }

        private void OnTriggerExit(Collider other)
        {
            if ((m_Unit.Behavior == UnitBehavior.FIGHT && other.gameObject.layer != LayerData.TeamLayers[(int)m_EnemyTeam]) ||
                (m_Unit.Behavior == UnitBehavior.GATHER && other.gameObject.layer != LayerData.TeamLayers[(int)m_Team]))
                return;

            if (other.gameObject.GetComponent<Unit>())
                m_NearbyObjects.Remove(other.gameObject);
        }


        public void SetDetectorSize(int tilesPerSide)
        {
            m_Collider = GetComponent<BoxCollider>();
            m_Collider.enabled = false;
            m_Collider.size = new Vector3(
                tilesPerSide * Terrain.Instance.UnitsPerTileSide,
                Terrain.Instance.MaxHeight,
                tilesPerSide * Terrain.Instance.UnitsPerTileSide
            );
            m_Collider.center = new Vector3(0, Terrain.Instance.MaxHeight / 2, 0);
        }

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
                sum += gameObject.transform.position * Vector3.Distance(gameObject.transform.position, m_Unit.transform.position);

            return ((sum / m_NearbyObjects.Count) - transform.position).normalized;
        }

        /// <summary>
        /// Changes the current active behavior of this unit to the given behavior.
        /// </summary>
        /// <param name="behavior">The <c>UnitBehavior</c> that this unit's behavior should be set to.</param>
        public void BehaviorChange(UnitBehavior behavior)
        {
            if (behavior == m_Unit.Behavior) return;

            m_NearbyObjects = new();

            if (behavior == UnitBehavior.GATHER || behavior == UnitBehavior.FIGHT)
                m_Collider.enabled = true;
            else
                m_Collider.enabled = false;
        }

        /// <summary>
        /// Removes the given object from the list of units in the vicinity.
        /// </summary>
        /// <param name="gameObject">The <c>GameObject</c> that should be removed.</param>
        public void RemoveObject(GameObject gameObject)
        {
            if (m_NearbyObjects.Contains(gameObject))
                m_NearbyObjects.Remove(gameObject);
        }
    }
}