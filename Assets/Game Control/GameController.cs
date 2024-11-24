using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// Powers the player can use to influence the world.
    /// </summary>
    public enum Power
    {
        /// <summary>
        /// The power to either elevate or lower a point on the terrain.
        /// </summary>
        MOLD_TERRAIN,
        /// <summary>
        /// The power to place a beacon that the followers will flock to.
        /// </summary>
        GUIDE_FOLLOWERS,
        /// <summary>
        /// The power to lower all the points in a set area.
        /// </summary>
        EARTHQUAKE,
        /// <summary>
        /// The power to place a swamp at a point which will destroy any follower that walks into it.
        /// </summary>
        SWAMP,
        /// <summary>
        /// The power to upgrade the leader into a Knight.
        /// </summary>
        KNIGHT,
        /// <summary>
        /// The power to elevate the terrain in a set area and scatter rocks across it.
        /// </summary>
        VOLCANO,
        /// <summary>
        /// The power to increase the water height by one level.
        /// </summary>
        FLOOD,
        /// <summary>
        /// The power to 
        /// </summary>
        ARMAGHEDDON
    }


    /// <summary>
    /// The <c>GameController</c> class is a <c>MonoBehavior</c> that controls the flow of the game.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class GameController : NetworkBehaviour
    {
        [SerializeField] private Color[] m_TeamColors;

        [Header("Manna")]
        [SerializeField] private int m_MaxManna = 100;
        [SerializeField] private int[] m_PowerActivationThreshold = new int[Enum.GetNames(typeof(Power)).Length];
        [SerializeField] private int[] m_PowerMannaCost = new int[Enum.GetNames(typeof(Power)).Length];

        [Header("Powers")]
        [SerializeField] private int m_EarthquakeRadius = 3;
        [SerializeField] private int m_SwampRadius = 3;
        [SerializeField] private int m_VolcanoRadius = 3;
        [SerializeField, Range(0, 1)] private float m_VolcanoRockDensity = 0.4f;


        private static GameController m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static GameController Instance { get => m_Instance; }

        /// <summary>
        /// True if the player is the host of the game, false otherwise.
        /// </summary>
        public bool IsPlayerHosting { get => IsHost; }
        /// <summary>
        /// An array of the colors of each team. 
        /// </summary>
        /// <remarks>The color at each index is the color of the team with that index.</remarks>
        public Color[] TeamColors { get => m_TeamColors; }

        private bool m_IsPaused;
        /// <summary>
        /// True if the game is paused, false otherwise.
        /// </summary>
        public bool IsPaused { get => m_IsPaused; }

        /// <summary>
        /// An array of the leaders in each team that are part of a unit, null if the team's leader is not in a unit.
        /// </summary>
        private readonly Unit[] m_LeaderUnits = new Unit[Enum.GetValues(typeof(Team)).Length];
        /// <summary>
        /// An array of the leaders in each team that are part of a settlement, null if the team's leader is not in a settlement.
        /// </summary>
        private readonly Settlement[] m_LeaderSettlements = new Settlement[Enum.GetValues(typeof(Team)).Length];

        /// <summary>
        /// An array of the amount of manna each team has.
        /// </summary>
        private readonly int[] m_Manna = new int[Enum.GetValues(typeof(Team)).Length];

        /// <summary>
        /// Gets the radius of the area of effect of the Earthquake power, in tiles.
        /// </summary>
        public int EarthquakeRadius { get => m_EarthquakeRadius; }
        /// <summary>
        /// Gets the radius of the area of effect of the Swamp power, in tiles.
        /// </summary>
        public int SwampRadius { get => m_SwampRadius; }
        /// <summary>
        /// Gets the radius of the area of effect of the Volcano power, in tiles.
        /// </summary>
        public int VolcanoRadius { get => m_VolcanoRadius; }

        /// <summary>
        /// An array containing the index of the next fight the player's camera will focus on if the Zoom to Fight action is performed.
        /// </summary>
        private readonly int[] m_FightIndex = new int[2];
        /// <summary>
        /// An array containing the index of the next knight the player's camera will focus on if the Zoom to Knight action is performed.
        /// </summary>
        private readonly int[] m_KnightsIndex = new int[2];
        /// <summary>
        /// An array containing the index of the next settlement the player's camera will focus on if the Zoom to Settlement action is performed.
        /// </summary>
        private readonly int[] m_SettlementIndex = new int[2];

        /// <summary>
        /// Action to be called when the heights of the terrain have been modified.
        /// </summary>
        public Action OnTerrainModified;
        /// <summary>
        /// Action to be called when the symbol of the red team is moved.
        /// </summary>
        public Action OnRedSymbolMoved;
        /// <summary>
        /// Action to be called when the symbol of the blue team is moved.
        /// </summary>
        public Action OnBlueSymbolMoved;
        /// <summary>
        /// Action to be called when a flood occurs.
        /// </summary>
        public Action OnFlood;
        public Action OnArmageddon;



        private void Awake()
        {
            if (m_Instance)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            Terrain.Instance.CreateTerrain();
            StructureManager.Instance.PlaceTreesAndRocks();
            UnitManager.Instance.SpawnStartingUnits();
            StructureManager.Instance.SpawnTeamSymbols();
        }



        #region Pause Game

        /// <summary>
        /// Pauses the game if it is unpaused and unpauses the game if it is paused for both players.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void TogglePauseGameServerRpc()
        {
            m_IsPaused = !m_IsPaused;
            Time.timeScale = m_IsPaused ? 0 : 1;

            SetPauseGameClientRpc(m_IsPaused);
        }

        /// <summary>
        /// Pauses the game if it is unpaused and unpauses the game if it is paused for the client.
        /// </summary>
        /// <param name="isPaused">True if the game is paused, false otherwise.</param>
        [ClientRpc]
        private void SetPauseGameClientRpc(bool isPaused)
        {
            if (!IsHost)
                Time.timeScale = isPaused ? 0 : 1;

            if (isPaused)
                PauseMenuController.Instance.ShowPauseMenu();
            else
                PauseMenuController.Instance.HidePauseMenu();
        }

        #endregion


        #region End Game

        public void GameOver(Team loser)
        {
            Time.timeScale = 0;
            GameOverClientRpc(loser == Team.RED ? Team.BLUE : Team.RED, loser);
        }

        [ClientRpc]
        private void GameOverClientRpc(Team winner, Team loser)
        {
            if (!IsHost)
                Time.timeScale = 0;

            EndGameMenuController.Instance.ShowEndGameMenu(winner);
        }

        #endregion


        #region Leader

        /// <summary>
        /// Checks whether the given team has an active leader.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be checked.</param>
        /// <returns>True if the team has a leader, false otherwise.</returns>
        public bool HasLeader(Team team) => m_LeaderUnits[(int)team] || m_LeaderSettlements[(int)team];
        /// <summary>
        /// Checks whether the given team has a leader that is in a unit.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be checked.</param>
        /// <returns>True if the team as a leader that is in a unit, false otherwise.</returns>
        public bool HasUnitLeader(Team team) => m_LeaderUnits[(int)team];
        /// <summary>
        /// Checks whether the given team has a leader that is in a settlement.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be checked.</param>
        /// <returns>True if the team as a leader that is in a settlement, false otherwise.</returns>
        public bool IsLeaderInSettlement(Team team) => m_LeaderSettlements[(int)team];

        /// <summary>
        /// Gets the <c>GameObject</c> of the leader of the team, 
        /// regardless of whether it is part of a unit or a settlement.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be returned.</param>
        /// <returns>The <c>GameObject</c> of the team's leader, null if the team doesn't have a leader.</returns>
        public GameObject GetLeaderObject(Team team)
            => HasUnitLeader(team) ? GetLeaderUnit(team).gameObject : (IsLeaderInSettlement(team) ? GetLeaderSettlement(team).gameObject : null);
        /// <summary>
        /// Gets the <c>Unit</c> the team leader is part of, if such a unit exists.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be returned.</param>
        /// <returns>The <c>Unit</c> of the team's leader, null if the leader is not part of a unit..</returns>
        public Unit GetLeaderUnit(Team team) => m_LeaderUnits[(int)team];
        /// <summary>
        /// Gets the <c>Settlement</c> the team leader is part of, if such a settlement exists.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be returned.</param>
        /// <returns>The <c>Unit</c> of the team's leader, null if the leader is not part of a settlement..</returns>
        public Settlement GetLeaderSettlement(Team team) => m_LeaderSettlements[(int)team];

        /// <summary>
        /// Sets the given <c>GameObject</c> to be the leader of the given team.
        /// </summary>
        /// <param name="leaderObject">The <c>GameObject</c> of the new leader.</param>
        /// <param name="team">The <c>Team</c> whose leader should be set.</param>
        public void SetLeader(GameObject leaderObject, Team team)
        {
            if (!leaderObject) return;
            RemoveLeader(team);

            Unit leaderUnit = leaderObject.GetComponent<Unit>();
            if (leaderUnit)
            {
                m_LeaderUnits[(int)team] = leaderUnit;
                leaderUnit.SetClass(UnitClass.LEADER);
                UnitManager.Instance.OnNewLeaderGained?.Invoke();
                return;
            }

            Settlement settlementUnit = leaderObject.GetComponent<Settlement>();
            if (settlementUnit)
            {
                m_LeaderSettlements[(int)team] = settlementUnit;
                settlementUnit.SetLeader(true);
                UnitManager.Instance.OnNewLeaderGained?.Invoke();
            }
        }

        /// <summary>
        /// Removes the leader of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be removed.</param>
        public void RemoveLeader(Team team)
        {
            if (!HasLeader(team)) return;

            if (HasUnitLeader(team))
            {
                m_LeaderUnits[(int)team].SetClass(UnitClass.WALKER);
                m_LeaderUnits[(int)team] = null;
            }

            if (IsLeaderInSettlement(team))
            {
                m_LeaderSettlements[(int)team].SetLeader(false);
                m_LeaderSettlements[(int)team] = null;
            }
        }

        #endregion


        #region Camera

        /// <summary>
        /// Sends the camera of the player of the given team to the location of the team's symbol.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowTeamSymbolServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            Vector3 flagPosition = StructureManager.Instance.GetSymbolPosition(team);

            CameraController.Instance.SetCameraLookPositionClientRpc(
                new Vector3(flagPosition.x, 0, flagPosition.z),
                new ClientRpcParams
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
        /// If the team doesn't have a leader, sends the camera to the team's symbol.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowLeaderServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            GameObject leader = GetLeaderObject(team);

            if (!leader)
            {
                ShowTeamSymbolServerRpc(team);
                return;
            }

            Vector3 leaderPosition = leader.transform.position;
            CameraController.Instance.SetCameraLookPositionClientRpc(
                new Vector3(leaderPosition.x, 0, leaderPosition.z),
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );
        }

        /// <summary>
        /// Sends the camera of the player of the given team to the location of one 
        /// of the team's settlements, cycling through them on repeated calls.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowSettlementsServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Vector3? position = StructureManager.Instance.GetSettlementLocation(m_SettlementIndex[teamIndex], team);
            if (!position.HasValue) return;

            CameraController.Instance.SetCameraLookPositionClientRpc(
                new Vector3(position.Value.x, 0, position.Value.z),
                new ClientRpcParams
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
        /// Sends the camera of the player of the given team to the location of one 
        /// of the ongoing fights, cycling through them on repeated calls.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowFightsServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Vector3? position = UnitManager.Instance.GetFightLocation(m_FightIndex[(int)team]);
            if (!position.HasValue) return;

            CameraController.Instance.SetCameraLookPositionClientRpc(
                new Vector3(position.Value.x, 0, position.Value.z),
                new ClientRpcParams
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
        /// Sends the camera of the player of the given team to the location of one 
        /// of the team's knights, cycling through them on repeated calls. If the team
        /// doesn't have any knights, sends the camera to one of the team's settlements.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowKnightsServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Unit knight = UnitManager.Instance.GetKnight(m_KnightsIndex[teamIndex], team);
            if (!knight)
            {
                ShowSettlementsServerRpc(team);
                return;
            }

            Vector3 knightPosition = knight.transform.position;

            CameraController.Instance.SetCameraLookPositionClientRpc(
                new Vector3(knightPosition.x, 0, knightPosition.z),
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                }
            );

            m_KnightsIndex[teamIndex] = GameUtils.GetNextArrayIndex(m_KnightsIndex[teamIndex], 1, UnitManager.Instance.GetKnightsNumber(team));
        }

        //[ClientRpc]
        public void RemoveVisibleObject/*ClientRpc*/(int objectId, ClientRpcParams clientParams = default)
            => CameraDetectionZone.Instance.RemoveVisibleObject(objectId);

        #endregion


        #region Manna

        /// <summary>
        /// Adds manna to the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> manna should be added to.</param>
        /// <param name="amount">The amount of manna to be added.</param>
        public void AddManna(Team team, int amount = 1)
            => SetManna(team, Mathf.Clamp(m_Manna[(int)team] + amount, 0, m_MaxManna));

        /// <summary>
        /// Removes manna from the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> manna should be removed from.</param>
        /// <param name="amount">The amount of manna to be removed.</param>
        public void RemoveManna(Team team, int amount = 1)
            => SetManna(team, Mathf.Clamp(m_Manna[(int)team] - amount, 0, m_MaxManna));

        /// <summary>
        /// Sets the manna of the given team to the given amount.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose manna should be set.</param>
        /// <param name="amount">The amount of manna the given team should have.</param>
        private void SetManna(Team team, int amount)
        {
            if (amount == m_Manna[(int)team]) return;

            m_Manna[(int)team] = amount;

            int activePowers = 0;
            foreach (int threshold in m_PowerActivationThreshold)
            {
                if (amount > threshold) break;
                activePowers++;
            }

            //UpdateMannaUIClientRpc(amount, activePowers, new ClientRpcParams
            //{
            //    Send = new ClientRpcSendParams
            //    {
            //        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(team) }
            //    }
            //});
        }

        /// <summary>
        /// Updates a player's UI 
        /// </summary>
        /// <param name="manna"></param>
        /// <param name="activePowers"></param>
        /// <param name="clientParams"></param>
        [ClientRpc]
        private void UpdateMannaUIClientRpc(int manna, int activePowers, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateMannaBar(manna, activePowers);

        /// <summary>
        /// Checks whether the player from the given team can use the given power.
        /// </summary>
        /// <param name="team">The <c>Team</c> the player controls.</param>
        /// <param name="power">The <c>Power</c> the player wants to activate.</param>
        //[ServerRpc(RequireOwnership = false)]
        public void TryActivatePower/*ServerRpc*/(Team team, Power power)
        {
            bool powerActivated = true;
            if (m_Manna[(int)team] < m_PowerMannaCost[(int)power] || (power == Power.KNIGHT && !HasLeader(team)) ||
                power == Power.FLOOD && Terrain.Instance.HasReachedMaxWaterLevel())
                powerActivated = false;

            if (power == Power.KNIGHT)
                CreateKnight(team);

            if (power == Power.FLOOD)
                CauseFlood();

            if (power == Power.ARMAGHEDDON)
                StartArmageddon();

            NotifyActivatePower/*ClientRpc*/(power, powerActivated);//, new ClientRpcParams
            //{
            //    Send = new ClientRpcSendParams
            //    {
            //        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(team) }
            //    }
            //});
        }

        /// <summary>
        /// Notifies the player whether the power they want to activate is activated or not.
        /// </summary>
        /// <param name="power">The <c>Power</c> the player wants to activate.</param>
        /// <param name="isActivated">True if the power is activated, false otherwise.</param>
        /// <param name="clientParams">RPC info for the client RPC.</param>
        //[ClientRpc]
        private void NotifyActivatePower/*ClientRpc*/(Power power, bool isActivated, ClientRpcParams clientParams = default)
            => PlayerController.Instance.ReceivePowerActivation(power, isActivated);

        #endregion


        #region Powers

        #region Mold Terrain

        /// <summary>
        /// Executes the Mold Terrain power, modifying the given point on the terrain..
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> which should be modified.</param>
        /// <param name="lower">True if the point should be lowered, false if the point should be elevated.</param>
        //[ServerRpc(RequireOwnership = false)]
        public void MoldTerrain/*ServerRpc*/(MapPoint point, bool lower)
        {
            if (point.Y == Terrain.Instance.MaxHeight)
                return;

            MoldTerrainClient/*Rpc*/(point, lower);
        }

        /// <summary>
        /// Executes the Mold Terrain power on the client.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> which should be modified.</param>
        /// <param name="lower">True if the point should be lowered, false if the point should be elevated.</param>
        //[ClientRpc]
        private void MoldTerrainClient/*Rpc*/(MapPoint point, bool lower)
        {
            Terrain.Instance.ModifyTerrain(point, lower);
            CameraController.Instance.UpdateCameraHeight();
        }

        #endregion


        #region Guide Followers

        [ServerRpc(RequireOwnership = false)]
        public void MoveFlagServerRpc(MapPoint point, Team team)
        {
            if (!HasLeader(team)) return;

            StructureManager.Instance.SetSymbolPosition/*ClientRpc*/(team, new Vector3(
                point.GridX * Terrain.Instance.UnitsPerTileSide,
                point.Y,
                point.GridZ * Terrain.Instance.UnitsPerTileSide
            ));

            if (team == Team.RED)
                OnRedSymbolMoved?.Invoke();
            else if (team == Team.BLUE)
                OnBlueSymbolMoved?.Invoke();
        }


        #endregion


        #region Earthquake

        /// <summary>
        /// Executes the Earthquake power on server.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> at the center of the earthquake.</param>
        [ServerRpc(RequireOwnership = false)]
        public void EarthquakeServerRpc(MapPoint point)
            => EarthquakeClientRpc(point, new Random().Next());

        [ClientRpc]
        private void EarthquakeClientRpc(MapPoint point, int randomizerSeed)
            => Terrain.Instance.CauseEarthquake(point, m_EarthquakeRadius, randomizerSeed);

        #endregion


        #region Swamp

        /// <summary>
        /// Executes the Swamp power on the server.
        /// </summary>
        /// <param name="tile">The <c>MapPoint</c> at the center of the area affected by the Swamp power.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateSwampServerRpc(MapPoint tile)
        {
            List<(int, int)> flatTiles = new();
            for (int z = -m_SwampRadius; z < m_SwampRadius; ++z)
            {
                for (int x = -m_SwampRadius; x < m_SwampRadius; ++x)
                {
                    (int x, int z) neighborTile = (tile.GridX + x, tile.GridZ + z);
                    if (tile.GridX + x < 0 || tile.GridX + x >= Terrain.Instance.TilesPerSide ||
                        tile.GridZ + z < 0 || tile.GridZ + z >= Terrain.Instance.TilesPerSide ||
                        !Terrain.Instance.IsTileFlat(neighborTile))
                        continue;

                    Structure structure = Terrain.Instance.GetStructureOnTile(neighborTile);

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
                    Structure structure = Terrain.Instance.GetStructureOnTile(flatTile);

                    if (structure && structure.GetType() == typeof(Field))
                    {
                        affectedSettlements.UnionWith(((Field)structure).SettlementsServed);
                        StructureManager.Instance.DespawnStructure(structure.gameObject);
                    }
                    else if (structure)
                        continue;

                    StructureManager.Instance.SpawnSwamp(flatTile);
                }
            }

            foreach (Settlement settlement in affectedSettlements)
                settlement.SetType();
        }

        #endregion


        #region Knight

        //[ServerRpc(RequireOwnership = false)]
        public void CreateKnight/*ServerRpc*/(Team team)
        {
            if (!HasLeader(team)) return;

            Unit knight = UnitManager.Instance.CreateKnight(team);
            StructureManager.Instance.SetSymbolPosition(team, knight.ClosestMapPoint.ToWorldPosition());

            //CameraController.Instance.SetCameraLookPositionClientRpc(
            //    new Vector3(knight.transform.position.x, 0, knight.transform.position.z),
            //    new ClientRpcParams
            //    {
            //        Send = new ClientRpcSendParams
            //        {
            //            TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(knight.Team) }
            //        }
            //    }
            //);
        }

        #endregion


        #region Volcano

        /// <summary>
        /// Executes the Volcano power on server.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> at the center of the volcano.</param>
        [ServerRpc(RequireOwnership = false)]
        public void VolcanoServerRpc(MapPoint point)
        {
            VolcanoClientRpc(point);
            StructureManager.Instance.PlaceVolcanoRocks(point, m_VolcanoRadius);
        }

        [ClientRpc]
        private void VolcanoClientRpc(MapPoint point)
            => Terrain.Instance.CauseVolcano(point, m_VolcanoRadius);

        #endregion


        #region Flood

        /// <summary>
        /// Executes the Flood power on server.
        /// </summary>
        public void CauseFlood()
        {
            if (Terrain.Instance.WaterLevel == Terrain.Instance.MaxHeight)
                return;

            CauseFloodClientRpc();
            OnFlood?.Invoke();
        }

        [ClientRpc]
        private void CauseFloodClientRpc()
        {
            Terrain.Instance.RaiseWaterLevel();
            Water.Instance.Raise();
            CameraController.Instance.UpdateCameraHeight();
        }

        #endregion


        #region Armagheddon

        public void StartArmageddon()
        {
            foreach (Team team in Enum.GetValues(typeof(Team)))
            {
                if (team == Team.NONE) break;

                StructureManager.Instance.SetSymbolPosition(team, Terrain.Instance.TerrainCenter.ToWorldPosition());
                UnitManager.Instance.ChangeUnitBehavior(UnitBehavior.GO_TO_SYMBOL, team);
            }

            // destroy all settlements
            OnArmageddon?.Invoke();
        }

        #endregion


        #endregion
    }
}