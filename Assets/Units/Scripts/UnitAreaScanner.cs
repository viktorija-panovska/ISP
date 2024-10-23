using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    public class UnitAreaScanner : MonoBehaviour
    {
        private UnitState m_UnitState = UnitState.SETTLE;
        private Team m_Team = Team.NONE;
        private Team m_EnemyTeam = Team.NONE;

        private HashSet<Unit> m_NearbyUnits = new();
        private HashSet<Settlement> m_NearbySettlements = new();

        private BoxCollider m_Collider;


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
            if ((m_UnitState == UnitState.BATTLE && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_EnemyTeam])) ||
                (m_UnitState == UnitState.GATHER && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_Team])))
                return;

            if (other.gameObject.GetComponent<Unit>() != null)
                m_NearbyUnits.Add(other.gameObject.GetComponent<Unit>());

            if (other.gameObject.GetComponent<Settlement>() != null)
                m_NearbySettlements.Add(other.gameObject.GetComponent<Settlement>());
        }

        private void OnTriggerExit(Collider other)
        {
            if ((m_UnitState == UnitState.BATTLE && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_EnemyTeam])) ||
                (m_UnitState == UnitState.GATHER && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_Team])))
                return;

            if (other.gameObject.GetComponent<Unit>() != null)
                m_NearbyUnits.Remove(other.gameObject.GetComponent<Unit>());

            if (other.gameObject.GetComponent<Settlement>() != null)
                m_NearbySettlements.Remove(other.gameObject.GetComponent<Settlement>());
        }


        public Vector3 GetAverageDirection()
        {
            if (m_NearbyUnits.Count == 0 && m_NearbySettlements.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;

            foreach (Unit unit in m_NearbyUnits)
                sum += unit.transform.position;

            foreach (Settlement settlement in m_NearbySettlements)
                sum += settlement.transform.position;

            return ((sum / (m_NearbyUnits.Count + m_NearbySettlements.Count)) - transform.position).normalized;
        }


        public void StateChange(UnitState state)
        {
            if (state == m_UnitState) return;

            m_UnitState = state;
            m_NearbyUnits = new();
            m_NearbySettlements = new();

            if (state == UnitState.GATHER || state == UnitState.BATTLE)
                m_Collider.enabled = true;
            else
                m_Collider.enabled = false;
        }

        public void RemoveUnit(Unit unit)
        {
            if (m_NearbyUnits.Contains(unit))
                m_NearbyUnits.Remove(unit);
        }

        public void RemoveSettlement(Settlement settlement)
        {
            if (m_NearbySettlements.Contains(settlement))
                m_NearbySettlements.Remove(settlement);
        }
    }
}