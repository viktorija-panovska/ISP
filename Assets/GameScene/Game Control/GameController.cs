using System;
using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>ILeader</c> interface defines methods necessary for classes that belong to objects that can be leaders of a faction (i.e. units and settlements)
    /// </summary>
    public interface ILeader
    {
        /// <summary>
        /// The GameObject associated with the class.
        /// </summary>
        public GameObject GameObject { get; }

        /// <summary>
        /// Sets whether the object is a leader or not.
        /// </summary>
        /// <param name="isLeader">True if the object should be set as the leader, false otherwise.</param>
        public void SetLeader(bool isLeader);
    }


    /// <summary>
    /// The <c>GameController</c> class that manages the state of the game (paused/unpaused, game ended) and handles the behavior of game elements
    /// that do not belong to a more specific script.
    /// </summary>
    public class GameController : NetworkBehaviour
    {
        #region Inspector Fields

        [Tooltip("The color representing each faction. Index 0 is the Red faction, index 1 is the Blue faction, and index 2 in None faction.")]
        [SerializeField] private Color[] m_FactionColors;
        [Tooltip("The GameObjects of the unit magnets of each faction. Index 0 is the Red unit magnet and index 1 is the Blue unit magnet.")]
        [SerializeField] private GameObject[] m_UnitMagnetObjects;

        #endregion


        #region Class Fields

        private static GameController m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static GameController Instance { get => m_Instance; }

        /// <summary>
        /// An array of the colors of each faction. 
        /// </summary>
        /// <remarks>The color at each index is the color of the faction with that value in the <c>Faction</c> enum.</remarks>
        public Color[] FactionColors { get => m_FactionColors; }

        /// <summary>
        /// An array of references to the unit magnets of both factions.
        /// </summary>
        /// <remarks>The unit magnet at each index is the unit magnet of the faction with that value in the <c>Faction</c> enum.</remarks>
        private readonly UnitMagnet[] m_UnitMagnets = new UnitMagnet[2];

        /// <summary>
        /// Each cell represents one of the factions, and the object in the cell is that faction's leader.
        /// </summary>
        /// <remarks>The object at each index is the leader of the faction with that value in the <c>Faction</c> enum.</remarks>
        private readonly ILeader[] m_Leaders = new ILeader[2];

        #endregion


        #region Actions

        /// <summary>
        /// Action to be called when the unit magnet of the red faction is moved.
        /// </summary>
        public Action OnRedMagnetMoved;
        /// <summary>
        /// Action to be called when the unit magnet of the blue faction is moved.
        /// </summary>
        public Action OnBlueMagnetMoved;

        #endregion


        #region Event Functions

        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }

        private void Start()
        {
            // on each client
            Terrain.Instance.Create();
            Water.Instance.Create();
            Frame.Instance.Create();
            Minimap.Instance.Create();
            BorderWalls.Instance.Create();
            MinimapCamera.Instance.Setup();

            // just on server from here on
            if (!IsHost) return;

            StructureManager.Instance.PlaceTreesAndRocks();
            UnitManager.Instance.SpawnStartingUnits();

            foreach (GameObject unitMagnetObject in m_UnitMagnetObjects)
            {
                UnitMagnet unitMagnet = unitMagnetObject.GetComponent<UnitMagnet>();
                m_UnitMagnets[(int)unitMagnet.Faction] = unitMagnet;
                unitMagnet.Setup();
            }
        }

        #endregion


        #region Game Flow

        /// <summary>
        /// Notifies all clients to set the state of their game to paused or unpaused.
        /// </summary>
        /// <param name="isPaused">True if the game should be paused, false otherwise.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SetPause_ServerRpc(bool isPaused) => SetPause_ClientRpc(isPaused);

        /// <summary>
        /// Pauses the game if it is unpaused and unpauses the game if it is paused for the client.
        /// </summary>
        /// <param name="isPaused">True if the game is paused, false otherwise.</param>
        [ClientRpc]
        private void SetPause_ClientRpc(bool isPaused) => PlayerController.Instance.SetPause(isPaused);

        /// <summary>
        /// Notifies all clients to end the game.
        /// </summary>
        /// <param name="winner">The <c>Faction</c> that won the game.</param>
        [ClientRpc]
        public void EndGame_ClientRpc(Faction winner) => PlayerController.Instance.EndGame(winner);

        #endregion


        #region Camera

        /// <summary>
        /// Notifies the client that the object with the given ID is not in the camera's field of view anymore.
        /// </summary>
        /// <remarks>Used when an object is despawned.</remarks>
        /// <param name="objectId">The network ID of the object that is not visible anymore.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        public void RemoveVisibleObject_ClientRpc(ulong objectId, ClientRpcParams clientParams = default)
            => CameraDetectionZone.Instance.RemoveVisibleObject(objectId);


        /// <summary>
        /// Sets the position of the follow target, and thus sets the point where the camera is looking.
        /// </summary>
        /// <param name="position">The new position of the follow target.</param>
        /// <param name="clientRpcParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        public void SetCameraLookPosition_ClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
            => PlayerCamera.Instance.SetCameraLookPosition(position);

        #endregion


        #region Unit Magnets

        /// <summary>
        /// Gets the position of the unit magnet of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the unit magnet belongs to.</param>
        /// <returns>A <c>Vector3</c> of the position of the unit magnet in the scene.</returns>
        public TerrainPoint GetUnitMagnetLocation(Faction faction) => m_UnitMagnets[(int)faction].GridLocation;

        /// <summary>
        /// Sets the unit magnet of the given faction to the position of the given terrain point.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the unit magnet belongs to.</param>
        /// <param name="point">The <c>TerrainPoint</c> that the unit magnet should be placed at.</param>
        public void PlaceUnitMagnetAtPoint(Faction faction, TerrainPoint point)
        {
            UnitMagnet magnet = m_UnitMagnets[(int)faction];

            if (magnet.GridLocation == point) return;
            magnet.MoveToPoint(point);

            if (UnitManager.Instance.GetActiveBehavior(faction) != UnitBehavior.GO_TO_MAGNET) return;

            if (faction == Faction.RED)
                OnRedMagnetMoved?.Invoke();
            else if (faction == Faction.BLUE)
                OnBlueMagnetMoved?.Invoke();
        }

        /// <summary>
        /// Updates the heights of the unit magnets, if they are in the given area.
        /// </summary>
        /// <param name="bottomLeft">The bottom-left corner of a rectangular area containing all modified terrain points.</param>
        /// <param name="topRight">The top-right corner of a rectangular area containing all modified terrain points.</param>
        public void UpdateMagnetsInArea(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            foreach (UnitMagnet magnet in m_UnitMagnets)
            {
                if (magnet.GridLocation.X >= bottomLeft.X && magnet.GridLocation.X <= topRight.X &&
                    magnet.GridLocation.Z >= bottomLeft.Z && magnet.GridLocation.Z <= topRight.Z)
                    magnet.UpdateHeight();
            }
        }

        #endregion


        #region Leaders

        /// <summary>
        /// Checks whether the given faction has a leader.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be checked.</param>
        /// <returns>True if the faction has a leader, false otherwise.</returns>
        public bool HasLeader(Faction faction) => m_Leaders[(int)faction] != null;
        /// <summary>
        /// Checks whether the given faction has a leader that is an unit.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be checked.</param>
        /// <returns>True if the faction has a leader unit, false otherwise.</returns>
        public bool HasLeaderUnit(Faction faction) => HasLeader(faction) && m_Leaders[(int)faction].GetType() == typeof(Unit);
        /// <summary>
        /// Checks whether the given faction has a leader that is a settlement.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be checked.</param>
        /// <returns>True if the faction has a settlement leader, false otherwise.</returns>
        public bool HasLeaderSettlement(Faction faction) => HasLeader(faction) && m_Leaders[(int)faction].GetType() == typeof(Settlement);

        /// <summary>
        /// Gets the leader of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be returned.</param>
        /// <returns>The <c>ILeader</c> of the given faction, null if there is none.</returns>
        public ILeader GetLeader(Faction faction) => m_Leaders[(int)faction];
        /// <summary>
        /// Gets the unit leader of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be returned.</param>
        /// <returns>The <c>Unit</c> that is the leader of the given faction, null if there is none.</returns>
        public Unit GetLeaderUnit(Faction faction) => (Unit)GetLeader(faction);
        /// <summary>
        /// Gets the settlement leader of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be returned.</param>
        /// <returns>The <c>Settlement</c> that is the leader of the given faction, null if there is none.</returns>
        public Settlement GetLeaderSettlement(Faction faction) => (Settlement)GetLeader(faction);

        /// <summary>
        /// Sets the given <c>ILeader</c> as the leader of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> that the leader should be set to.</param>
        /// <param name="leader">The <c>ILeader</c> that should be set as the leader.</param>
        public void SetLeader(Faction faction, ILeader leader)
        {
            if (HasLeader(faction))
                RemoveLeader(faction);

            m_Leaders[(int)faction] = leader;
            leader.SetLeader(true);
            UnitManager.Instance.SwitchLeaderTarget(faction);
        }

        /// <summary>
        /// Removes the leader of the given faction, if it exists.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be removed.</param>
        public void RemoveLeader(Faction faction)
        {
            m_Leaders[(int)faction].SetLeader(false);
            m_Leaders[(int)faction] = null;
        }

        #endregion
    }
}