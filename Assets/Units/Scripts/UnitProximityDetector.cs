using System.Collections.Generic;
using UnityEngine;

namespace Populous
{
    public class UnitProximityDetector : MonoBehaviour
    {
        private Unit m_Unit;
        private UnitState m_UnitState = UnitState.SETTLE;
        private Team m_Team = Team.NONE;
        private Team m_EnemyTeam = Team.NONE;

        private GameObject m_Target;
        private BoxCollider m_Collider;


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
            if ((m_UnitState == UnitState.BATTLE && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_EnemyTeam])) ||
                (m_UnitState == UnitState.GATHER && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_Team])) ||
                (m_Target != null && Vector3.Distance(other.transform.position, transform.position) >= Vector3.Distance(m_Target.transform.position, transform.position)))
                return;

            Debug.Log("Target acquired");

            m_Target = other.gameObject;
            m_Unit.NewTargetAcquired(m_Target);
        }

        private void OnTriggerExit(Collider other)
        {
            if ((m_UnitState == UnitState.BATTLE && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_EnemyTeam])) ||
                (m_UnitState == UnitState.GATHER && other.gameObject.layer != LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)m_Team])) || 
                other.gameObject != m_Target)
                return;

            RemoveTarget(other.gameObject);
        }


        public void StateChange(UnitState state)
        {
            if (state == m_UnitState) return;

            m_UnitState = state;

            if (state == UnitState.GATHER || state == UnitState.BATTLE)
                m_Collider.enabled = true;
            else
                m_Collider.enabled = false;
        }

        public void RemoveTarget(GameObject target)
        {
            if (m_Target == target)
            {
                m_Unit.TargetLost(m_Target);
                m_Target = null;
            }
        }
    }
}