using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>UnitWideRangeDetector</c> class implements the functionality of the largest collider for detecting other units that a team has.
    /// </summary>
    /// <remarks>The wide range detector is used to find a direction this team should move in to find other team when there aren't any close to it.</remarks>
    public class UnitWideRangeDetector : MonoBehaviour
    {
        private UnitBehavior m_UnitState = UnitBehavior.SETTLE;
        private Team m_Team = Team.NONE;
        private Team m_EnemyTeam = Team.NONE;

        private HashSet<Unit> m_NearbyUnits = new();
        private BoxCollider m_Collider;

        /// <summary>
        /// Sets the properties of the wide range detector.
        /// </summary>
        /// <param name="team">The <c>Team</c> this unit belongs to.</param>
        /// <param name="tilesPerSide">The size of one side of the detector, in tiles.</param>
        public void Setup(Team team, int tilesPerSide)
        {
            m_Team = team;
            m_EnemyTeam = team == Team.RED ? Team.BLUE : Team.RED;

            m_Collider = GetComponent<BoxCollider>();
            m_Collider.enabled = false;
            m_Collider.size = new Vector3(
                tilesPerSide * Terrain.Instance.UnitsPerTileSide,
                Terrain.Instance.MaxHeight,
                tilesPerSide * Terrain.Instance.UnitsPerTileSide
            );
            m_Collider.center = new Vector3(0, Terrain.Instance.MaxHeight / 2, 0);
        }

        private void OnTriggerEnter(Collider other)
        {
            if ((m_UnitState == UnitBehavior.FIGHT && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_EnemyTeam])) ||
                (m_UnitState == UnitBehavior.GATHER && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_Team])))
                return;

            if (other.gameObject.GetComponent<Unit>() != null)
                m_NearbyUnits.Add(other.gameObject.GetComponent<Unit>());
        }

        private void OnTriggerExit(Collider other)
        {
            if ((m_UnitState == UnitBehavior.FIGHT && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_EnemyTeam])) ||
                (m_UnitState == UnitBehavior.GATHER && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_Team])))
                return;

            if (other.gameObject.GetComponent<Unit>() != null)
                m_NearbyUnits.Remove(other.gameObject.GetComponent<Unit>());
        }

        /// <summary>
        /// Computes the average vector from the positions of all the units in the vicinity.
        /// </summary>
        /// <returns>A <c>Vector3</c> representing the average direction.</returns>
        public Vector3 GetAverageDirection()
        {
            if (m_NearbyUnits.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;

            foreach (Unit unit in m_NearbyUnits)
                sum += unit.transform.position;

            return ((sum / m_NearbyUnits.Count) - transform.position).normalized;
        }

        /// <summary>
        /// Changes the current active state of this unit to the given state.
        /// </summary>
        /// <param name="state">The <c>UnitBehavior</c> that this unit's state should be set to.</param>
        public void StateChange(UnitBehavior state)
        {
            if (state == m_UnitState) return;

            m_UnitState = state;
            m_NearbyUnits = new();

            if (state == UnitBehavior.GATHER || state == UnitBehavior.FIGHT)
                m_Collider.enabled = true;
            else
                m_Collider.enabled = false;
        }

        /// <summary>
        /// Removes the given unit from the list of units in the vicinity.
        /// </summary>
        /// <param name="unit">The <c>Unit</c> that should be removed.</param>
        public void RemoveUnit(Unit unit)
        {
            if (m_NearbyUnits.Contains(unit))
                m_NearbyUnits.Remove(unit);
        }
    }
}