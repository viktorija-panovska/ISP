using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The <c>GameController</c> class controls the flow of the game and the executes the players' actions or passes them along to the systems that can execute them.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class GameController : NetworkBehaviour
    {
        #region Inspector Fields

        [Tooltip("The marker objects corresponding to each power. The index of each marker in this array corresponds to the value of its corresponding power in the Power enum.")]
        [SerializeField] private Color[] m_TeamColors;


        [Header("Manna")]

        [Tooltip("The maximum amount of manna a player can have.")]
        [SerializeField] private int m_MaxManna = 100;

        [Tooltip("The percentage of manna that is required to be full in order to have access to the Power with the value of the index in the Power enum.")]
        [SerializeField] private float[] m_PowerActivationPercent = new float[Enum.GetNames(typeof(Power)).Length];

        [Tooltip("The amount of manna spent after using the Power with the value of the index in the Power enum.")]
        [SerializeField] private int[] m_PowerMannaCost = new int[Enum.GetNames(typeof(Power)).Length];


        [Header("Powers")]

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
        /// An array of the colors of each team. 
        /// </summary>
        /// <remarks>The color at each index is the color of the team with that index.</remarks>
        public Color[] TeamColors { get => m_TeamColors; }


        #region Manna

        /// <summary>
        /// Gets the maximum amount of manna a player can have.
        /// </summary>
        public int MaxManna { get => m_MaxManna; }
        /// <summary>
        /// An array of the amount of manna each team has.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly int[] m_Manna = new int[2];

        #endregion


        #region Powers

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

        #endregion


        #region Inspected Object

        /// <summary>
        /// Each cell represents one of the teams, and designates whether the Inspect Mode is active or not for that team.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly bool[] m_IsInspectModeActive = new bool[2];
        /// <summary>
        /// Each cell represents one of the teams, and the object in the cell is the object that team's player is inspecting.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly IInspectableObject[] m_InspectedObjects = new IInspectableObject[2];

        #endregion


        #region Camera Snap

        /// <summary>
        /// An array containing the index of the next fight the player's camera will snap to if the Zoom to Fight action is performed.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
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
            TerrainBorderWalls.Instance.Create();

            //if (!IsHost) return;

            //// just on server
            //StructureManager.Instance.SpawnUnitMagnets();
            //StructureManager.Instance.PlaceTreesAndRocks();
            //UnitManager.Instance.SpawnStartingUnits();
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
        /// <param name="winner">The <c>Team</c> that won the game.</param>
        [ClientRpc]
        public void EndGame_ClientRpc(Team winner) => PlayerController.Instance.EndGame(winner);

        #endregion


        /// <summary>
        /// Notifies that the object with the given ID is not visible anymore.
        /// </summary>
        /// <remarks>Used when an object is despawned.</remarks>
        /// <param name="objectId">The ID of the object that is not visible anymore.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        public void RemoveVisibleObject_ClientRpc(ulong objectId, ClientRpcParams clientParams = default)
            => CameraDetectionZone.Instance.RemoveVisibleObject(objectId);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modifiedAreaCorners"></param>
        public void RespondToTerrainUpdate((TerrainPoint bottomLeft, TerrainPoint topRight) modifiedAreaCorners)
        {
            TerrainBorderWalls.Instance.UpdateWallsInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);
            Minimap.Instance.UpdateTextureInArea(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);

            // for units and structures
            OnTerrainModified?.Invoke(modifiedAreaCorners.bottomLeft, modifiedAreaCorners.topRight);
        }


        #region Leader

        /// <summary>
        /// Checks whether the given team has a leader.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be checked.</param>
        /// <returns>True if the team has a leader, false otherwise.</returns>
        public bool HasLeader(Team team) => UnitManager.Instance.HasUnitLeader(team) || StructureManager.Instance.HasSettlementLeader(team);

        /// <summary>
        /// Gets the <c>GameObject</c> of the leader of the team, regardless of whether it is part of a unit or a settlement.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be returned.</param>
        /// <returns>The <c>GameObject</c> of the team's leader, null if the team doesn't have a leader.</returns>
        public GameObject GetLeaderObject(Team team)
        {
            if (UnitManager.Instance.HasUnitLeader(team))
                return UnitManager.Instance.GetLeaderUnit(team).gameObject;

            if (StructureManager.Instance.HasSettlementLeader(team))
                return StructureManager.Instance.GetLeaderSettlement(team).gameObject;

            return null;
        }

        #endregion


        #region Camera Snap

        /// <summary>
        /// Sends the camera of the player of the given team to the location of the inspected object.
        /// </summary>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowInspectedObject_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            IInspectableObject inspected = GetInspectedObject(team);

            if (inspected == null)
            {
                NotifyCannotSnap_ClientRpc(CameraSnap.INSPECTED_OBJECT);
                return;
            }

            SetCameraLookPosition_ClientRpc(inspected.GameObject.transform.position, new()
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of the unit magnet.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowMagnet_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            SetCameraLookPosition_ClientRpc(StructureManager.Instance.GetMagnetPosition(team), new()
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of the team's leader.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowLeader_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            GameObject leader = GetLeaderObject(team);

            if (!leader)
            {
                NotifyCannotSnap_ClientRpc(CameraSnap.LEADER);
                return;
            }

            SetCameraLookPosition_ClientRpc(leader.transform.position, new()
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of one of the team's settlements, cycling through them on repeated calls.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowSettlements_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Vector3? position = StructureManager.Instance.GetSettlementLocation(m_SettlementIndex[teamIndex], team);
            if (!position.HasValue)
            {
                NotifyCannotSnap_ClientRpc(CameraSnap.SETTLEMENT);
                return;
            }

            SetCameraLookPosition_ClientRpc(new(position.Value.x, 0, position.Value.z), new()
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );

            m_SettlementIndex[teamIndex] = GameUtils.GetNextArrayIndex(m_SettlementIndex[teamIndex], 1, StructureManager.Instance.GetSettlementsNumber(team));
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of one of the ongoing fights, cycling through them on repeated calls.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowFights_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Vector3? position = UnitManager.Instance.GetFightLocation(m_FightIndex[(int)team]);
            if (!position.HasValue)
            {
                NotifyCannotSnap_ClientRpc(CameraSnap.FIGHT);
                return;
            }

            SetCameraLookPosition_ClientRpc(new(position.Value.x, 0, position.Value.z), new()
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );

            m_FightIndex[teamIndex] = GameUtils.GetNextArrayIndex(m_FightIndex[teamIndex], 1, UnitManager.Instance.GetFightsNumber());
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of one of the team's knights, cycling through them on repeated calls.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowKnights_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Unit knight = UnitManager.Instance.GetKnight(team, m_KnightsIndex[teamIndex]);
            if (!knight)
            {
                NotifyCannotSnap_ClientRpc(CameraSnap.KNIGHT);
                return;
            }

            Vector3 knightPosition = knight.transform.position;

            SetCameraLookPosition_ClientRpc(new(knightPosition.x, 0, knightPosition.z), new()
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
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
            => CameraController.Instance.SetCameraLookPosition(position);

        /// <summary>
        /// Triggers the client's UI to show that snapping the camera to the given object was impossible.
        /// </summary>
        /// <param name="snapOption">The camera snapping option that was attempted.</param>
        [ClientRpc]
        private void NotifyCannotSnap_ClientRpc(CameraSnap snapOption) 
            => GameUI.Instance.NotifyCannotSnapCamera(snapOption);

        #endregion


        #region Manna

        /// <summary>
        /// Adds manna to the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> manna should be added to.</param>
        /// <param name="amount">The amount of manna to be added.</param>
        public void AddManna(Team team, int amount = 1) => SetManna(team, Mathf.Clamp(m_Manna[(int)team] + amount, 0, m_MaxManna));

        /// <summary>
        /// Removes manna from the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> manna should be removed from.</param>
        /// <param name="amount">The amount of manna to be removed.</param>
        public void RemoveManna(Team team, int amount = 1) => SetManna(team, Mathf.Clamp(m_Manna[(int)team] - amount, 0, m_MaxManna));

        /// <summary>
        /// Sets the manna of the given team to the given amount.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose manna should be set.</param>
        /// <param name="amount">The amount of manna the given team should have.</param>
        private void SetManna(Team team, int amount)
        {
            if (amount == m_Manna[(int)team]) return;

            m_Manna[(int)team] = amount;

            int activePowers = -1;
            foreach (float threshold in m_PowerActivationPercent)
            {
                if (amount < threshold * m_MaxManna) break;
                activePowers++;
            }

            UpdateMannaUI_ClientRpc(amount, activePowers, new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(team) }
                    }
                }
            );
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
        /// <param name="team">The <c>Team</c> the player controls.</param>
        /// <param name="power">The <c>Power</c> the player wants to activate.</param>
        [ServerRpc(RequireOwnership = false)]
        public void TryActivatePower_ServerRpc(Team team, Power power)
        {
            bool powerActivated = true;

            if (m_Manna[(int)team] < m_PowerActivationPercent[(int)power] * m_MaxManna ||
                (power == Power.KNIGHT && !HasLeader(team)) ||
                (power == Power.FLOOD && Terrain.Instance.HasReachedMaxWaterLevel()))
                powerActivated = false;

            if (powerActivated)
            {
                if (power == Power.KNIGHT)
                    CreateKnight(team);

                if (power == Power.FLOOD)
                    CauseFlood(team);

                if (power == Power.ARMAGHEDDON)
                    StartArmageddon(team);
            }

            SendPowerActivationInfo_ClientRpc(power, powerActivated, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(team) }
                }
            });
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


        #region Powers

        #region Mold Terrain

        /// <summary>
        /// Triggers the execution of the Mold Terrain power on both clients.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> which should be modified.</param>
        /// <param name="lower">True if the center should be lowered, false if the center should be elevated.</param>
        [ServerRpc(RequireOwnership = false)]
        public void MoldTerrain_ServerRpc(Team team, TerrainPoint point, bool lower)
        {
            if (point.IsAtMaxHeight()) return;

            RemoveManna(team, m_PowerMannaCost[(int)Power.MOLD_TERRAIN]);
            MoldTerrain_ClientRpc(point, lower);
        }

        /// <summary>
        /// Executes the Mold Terrain power on the client.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> which should be modified.</param>
        /// <param name="lower">True if the center should be lowered, false if the center should be elevated.</param>
        [ClientRpc]
        private void MoldTerrain_ClientRpc(TerrainPoint point, bool lower)
            => RespondToTerrainUpdate(Terrain.Instance.ModifyTerrain(point, lower));

        #endregion


        #region Move Magnet

        /// <summary>
        /// Sets a new position for the unit magnet of the given team.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> on which the magnet should be set.</param>
        /// <param name="team">The <c>Team</c> whose magnet should be moved.</param>
        [ServerRpc(RequireOwnership = false)]
        public void MoveMagnet_ServerRpc(Team team, TerrainPoint point)
        {
            // the magnet can only be moved if there is a leader
            if (!HasLeader(team)) return;

            RemoveManna(team, m_PowerMannaCost[(int)Power.MOVE_MAGNET]);

            StructureManager.Instance.SetMagnetPosition/*ClientRpc*/(team, new Vector3(
                point.X * Terrain.Instance.UnitsPerTileSide,
                point.GetHeight(),
                point.Z * Terrain.Instance.UnitsPerTileSide
            ));

            if (team == Team.RED)
                OnRedMagnetMoved?.Invoke();
            else if (team == Team.BLUE)
                OnBlueMagnetMoved?.Invoke();
        }

        #endregion


        #region Earthquake

        /// <summary>
        /// Triggers the execution of the Earthquake power on both clients.
        /// </summary>
        /// <param name="team">The <c>Team</c> of the player that triggered the power.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the earthquake.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateEarthquake_ServerRpc(Team team, TerrainPoint center)
        {
            RemoveManna(team, m_PowerMannaCost[(int)Power.EARTHQUAKE]);
            CreateEarthquake_ClientRpc(center, new Random().Next());
        }

        /// <summary>
        /// Executes the Earthquake power on the client.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the earthquake.</param>
        /// <param name="randomizerSeed">The seed used for the randomizer that sets the heights in the earthquake area.</param>
        [ClientRpc]
        private void CreateEarthquake_ClientRpc(TerrainPoint center, int randomizerSeed)
            => RespondToTerrainUpdate(Terrain.Instance.CauseEarthquake(center, m_EarthquakeRadius, randomizerSeed));

        #endregion


        #region Swamp

        /// <summary>
        /// Populates the tiles in the swamp area centered on the given center randomly with swamps.
        /// </summary>
        /// <param name="team">The <c>Team</c> of the player that triggered the power.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the swamp.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateSwamp_ServerRpc(Team team, TerrainPoint center)
        {
            RemoveManna(team, m_PowerMannaCost[(int)Power.SWAMP]);

            // gets all the flat tiles in the swamp area
            List<(int, int)> flatTiles = new();
            for (int z = -m_SwampRadius; z < m_SwampRadius; ++z)
            {
                for (int x = -m_SwampRadius; x < m_SwampRadius; ++x)
                {
                    (int x, int z) neighborTile = (center.X + x, center.Z + z);
                    if (center.X + x < 0 || center.X + x >= Terrain.Instance.TilesPerSide ||
                        center.Z + z < 0 || center.Z + z >= Terrain.Instance.TilesPerSide ||
                        !(new TerrainTile(neighborTile).IsFlat()))
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
            foreach ((int x, int z) flatTile in flatTiles)
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
                settlement.SetSettlementType();
        }

        #endregion


        #region Knight

        /// <summary>
        /// Transforms the leader of the given team into a knight.
        /// </summary>
        /// <param name="team">The <c>Team</c> that should gain a knight.</param>
        public void CreateKnight(Team team)
        {
            // the team has to have a leader to turn into a knight.
            if (!HasLeader(team)) return;

            RemoveManna(team, m_PowerMannaCost[(int)Power.KNIGHT]);

            Unit knight = UnitManager.Instance.CreateKnight(team);
            StructureManager.Instance.SetMagnetPosition(team, knight.ClosestMapPoint.ToWorldPosition());

            // show the knight
            SetCameraLookPosition_ClientRpc(new(knight.transform.position.x, 0, knight.transform.position.z), new()
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(knight.Team) }
                    }
                }
            );
        }

        #endregion


        #region Volcano

        /// <summary>
        /// Triggers the execution of the Volcano power on both clients.
        /// </summary>
        /// <param name="team">The <c>Team</c> of the player that triggered the power.</param>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the volcano.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateVolcano_ServerRpc(Team team, TerrainPoint center)
        {
            RemoveManna(team, m_PowerMannaCost[(int)Power.VOLCANO]);
            CreateVolcano_ClientRpc(center);
            StructureManager.Instance.PlaceVolcanoRocks(center, m_VolcanoRadius);
        }

        /// <summary>
        /// Executes the Volcano power on the client.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the volcano.</param>
        [ClientRpc]
        private void CreateVolcano_ClientRpc(TerrainPoint center)
            => RespondToTerrainUpdate(Terrain.Instance.CauseVolcano(center, m_VolcanoRadius));

        #endregion


        #region Flood

        /// <summary>
        /// Triggers the execution of the Flood power on the clients.
        /// </summary>
        /// <param name="team">The <c>Team</c> of the player that triggered the power.</param>
        public void CauseFlood(Team team)
        {
            if (Terrain.Instance.HasReachedMaxWaterLevel())
                return;

            RemoveManna(team, m_PowerMannaCost[(int)Power.FLOOD]);
            CauseFlood_ClientRpc();
            OnFlood?.Invoke();
        }

        /// <summary>
        /// Executes the Flood power on the client.
        /// </summary>
        [ClientRpc]
        private void CauseFlood_ClientRpc()
        {
            Terrain.Instance.RaiseWaterLevel();
            Water.Instance.Raise();
            TerrainBorderWalls.Instance.UpdateAllWalls();
            Minimap.Instance.SetTexture();
        }

        #endregion


        #region Armagheddon

        /// <summary>
        /// Executes the Armageddon power.
        /// </summary>
        /// <param name="team">The <c>Team</c> of the player that triggered the power.</param>
        public void StartArmageddon(Team team)
        {
            RemoveManna(team, m_PowerMannaCost[(int)Power.ARMAGHEDDON]);

            foreach (Team teams in Enum.GetValues(typeof(Team)))
            {
                if (teams == Team.NONE) break;

                StructureManager.Instance.SetMagnetPosition(teams, Terrain.Instance.TerrainCenter.ToWorldPosition());
                UnitManager.Instance.ChangeUnitBehavior_ServerRpc(teams, UnitBehavior.GO_TO_MAGNET);
            }

            // destroy all settlements
            OnArmageddon?.Invoke();
        }

        #endregion

        #endregion


        #region Inspect Mode

        /// <summary>
        /// Sets whether the player of the given team is in Inspect Mode or not.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose player Inspect Mode should be set for.</param>
        /// <param name="isActive">True if the player is in Inspect Mode, false otherwise.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SetInspectMode_ServerRpc(Team team, bool isActive) => m_IsInspectModeActive[(int)team] = isActive;

        /// <summary>
        /// Gets whether the player of the given team is in Insepct Mode or not.
        /// </summary>
        /// <param name="team">The <c>Team</c> that we are checking the mode for.</param>
        /// <returns>True if the player is in Inspect Mode, false otherwise.</returns>
        public bool IsInspectModeActiveForTeam(Team team) => m_IsInspectModeActive[(int)team];


        #region Inspected Objects

        /// <summary>
        /// Gets the object the player of the given team is inspecting.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose inspected object should be returned.</param>
        /// <returns>The <c>IInspectableObject</c> that the player is inspecting.</returns>
        public IInspectableObject GetInspectedObject(Team team) => m_InspectedObjects[(int)team];

        /// <summary>
        /// Gets the team of the player that is inspecting the given object.
        /// </summary>
        /// <param name="inspectedObject">The object that is being checked.</param>
        /// <returns>The value of the team whose player is inspecting the object in the <c>Team</c> enum.</returns>
        public int GetPlayerInspectingObject(IInspectableObject inspectedObject) => Array.IndexOf(m_InspectedObjects, inspectedObject);

        /// <summary>
        /// Sets the given object as the object being inspected by the player of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> of the player that's inspecting the object.</param>
        /// <param name="inspectedObject">A <c>NetworkObjectReference</c> of the object being inspected.</param>
        /// <param name="serverRpcParams">RPC parameters for the server RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void SetInspectedObject_ServerRpc(Team team, NetworkObjectReference inspectedObject, ServerRpcParams serverRpcParams = default)
        {
            if (!m_IsInspectModeActive[(int)team] || !inspectedObject.TryGet(out NetworkObject networkObject) || 
                networkObject.GetComponent<IInspectableObject>() == null)
                return;

            IInspectableObject inspectObject = networkObject.GetComponent<IInspectableObject>();
            IInspectableObject lastInspectedObject = m_InspectedObjects[(int)team];

            // stop inspecting the last inspected object
            if (lastInspectedObject != null)
            {
                lastInspectedObject.SetHighlight(false);
                m_InspectedObjects[(int)team] = null;
                lastInspectedObject.IsInspected = false;
                HideInspectedObjectPanel_ClientRpc(new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                });
            }

            // this will make it so clicking again on an inspected object will just stop inspecting it.
            if (lastInspectedObject == inspectObject) return;

            ClientRpcParams clientParams = new()
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                }
            };

            m_InspectedObjects[(int)team] = inspectObject;
            inspectObject.IsInspected = true;

            if (inspectObject.GetType() == typeof(Unit))
            {
                Unit unit = (Unit)inspectObject;

                if (unit.IsInFight)
                {
                    (Unit red, Unit blue) = UnitManager.Instance.GetFightParticipants(unit.FightId);
                    ShowFightData_ClientRpc(red.Followers, blue.Followers, clientParams);
                    return;
                }

                ShowUnitData_ClientRpc(unit.Team, unit.Class, unit.Followers, clientParams);
            }

            if (inspectObject.GetType() == typeof(Settlement))
            {
                Settlement settlement = (Settlement)inspectObject;
                ShowSettlementData_ClientRpc(settlement.Team, settlement.Type, settlement.FollowersInSettlement, settlement.Capacity, clientParams);
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

            HideInspectedObjectPanel_ClientRpc(new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
                    }
                }
            );
        }

        #endregion


        #region Show Inspected UI

        /// <summary>
        /// Tells the client to show the given data on the Inspected Unit panel.
        /// </summary>
        /// <param name="team">The <c>Team</c> the unit belongs to.</param>
        /// <param name="unitClass">The class of the unit.</param>
        /// <param name="strength">The current strength of the unit.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void ShowUnitData_ClientRpc(Team team, UnitClass unitClass, int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowUnitData(team, unitClass, strength);

        /// <summary>
        /// Tells the client to show the given data on the Inspected Settlement panel.
        /// </summary>
        /// <param name="team">The <c>Team</c> the settlement belongs to.</param>
        /// <param name="type">The type of the settlement.</param>
        /// <param name="unitsInSettlement">The number of units currently in the settlement.</param>
        /// <param name="maxUnitsInSettlement">The maximum number of units for the settlement.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void ShowSettlementData_ClientRpc(Team team, SettlementType type, int unitsInSettlement, int maxUnitsInSettlement, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowSettlementData(team, type, unitsInSettlement, maxUnitsInSettlement);

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
        private void HideInspectedObjectPanel_ClientRpc(ClientRpcParams clientParams = default) => GameUI.Instance.HideInspectedObjectPanel();

        #endregion


        #region Update Inspected UI

        /// <summary>
        /// Handles the update of the given unit data on the UI of the player focusing on the unit.
        /// </summary>
        /// <param name="unit">The unit whose UI data should be updated.</param>
        /// <param name="updateClass">True if the unit's class should be updated, false otherwise.</param>
        /// <param name="updateStrength">True if the unit's strength should be updated, false otherwise.</param>
        public void UpdateInspectedUnit(Unit unit, bool updateClass = false, bool updateStrength = false)
        {
            int index = GetPlayerInspectingObject(unit);
            if (index < 0) return;

            ClientRpcParams clientParams = new()
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
                }
            };

            if (updateClass)
                UpdateInspectedUnitClass_ClientRpc(unit.Class, clientParams);

            if (updateStrength && unit.IsInFight)
                UpdateInspectedFight_ClientRpc(unit.Team, unit.Followers, clientParams);

            if (updateStrength)
                UpdateInspectedUnitStrength_ClientRpc(unit.Followers, clientParams);
        }

        /// <summary>
        /// Triggers the update of the inspected unit's class on the UI of the client.
        /// </summary>
        /// <param name="unitClass">The new class that should be set.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedUnitClass_ClientRpc(UnitClass unitClass, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateUnitClass(unitClass);

        /// <summary>
        /// Triggers the update of the inspected unit's strength on the UI of the client.
        /// </summary>
        /// <param name="strength">The new strength that should be set.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedUnitStrength_ClientRpc(int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateUnitStrength(strength);

        /// <summary>
        /// Triggers the update of the strength of the unit of the given team in the inspected fight on the UI of the client.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose unit's strength should be updated.</param>
        /// <param name="strength">The new strength of the unit.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedFight_ClientRpc(Team team, int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateFight(team, strength);


        /// <summary>
        /// Handles the update of the given settlement data on the UI of the player focusing on the settlement.
        /// </summary>
        /// <param name="settlement">The settlement whose UI data should be updated.</param>
        /// <param name="updateTeam">True if the settlement's team should be updated, false otherwise.</param>
        /// <param name="updateType">True if the settlement's type should be updated, false otherwise.</param>
        /// <param name="updateFollowers">True if the amount of followers in the settlement should be updated, false otherwise.</param>
        public void UpdateInspectedSettlement(Settlement settlement, bool updateTeam = false, bool updateType = false, bool updateFollowers = false)
        {
            int index = GetPlayerInspectingObject(settlement);
            if (index < 0) return;

            ClientRpcParams clientParams = new()
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
                }
            };

            if (updateTeam)
                UpdateInspectedSettlementTeam_ClientRpc(settlement.Team, clientParams);

            if (updateType)
                UpdateInspectedSettlementType_ClientRpc(settlement.Type, settlement.Capacity, clientParams);

            if (updateFollowers)
                UpdateInspectedSettlementFollowers_ClientRpc(settlement.FollowersInSettlement, clientParams);
        }

        /// <summary>
        /// Triggers the update of the inspected settlement's team on the UI of the client.
        /// </summary>
        /// <param name="team">The new <c>Team</c> of the settlement.</param>
        /// <param name="clientParams">RPC parameters for the client RPC.</param>
        [ClientRpc]
        private void UpdateInspectedSettlementTeam_ClientRpc(Team team, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateSettlementTeam(team);

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

        #endregion

        #endregion
    }
}