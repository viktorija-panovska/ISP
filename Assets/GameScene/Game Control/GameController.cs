using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The Divine Interventions available in the game.
    /// </summary>
    public enum Power
    {
        /// <summary>
        /// The "Mold Terrain" Divine Intervention.
        /// </summary>
        MOLD_TERRAIN,
        /// <summary>
        /// The "Place Unit Magnet" Divine Intervention.
        /// </summary>
        PLACE_MAGNET,
        /// <summary>
        /// The "Earthquake" Divine Intervention.
        /// </summary>
        EARTHQUAKE,
        /// <summary>
        /// The "Swamp" Divine Intervention.
        /// </summary>
        SWAMP,
        /// <summary>
        /// The "Knight" Divine Intervention.
        /// </summary>
        KNIGHT,
        /// <summary>
        /// The "Volcano" Divine Intervention.
        /// </summary>
        VOLCANO,
        /// <summary>
        /// The "Flood" Divine Intervention.
        /// </summary>
        FLOOD,
        /// <summary>
        /// The "Armageddon" Divine Intervention.
        /// </summary>
        ARMAGEDDON
    }

    /// <summary>
    /// The "Snap To" actions available in the game.
    /// </summary>
    public enum SnapTo
    {
        /// <summary>
        /// Snap the camera to the currently inspected object.
        /// </summary>
        INSPECTED_OBJECT,
        /// <summary>
        /// Snap the camera to the faction's unit magnet.
        /// </summary>
        UNIT_MAGNET,
        /// <summary>
        /// Snap the camera to the faction's leader.
        /// </summary>
        LEADER,
        /// <summary>
        /// Snap the camera to a settlement belonging to the faction.
        /// </summary>
        SETTLEMENT,
        /// <summary>
        /// Snap the camera to an active fight.
        /// </summary>
        FIGHT,
        /// <summary>
        /// Snap the camera to a knight belonging to the faction.
        /// </summary>
        KNIGHT
    }


    /// <summary>
    /// The <c>GameController</c> class controls the flow of the game and the executes the players' actions or passes them along to systems that can execute them.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class GameController : NetworkBehaviour
    {
        #region Inspector Fields

        [Tooltip("The color representing each faction. Index 0 is the Red faction, index 1 is the Blue faction, and index 2 in None faction.")]
        [SerializeField] private Color[] m_FactionColors;
        [Tooltip("The GameObjects of the unit magnets of each faction. Index 0 is the Red unit magnet and index 1 is the Blue unit magnet.")]
        [SerializeField] private GameObject[] m_UnitMagnetObjects;

        [Header("Manna")]
        [Tooltip("The maximum amount of manna a player can have.")]
        [SerializeField] private int m_MaxManna = 100;
        [Tooltip("The percentage of manna that is required to be full in order to have access to a Divine Intervention." +
            "The value for a Divine Intervention is at the index equal to the value of the Divine Intervention in the Power enum.")]
        [SerializeField] private float[] m_PowerActivationPercent = new float[Enum.GetNames(typeof(Power)).Length];
        [Tooltip("The amount of manna spent for using a Divine Intervention." +
            "The value for a Divine Intervention is at the index equal to the value of the Divine Intervention in the Power enum.")]
        [SerializeField] private int[] m_PowerMannaCost = new int[Enum.GetNames(typeof(Power)).Length];

        [Header("Divine Interventions")]
        [Tooltip("Half the number of tiles on one side of the square area of effect of the Earthquake.")]
        [SerializeField] private int m_EarthquakeRadius = 3;
        [Tooltip("Half the number of tiles on one side of the square area of effect of the Swamp.")]
        [SerializeField] private int m_SwampRadius = 3;
        [Tooltip("Half the number of tiles on one side of the square area of effect of the Volcano.")]
        [SerializeField] private int m_VolcanoRadius = 3;

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

        /// <summary>
        /// Each cell represents one of the factions, and the object in the cell is the object that faction's player is inspecting.
        /// </summary>
        /// <remarks>The object at each index is the inspected object of the player of the faction with that value in the <c>Faction</c> enum.</remarks>
        private readonly IInspectableObject[] m_InspectedObjects = new IInspectableObject[2];


        #region Manna

        /// <summary>
        /// Gets the maximum amount of manna a faction can have.
        /// </summary>
        public int MaxManna { get => m_MaxManna; }
        /// <summary>
        /// An array of the amount of manna each faction has.
        /// </summary>
        /// <remarks>The manna at each index is the manna of the faction with that value in the <c>Faction</c> enum.</remarks>
        private readonly int[] m_Manna = new int[2];

        #endregion


        #region Divine Interventions

        /// <summary>
        /// Gets half the number of tiles on a side of the area of effect of the Earthquake.
        /// </summary>
        public int EarthquakeRadius { get => m_EarthquakeRadius; }
        /// <summary>
        /// Gets half the number of tiles on a side of the area of effect of the Swamp.
        /// </summary>
        public int SwampRadius { get => m_SwampRadius; }
        /// <summary>
        /// Gets half the number of tiles on a side of the area of effect of the Volcano.
        /// </summary>
        public int VolcanoRadius { get => m_VolcanoRadius; }

        private bool m_IsArmageddon;
        /// <summary>
        /// True if the Armageddon Divine Intervention has been activated, false otherwise.
        /// </summary>
        public bool IsArmageddon { get => m_IsArmageddon; }

        #endregion


        #region Camera Snap

        /// <summary>
        /// An array containing the index of the next fight the player's camera will snap to if the Zoom to Fight action is performed.
        /// </summary>
        /// <remarks>The fight index at each array index is the fight index of the faction with that value in the <c>Faction</c> enum.</remarks>
        private readonly int[] m_FightIndex = new int[2];
        /// <summary>
        /// An array containing the index of the next knight the player's camera will snap to if the Zoom to Knight action is performed.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly int[] m_KnightsIndex = new int[2];
        /// <summary>
        /// An array containing the index of the next settlement the player's camera will snap to if the Zoom to Settlement action is performed.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly int[] m_SettlementIndex = new int[2];

        #endregion

        #endregion


        #region Actions

        /// <summary>
        /// Action to be called when the heights of the terrain have been modified.
        /// </summary>
        public Action<TerrainPoint, TerrainPoint> OnTerrainModified;
        /// <summary>
        /// Action to be called when the unit magnet of the red team is moved.
        /// </summary>
        public Action OnRedMagnetMoved;
        /// <summary>
        /// Action to be called when the unit magnet of the blue team is moved.
        /// </summary>
        public Action OnBlueMagnetMoved;
        /// <summary>
        /// Action to be called when the Flood power is used.
        /// </summary>
        public Action OnFlood;
        /// <summary>
        /// Action to be called when the Armageddon power is used.
        /// </summary>
        public Action OnArmageddon;

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

            //StructureManager.Instance.PlaceTreesAndRocks();
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


        #region Unit Magnets

        /// <summary>
        /// Gets the position of the unit magnet of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the unit magnet belongs to.</param>
        /// <returns>A <c>Vector3</c> of the position of the unit magnet in the scene.</returns>
        public Vector3 GetUnitMagnetPosition(Faction faction) => m_UnitMagnets[(int)faction].transform.position;

        /// <summary>
        /// Sets the unit magnet of the given faction to the position of the given terrain point.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the unit magnet belongs to.</param>
        /// <param name="point">The <c>TerrainPoint</c> that the unit magnet should be placed at.</param>
        public void PlaceUnitMagnetAtPoint(Faction faction, TerrainPoint point)
            => m_UnitMagnets[(int)faction].MoveToPoint(point);

        #endregion


        #region Snap Camera to Object

        /// <summary>
        /// Sends the camera of the player of the given team to the location of the inspected object.
        /// </summary>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToInspectedObject_ServerRpc(Faction team, ServerRpcParams serverRpcParams = default)
        {
            IInspectableObject inspected = GetInspectedObject(team);

            if (inspected == null)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.INSPECTED_OBJECT);
                return;
            }

            SetCameraLookPosition_ClientRpc(
                inspected.GameObject.transform.position,
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of the unit magnet.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToUnitMagnet_ServerRpc(Faction team, ServerRpcParams serverRpcParams = default)
        {
            SetCameraLookPosition_ClientRpc(
                GetUnitMagnetPosition(team),
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of the team's leader.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToLeader_ServerRpc(Faction team, ServerRpcParams serverRpcParams = default)
        {
            ILeader leader = GetLeader(team);

            if (leader == null)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.LEADER);
                return;
            }

            SetCameraLookPosition_ClientRpc(
                leader.GameObject.transform.position,
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of one of the team's settlements, cycling through them on repeated calls.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToSettlements_ServerRpc(Faction team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Vector3? position = StructureManager.Instance.GetSettlementLocation(team, m_SettlementIndex[teamIndex]);
            if (!position.HasValue)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.SETTLEMENT);
                return;
            }

            SetCameraLookPosition_ClientRpc(
                new(position.Value.x, Terrain.Instance.WaterLevel, position.Value.z),
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );

            m_SettlementIndex[teamIndex] = GameUtils.GetNextArrayIndex(m_SettlementIndex[teamIndex], 1, StructureManager.Instance.GetSettlementsNumber(team));
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of one of the ongoing fights, cycling through them on repeated calls.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToFights_ServerRpc(Faction team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Vector3? position = UnitManager.Instance.GetFightLocation(m_FightIndex[(int)team]);
            if (!position.HasValue)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.FIGHT);
                return;
            }

            SetCameraLookPosition_ClientRpc(
                new(position.Value.x, Terrain.Instance.WaterLevel, position.Value.z),
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );

            m_FightIndex[teamIndex] = GameUtils.GetNextArrayIndex(m_FightIndex[teamIndex], 1, UnitManager.Instance.GetFightsNumber());
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of one of the team's knights, cycling through them on repeated calls.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SnapToKnights_ServerRpc(Faction team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Unit knight = UnitManager.Instance.GetKnight(team, m_KnightsIndex[teamIndex]);
            if (!knight)
            {
                NotifyCannotSnap_ClientRpc(SnapTo.KNIGHT);
                return;
            }

            Vector3 position = knight.transform.position;

            SetCameraLookPosition_ClientRpc(
                new(position.x, Terrain.Instance.WaterLevel, position.z),
                GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId)
            );

            m_KnightsIndex[teamIndex] = GameUtils.GetNextArrayIndex(m_KnightsIndex[teamIndex], 1, UnitManager.Instance.GetKnightsNumber(team));
        }

        /// <summary>
        /// Sets the position of the follow target, and thus sets the point where the camera is looking.
        /// </summary>
        /// <param name="position">The new position of the follow target.</param>
        /// <param name="clientRpcParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        public void SetCameraLookPosition_ClientRpc(Vector3 position, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log("Camera");
            PlayerCamera.Instance.SetCameraLookPosition(position);
        }

        /// <summary>
        /// Triggers the client's UI to show that snapping the camera to the given object was impossible.
        /// </summary>
        /// <param name="snapOption">The camera snapping option that was attempted.</param>
        [ClientRpc]
        private void NotifyCannotSnap_ClientRpc(SnapTo snapOption)
            => GameUI.Instance.NotifyCannotSnapTo(snapOption);

        #endregion


        #region Leader

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

            Debug.Log("Set Leader");
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


        #region Divine Interventions

        #region Manna

        /// <summary>
        /// Adds manna to the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> manna should be added to.</param>
        /// <param name="amount">The amount of manna to be added.</param>
        public void AddManna(Faction team, int amount = 1) => SetManna(team, Mathf.Clamp(m_Manna[(int)team] + amount, 0, m_MaxManna));

        /// <summary>
        /// Removes manna from the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> manna should be removed from.</param>
        /// <param name="amount">The amount of manna to be removed.</param>
        public void RemoveManna(Faction team, int amount = 1) => SetManna(team, Mathf.Clamp(m_Manna[(int)team] - amount, 0, m_MaxManna));

        /// <summary>
        /// Sets the manna of the given team to the given amount.
        /// </summary>
        /// <param name="faction">The <c>Team</c> whose manna should be set.</param>
        /// <param name="amount">The amount of manna the given team should have.</param>
        private void SetManna(Faction faction, int amount)
        {
            if (amount == m_Manna[(int)faction]) return;

            m_Manna[(int)faction] = amount;

            int activePowers = -1;
            foreach (float threshold in m_PowerActivationPercent)
            {
                if (amount < threshold * m_MaxManna) break;
                activePowers++;
            }

            //UpdateMannaUI_ClientRpc(
            //    amount, 
            //    activePowers, 
            //    GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(faction))
            //);
        }

        /// <summary>
        /// Triggers the update of a player's UI with manna and power information.
        /// </summary>
        /// <param name="manna">The amount of manna the player has.</param>
        /// <param name="activePowers">The number of active powers the player has.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateMannaUI_ClientRpc(int manna, int activePowers, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateMannaBar(manna, activePowers);

        /// <summary>
        /// Checks whether the player from the given team can use the given power.
        /// </summary>
        /// <param name="faction">The <c>Team</c> the player controls.</param>
        /// <param name="power">The <c>Power</c> the player wants to activate.</param>
        [ServerRpc(RequireOwnership = false)]
        public void TryActivatePower_ServerRpc(Faction faction, Power power)
        {
            bool powerActivated = true;

            //if (m_Manna[(int)faction] < m_PowerActivationPercent[(int)power] * m_MaxManna ||
            //    (power == Power.KNIGHT && !HasLeader(faction)) ||
            //    (power == Power.FLOOD && Terrain.Instance.HasReachedMaxWaterLevel()))
            //    powerActivated = false;

            if (powerActivated)
            {
                if (power == Power.KNIGHT)
                    CreateKnight(faction);

                if (power == Power.FLOOD)
                    CauseFlood(faction);

                if (power == Power.ARMAGEDDON)
                    StartArmageddon(faction);
            }

            SendPowerActivationInfo_ClientRpc(
                power,
                powerActivated,
                GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(faction))
            );
        }

        /// <summary>
        /// Notifies the player whether the power they want to activate is activated or not.
        /// </summary>
        /// <param name="power">The <c>Power</c> the player wants to activate.</param>
        /// <param name="isActivated">True if the power is activated, false otherwise.</param>
        /// <param name="clientParams">RPC info for the client RPC.</param>
        [ClientRpc]
        private void SendPowerActivationInfo_ClientRpc(Power power, bool isActivated, ClientRpcParams clientParams = default)
            => PlayerController.Instance.ReceivePowerActivationInfo(power, isActivated);

        #endregion


        /// <summary>
        /// Triggers the execution of the Mold Terrain Divine Intervention.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> that should be modified.</param>
        /// <param name="lower">True if the point should be lowered, false if the point should be elevated.</param>
        [ServerRpc(RequireOwnership = false)]
        public void MoldTerrain_ServerRpc(TerrainPoint point, bool lower)
            => MoldTerrain_ClientRpc(point, lower);
        
        /// <summary>
        /// Executes the Mold Terrain power on the client.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> that should be modified.</param>
        /// <param name="lower">True if the point should be lowered, false if the point should be elevated.</param>
        [ClientRpc]
        private void MoldTerrain_ClientRpc(TerrainPoint point, bool lower)
            => RespondToTerrainChange(Terrain.Instance.ModifyTerrain(point, lower));

        public void MoldTerrain(TerrainPoint point, bool lower)
            => RespondToTerrainChange(Terrain.Instance.ModifyTerrain(point, lower));


        /// <summary>
        /// Executes the Place Unit Magnet Divine Intervention.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> on which the magnet should be set.</param>
        /// <param name="faction">The <c>Faction</c> whose magnet should be moved.</param>
        [ServerRpc(RequireOwnership = false)]
        public void PlaceUnitMagnet_ServerRpc(Faction faction, TerrainPoint point)
        {
            // the magnet can only be moved if there is a leader
            if (!HasLeader(faction)) return;

            RemoveManna(faction, m_PowerMannaCost[(int)Power.PLACE_MAGNET]);

            PlaceUnitMagnetAtPoint(faction, point);

            if (UnitManager.Instance.GetActiveBehavior(faction) != UnitBehavior.GO_TO_MAGNET)
                return;

            if (faction == Faction.RED)
                OnRedMagnetMoved?.Invoke();
            else if (faction == Faction.BLUE)
                OnBlueMagnetMoved?.Invoke();
        }


        /// <summary>
        /// Triggers the execution of the Earthquake Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the earthquake.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateEarthquake_ServerRpc(Faction faction, TerrainPoint center)
        {
            RemoveManna(faction, m_PowerMannaCost[(int)Power.EARTHQUAKE]);
            CreateEarthquake_ClientRpc(center, new Random().Next());
        }
        
        /// <summary>
        /// Executes the Earthquake Divine Intervention on the client.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the earthquake.</param>
        /// <param name="randomizerSeed">The seed used for the randomizer that sets the heights in the earthquake area.</param>
        [ClientRpc]
        private void CreateEarthquake_ClientRpc(TerrainPoint center, int randomizerSeed)
            => RespondToTerrainChange(Terrain.Instance.CauseEarthquake(center, m_EarthquakeRadius, randomizerSeed));


        /// <summary>
        /// Executes the Swamp Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the swamp.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateSwamp_ServerRpc(Faction faction, TerrainPoint center)
        {
            RemoveManna(faction, m_PowerMannaCost[(int)Power.SWAMP]);

            // gets all the flat tiles in the swamp area
            List<TerrainTile> flatTiles = new();
            for (int z = -m_SwampRadius; z < m_SwampRadius; ++z)
            {
                for (int x = -m_SwampRadius; x < m_SwampRadius; ++x)
                {
                    TerrainTile neighborTile = new(center.X + x, center.Z + z);

                    if (!neighborTile.IsInBounds() || !neighborTile.IsFlat())
                        continue;

                    Structure structure = StructureManager.Instance.GetStructureOnTile(neighborTile);

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Rock) || structureType == typeof(Tree) ||
                            structureType == typeof(Swamp) || structureType == typeof(Settlement))
                            continue;
                    }

                    flatTiles.Add(neighborTile);
                }
            }

            // randomly places swamps in the area, using shuffle algorithm
            Random random = new();
            List<int> tiles = Enumerable.Range(0, flatTiles.Count).ToList();
            int swampTiles = random.Next(Mathf.RoundToInt(flatTiles.Count * 0.5f), flatTiles.Count);

            HashSet<Settlement> affectedSettlements = new();
            int count = tiles.Count;
            foreach (TerrainTile flatTile in flatTiles)
            {
                count--;
                int randomIndex = random.Next(count + 1);
                (tiles[count], tiles[randomIndex]) = (tiles[randomIndex], tiles[count]);

                if (tiles[count] <= swampTiles)
                {
                    Structure structure = StructureManager.Instance.GetStructureOnTile(flatTile);

                    if (structure && structure.GetType() == typeof(Field))
                    {
                        // gets settlements whose fields were destroyed
                        affectedSettlements.UnionWith(((Field)structure).SettlementsServed);
                        StructureManager.Instance.DespawnStructure(structure.gameObject);
                    }
                    else if (structure)
                        continue;

                    StructureManager.Instance.SpawnSwamp(flatTile);
                }
            }
            
            // updates settlements whose fields were destroyed
            foreach (Settlement settlement in affectedSettlements)
                settlement.SetType();
        }


        /// <summary>
        /// Executes the Knight Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> that should gain a knight.</param>
        public void CreateKnight(Faction faction)
        {
            // the team has to have a leader to turn into a knight.
            if (!HasLeader(faction)) return;

            RemoveManna(faction, m_PowerMannaCost[(int)Power.KNIGHT]);
            
            Unit knight = null;

            // if the leader is in a unit, just turn that unit into a knight
            if (HasLeaderUnit(faction))
            {
                knight = GetLeaderUnit(faction);
                RemoveLeader(faction);
                UnitManager.Instance.SetKnight(faction, knight);
            }

            // if the leader is in a origin, destroy that origin and spawnPoint a knight in its position
            if (HasLeaderSettlement(faction))
            {
                Settlement settlement = GetLeaderSettlement(faction);
                RemoveLeader(faction);

                knight = UnitManager.Instance.SpawnUnit(
                    location: new(settlement.OccupiedTile.X, settlement.OccupiedTile.Z),
                    faction,
                    type: UnitType.KNIGHT,
                    strength: settlement.FollowersInSettlement,
                    origin: settlement
                ).GetComponent<Unit>();

                settlement.Destroy(updateNearbySettlements: true);
            }

            PlaceUnitMagnetAtPoint(faction, knight.ClosestTerrainPoint);

            // show the knight
            SetCameraLookPosition_ClientRpc(
                new(knight.transform.position.x, 0, knight.transform.position.z),
                GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(faction))
            );
        }


        /// <summary>
        /// Triggers the execution of the Volcano Divine Intervention.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the volcano.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateVolcano_ServerRpc(Faction faction, TerrainPoint center)
        {
            RemoveManna(faction, m_PowerMannaCost[(int)Power.VOLCANO]);

            CreateVolcano_ClientRpc(center);
            StructureManager.Instance.PlaceVolcanoRocks(center, m_VolcanoRadius);
        }
        
        /// <summary>
        /// Executes the Volcano Divine Intervention on the client.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the volcano.</param>
        [ClientRpc]
        private void CreateVolcano_ClientRpc(TerrainPoint center)
            => RespondToTerrainChange(Terrain.Instance.CauseVolcano(center, m_VolcanoRadius));


        /// <summary>
        /// Triggers the execution of the Flood Divine Intervention on the clients.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that triggered the action.</param>
        public void CauseFlood(Faction faction)
        {
            if (Terrain.Instance.HasReachedMaxWaterLevel()) return;

            RemoveManna(faction, m_PowerMannaCost[(int)Power.FLOOD]);

            CauseFlood_ClientRpc();
            OnFlood?.Invoke();
        }
        
        /// <summary>
        /// Executes the Flood Divine Intervention on the client.
        /// </summary>
        [ClientRpc]
        private void CauseFlood_ClientRpc()
        {
            Terrain.Instance.RaiseWaterLevel();
            Water.Instance.Raise();
            BorderWalls.Instance.UpdateAllWalls();
            Minimap.Instance.SetTexture();
        }


        /// <summary>
        /// Executes the Armageddon Divine Intervention.
        /// </summary>
        /// <param name="team">The <c>Faction</c> of the player that triggered the action.</param>
        public void StartArmageddon(Faction team)
        {
            RemoveManna(team, m_PowerMannaCost[(int)Power.ARMAGEDDON]);

            m_IsArmageddon = true;

            foreach (Faction teams in Enum.GetValues(typeof(Faction)))
            {
                if (teams == Faction.NONE) break;

                PlaceUnitMagnetAtPoint(teams, Terrain.Instance.TerrainCenter);
                UnitManager.Instance.ChangeUnitBehavior_ServerRpc(teams, UnitBehavior.GO_TO_MAGNET);
            }

            // destroy all settlements
            OnArmageddon?.Invoke();
        }

        #endregion


        /// <summary>
        /// Updates the terrain accessories, the structures, the units, and the unit magnets after a terrain modification.
        /// </summary>
        /// <param name="modifiedAreaCorners">A tuple of the <c>TerrainPoint</c> at the bottom left and the <c>TerrainPoint</c> on the top right
        /// of a rectangular area containing all the points whose heights were changed in the terrain modification.</param>
        public void RespondToTerrainChange((TerrainPoint bottomLeft, TerrainPoint topRight) modifiedAreaCorners)
        {
            BorderWalls.Instance.UpdateWallsInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);
            Minimap.Instance.UpdateTextureInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);

            if (!IsHost) return;

            Debug.Log("Respond");
            StructureManager.Instance.UpdateStructuresInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);
            OnTerrainModified?.Invoke(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight); // for units

            foreach (UnitMagnet magnet in m_UnitMagnets)
            {
                if (magnet.GridLocation.X >= modifiedAreaCorners.bottomLeft.X || magnet.GridLocation.X <= modifiedAreaCorners.topRight.X ||
                    magnet.GridLocation.Z >= modifiedAreaCorners.bottomLeft.Z || magnet.GridLocation.Z <= modifiedAreaCorners.topRight.Z)
                    magnet.UpdateHeight();
            }
        }

        /// <summary>
        /// Notifies the client that the object with the given ID is not in the camera's field of view anymore.
        /// </summary>
        /// <remarks>Used when an object is despawned.</remarks>
        /// <param name="objectId">The network ID of the object that is not visible anymore.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        public void RemoveVisibleObject_ClientRpc(ulong objectId, ClientRpcParams clientParams = default)
            => CameraDetectionZone.Instance.RemoveVisibleObject(objectId);


        #region Query Mode

        /// <summary>
        /// Gets the object the player of the given faction is inspecting.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose inspected object should be returned.</param>
        /// <returns>The <c>IInspectableObject</c> that the player is inspecting, null if there is none.</returns>
        public IInspectableObject GetInspectedObject(Faction faction) => m_InspectedObjects[(int)faction];

        /// <summary>
        /// Gets the faction of the player that is inspecting the given object.
        /// </summary>
        /// <param name="inspectedObject">The object that is being checked.</param>
        /// <returns>The value of the faction whose player is inspecting the object in the <c>Faction</c> enum, -1 if the object is not being inspected.</returns>
        public int GetPlayerInspectingObject(IInspectableObject inspectedObject) => Array.IndexOf(m_InspectedObjects, inspectedObject);

        /// <summary>
        /// Sets the given object as the object being inspected by the player of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> of the player that's inspecting the object.</param>
        /// <param name="inspectedObject">A <c>NetworkObjectReference</c> of the object being inspected.</param>
        /// <param name="serverRpcParams">RPC parameters for the server RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SetInspectedObject_ServerRpc(Faction faction, NetworkObjectReference inspectedObject, ServerRpcParams serverRpcParams = default)
        {
            if (!inspectedObject.TryGet(out NetworkObject networkObject) || networkObject.GetComponent<IInspectableObject>() == null)
                return;

            IInspectableObject inspectObject = networkObject.GetComponent<IInspectableObject>();
            IInspectableObject lastInspectedObject = m_InspectedObjects[(int)faction];

            // stop inspecting the last inspected object
            if (lastInspectedObject != null)
            {
                lastInspectedObject.SetHighlight(false);
                m_InspectedObjects[(int)faction] = null;
                lastInspectedObject.IsInspected = false;
                HideInspectedObjectPanel_ClientRpc(GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId));
            }

            // this will make it so clicking again on an inspected object will just stop inspecting it.
            if (lastInspectedObject == inspectObject) return;

            ClientRpcParams clientParams = GameUtils.GetClientParams(serverRpcParams.Receive.SenderClientId);

            m_InspectedObjects[(int)faction] = inspectObject;
            inspectObject.IsInspected = true;

            if (inspectObject.GetType() == typeof(Unit))
            {
                Unit unit = (Unit)inspectObject;

                if (unit.IsInFight)
                {
                    (Unit red, Unit blue) = UnitManager.Instance.GetFightParticipants(unit.FightId);
                    ShowFightData_ClientRpc(red.Strength, blue.Strength, clientParams);
                    return;
                }

                ShowUnitData_ClientRpc(unit.Faction, unit.Type, unit.Strength, clientParams);
            }

            if (inspectObject.GetType() == typeof(Settlement))
            {
                Settlement settlement = (Settlement)inspectObject;
                ShowSettlementData_ClientRpc(settlement.Faction, settlement.Type, settlement.FollowersInSettlement, settlement.Capacity, clientParams);
            }
        }

        /// <summary>
        /// Stops inspecting the given object, if any player is inspecting it.
        /// </summary>
        /// <remarks>Called when the object is despawned.</remarks>
        /// <param name="inspectedObject">The object that should be removed from being inspected.</param>
        public void RemoveInspectedObject(IInspectableObject inspectedObject)
        {
            int index = GetPlayerInspectingObject(inspectedObject);
            // nobody is inspecting the object
            if (index < 0) return;

            m_InspectedObjects[index] = null;
            inspectedObject.IsInspected = false;

            HideInspectedObjectPanel_ClientRpc(GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(index)));
        }


        #region Show/Hide Inspected Object UI

        /// <summary>
        /// Tells the client to show the given data on the Inspected Unit panel.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the unit belongs to.</param>
        /// <param name="type">The type of the unit.</param>
        /// <param name="strength">The current strength of the unit.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void ShowUnitData_ClientRpc(Faction faction, UnitType type, int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowUnitData(faction, type, strength);

        /// <summary>
        /// Tells the client to show the given data on the Inspected Settlement panel.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the settlement belongs to.</param>
        /// <param name="type">The type of the settlement.</param>
        /// <param name="unitsInSettlement">The number of units currently in the settlement.</param>
        /// <param name="maxUnitsInSettlement">The maximum number of units for the settlement.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void ShowSettlementData_ClientRpc(Faction faction, SettlementType type, int unitsInSettlement, int maxUnitsInSettlement, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowSettlementData(faction, type, unitsInSettlement, maxUnitsInSettlement);

        /// <summary>
        /// Tells the client to show the given data on the Inspected Fight panel.
        /// </summary>
        /// <param name="redStrength">The current strength of the red unit in the fight.</param>
        /// <param name="blueStrength">The current strength of the blue unit in the fight.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void ShowFightData_ClientRpc(int redStrength, int blueStrength, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowFightData(redStrength, blueStrength);

        /// <summary>
        /// Tells the client to hide the inspected object panel.
        /// </summary>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void HideInspectedObjectPanel_ClientRpc(ClientRpcParams clientParams = default) 
            => GameUI.Instance.HideInspectedObjectPanel();

        #endregion


        #region Update Inspected UI

        /// <summary>
        /// Handles the update of the given unit data on the UI of the player focusing on the unit.
        /// </summary>
        /// <param name="unit">The unit whose UI data should be updated.</param>
        /// <param name="updateType">True if the unit's type should be updated, false otherwise.</param>
        /// <param name="updateStrength">True if the unit's strength should be updated, false otherwise.</param>
        public void UpdateInspectedUnit(Unit unit, bool updateType = false, bool updateStrength = false)
        {
            IEnumerable<int> playerIndices = Enumerable.Range(0, m_InspectedObjects.Length)
                .Where(i => m_InspectedObjects[i].GameObject == unit.GameObject);

            if (playerIndices.Count() == 0) return;

            foreach (int playerIndex in playerIndices)
            {
                ClientRpcParams clientParams = GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(playerIndex));

                if (updateType)
                    UpdateInspectedUnitType_ClientRpc(unit.Type, clientParams);

                if (updateStrength)
                    UpdateInspectedUnitStrength_ClientRpc(unit.Strength, clientParams);
            }
        }

        /// <summary>
        /// Triggers the update of the inspected unit's type on the UI of the client.
        /// </summary>
        /// <param name="type">The new type that should be set.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedUnitType_ClientRpc(UnitType type, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateUnitType(type);

        /// <summary>
        /// Triggers the update of the inspected unit's strength on the UI of the client.
        /// </summary>
        /// <param name="strength">The new strength that should be set.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedUnitStrength_ClientRpc(int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateUnitStrength(strength);


        /// <summary>
        /// Handles the update of the given settlement data on the UI of the player focusing on the settlement.
        /// </summary>
        /// <param name="settlement">The settlement whose UI data should be updated.</param>
        /// <param name="updateTeam">True if the settlement's team should be updated, false otherwise.</param>
        /// <param name="updateType">True if the settlement's type should be updated, false otherwise.</param>
        /// <param name="updateFollowers">True if the amount of followers in the settlement should be updated, false otherwise.</param>
        public void UpdateInspectedSettlement(Settlement settlement, bool updateTeam = false, bool updateType = false, bool updateFollowers = false)
        {
            IEnumerable<int> playerIndices = Enumerable.Range(0, m_InspectedObjects.Length)
                .Where(i => m_InspectedObjects[i].GameObject == settlement.GameObject);

            if (playerIndices.Count() == 0) return;

            foreach (int playerIndex in playerIndices)
            {
                ClientRpcParams clientParams = GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(playerIndex));

                if (updateTeam)
                    UpdateInspectedSettlementFaction_ClientRpc(settlement.Faction, clientParams);

                if (updateType)
                    UpdateInspectedSettlementType_ClientRpc(settlement.Type, settlement.Capacity, clientParams);

                if (updateFollowers)
                    UpdateInspectedSettlementFollowers_ClientRpc(settlement.FollowersInSettlement, clientParams);
            }
        }

        /// <summary>
        /// Triggers the update of the inspected settlement's faction on the UI of the client.
        /// </summary>
        /// <param name="faction">The new <c>Faction</c> of the settlement.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedSettlementFaction_ClientRpc(Faction faction, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateSettlementTeam(faction);

        /// <summary>
        /// Triggers the update of the inspected settlement's type on the UI of the client.
        /// </summary>
        /// <param name="type">The new type of the settlement.</param>
        /// <param name="maxUnitsInSettlement">The new maximum amount of units in the settlement.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedSettlementType_ClientRpc(SettlementType type, int maxUnitsInSettlement, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateSettlementType(type, maxUnitsInSettlement);

        /// <summary>
        /// Triggers the update of the number of followers in the inspected settlement on the UI of the client.
        /// </summary>
        /// <param name="followers">The new number of followers.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedSettlementFollowers_ClientRpc(int followers, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateSettlementFollowers(followers);


        /// <summary>
        /// Handles the update of the strengths of the given units, which are involved in a fight.
        /// </summary>
        /// <param name="red">The <c>Unit</c> of the Red faction in the fight.</param>
        /// <param name="blue">The <c>Unit</c> of the Blue faction in the fight.</param>
        public void UpdateInspectedFight(Unit red, Unit blue)
        {
            IEnumerable<int> playerIndices = Enumerable.Range(0, m_InspectedObjects.Length)
                .Where(i => m_InspectedObjects[i].GameObject == red.GameObject || m_InspectedObjects[i].GameObject == blue.GameObject);

            if (playerIndices.Count() == 0) return;

            foreach (int playerIndex in playerIndices)
                UpdateInspectedFight_ClientRpc(red.Strength, blue.Strength, GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(playerIndex)));
        }

        /// <summary>
        /// Triggers the update of the strengths of the units in the inspected fight.
        /// </summary>
        /// <param name="redStrength">The strength of the Red unit in the fight.</param>
        /// <param name="blueStrength">The strength of the Blue unit in the fight.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedFight_ClientRpc(int redStrength, int blueStrength, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateFight(redStrength, blueStrength);

        #endregion

        #endregion
    }
}