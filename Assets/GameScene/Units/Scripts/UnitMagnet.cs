using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>UnitMagnet</c> class represents the unit magnet of one of the factions.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class UnitMagnet : NetworkBehaviour
    {
        [SerializeField] private Faction m_Faction;
        /// <summary>
        /// The faction this unit magnet belongs to.
        /// </summary>
        public Faction Faction { get => m_Faction; }

        private TerrainPoint m_GridLocation;
        /// <summary>
        /// The point on the terrain that the unit magnet is placed at.
        /// </summary>
        public TerrainPoint GridLocation { get => m_GridLocation; }


        #region Event Functions

        private void Start() => GetComponent<Collider>().enabled = IsHost;

        private void OnTriggerEnter(Collider other)
        {
            Unit unit = other.GetComponent<Unit>();

            if (!unit || unit.Faction != m_Faction || unit.Type == UnitType.KNIGHT)
                return;

            unit.SetUnitMagnetReached();

            // if we don't have a leader, set the first unit that reached the unit magnet to be the leader.
            if (GameController.Instance.HasLeader(m_Faction)) return;
            GameController.Instance.SetLeader(m_Faction, unit);
        }

        public override void OnDestroy()
        {
            DivineInterventionsController.Instance.OnFlood -= UpdateHeight;
            base.OnDestroy();
        }

        #endregion


        /// <summary>
        /// Sets up the initial state of the unit magnet.
        /// </summary>
        public void Setup()
        {
            m_GridLocation = new(0, 0)/* Terrain.Instance.TerrainCenter*/;
            DivineInterventionsController.Instance.OnFlood += UpdateHeight;

            SetPosition_ClientRpc(m_GridLocation.ToScenePosition());
        }


        #region Move Unit Magnet

        /// <summary>
        /// Updates the height at which the unit magnet is placed.
        /// </summary>
        public void UpdateHeight() => SetPosition_ClientRpc(m_GridLocation.ToScenePosition());

        /// <summary>
        /// Places the unit magnet at the position of the given terrain point.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> at which the unit magnet should be placed.</param>
        public void MoveToPoint(TerrainPoint point)
        {
            m_GridLocation = point;
            SetPosition_ClientRpc(point.ToScenePosition());
        }

        /// <summary>
        /// Moves the unit magnet to the given scene position.
        /// </summary>
        /// <param name="position">The position in the scene the unit magnet should be placed at.</param>
        [ClientRpc]
        private void SetPosition_ClientRpc(Vector3 position) => transform.position = position;

        #endregion
    }
}