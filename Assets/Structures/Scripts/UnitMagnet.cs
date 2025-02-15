using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>UnitMagnet</c> class represents the unit magnet of one of the teams.
    /// </summary>
    public class UnitMagnet : Structure
    {
        #region Event Functions

        private void Start() => m_DestroyMethod = DestroyMethod.NONE;

        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();

            if (!unit || unit.Team != m_Team || unit.Class == UnitClass.KNIGHT)
                return;

            unit.TeamSymbolReached();

            // if we don't have a leader, set the first unit that reached the symbol to be the leader.
            if (GameController.Instance.HasLeader(m_Team)) return;
            UnitManager.Instance.SetUnitLeader(unit.Team, unit);
        }

        #endregion


        /// <inheritdoc />
        public override void ReactToTerrainChange()
        {
            m_OccupiedTile = new(transform.position.x, transform.position.z, getClosestPoint: false);
            int height = Terrain.Instance.GetPointHeight((m_OccupiedTile.GridX, m_OccupiedTile.GridZ));

            if (height < Terrain.Instance.WaterLevel)
                height = Terrain.Instance.WaterLevel;

            SetHeight_ClientRpc(height);
        }

        /// <summary>
        /// Sets the position of the unit magnet to the given position.
        /// </summary>
        /// <param name="position">The new position of the unit magnet.</param>
        [ClientRpc]
        public void SetMagnetPosition_ClientRpc(Vector3 position) => transform.position = position;
    }
}