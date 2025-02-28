using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>UnitMagnet</c> class represents the unit magnet of one of the factions.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(Collider))]
    public class UnitMagnet : NetworkBehaviour
    {
        [SerializeField] private Faction m_Faction;
        /// <summary>
        /// 
        /// </summary>
        public Faction Faction { get => m_Faction; }

        private TerrainPoint m_GridLocation;
        /// <summary>
        /// 
        /// </summary>
        public TerrainPoint GridLocation { get => m_GridLocation; }


        /// <summary>
        /// Sets up the initial state of the unit magnet.
        /// </summary>
        public void Setup()
        {
            m_GridLocation = Terrain.Instance.TerrainCenter;
            SetPosition_ClientRpc(m_GridLocation.ToScenePosition());
            GameController.Instance.OnFlood += UpdateHeight;
        }


        public override void OnDestroy()
        {
            GameController.Instance.OnFlood -= UpdateHeight;
            base.OnDestroy();
        }

        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();

            if (!unit || unit.Faction != m_Faction || unit.Type == UnitType.KNIGHT)
                return;

            unit.SetUnitMagnetReached();

            // if we don't have a leader, set the first unit that reached the symbol to be the leader.
            if (GameController.Instance.HasLeader(m_Faction)) return;
            UnitManager.Instance.SetUnitLeader(unit.Faction, unit);
        }


        /// <summary>
        /// Updates the height at which the unit magnet is placed.
        /// </summary>
        public void UpdateHeight() 
        {
            m_GridLocation = new(transform.position);
            SetPosition_ClientRpc(m_GridLocation.ToScenePosition());
        }

        /// <summary>
        /// Moves the unit magnet to the given terrain point.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> the unit magnet should be placed at.</param>
        [ClientRpc]
        public void SetPosition_ClientRpc(Vector3 position) => transform.position = position;
    }
}