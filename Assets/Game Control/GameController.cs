using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking.Types;
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
        /// The power to upgrade the leader into a KNIGHT.
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

    public enum CameraSnap
    {
        FOCUSED_OBJECT,
        SYMBOL,
        LEADER,
        SETTLEMENT,
        FIGHT,
        KNIGHT
    }


    public interface IFocusableObject : IPointerEnterHandler, IPointerExitHandler
    {
        public GameObject GameObject { get; }
        public void SetHighlight(bool isHighlightOn);
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
        [SerializeField] private float[] m_PowerActivationPercent = new float[Enum.GetNames(typeof(Power)).Length];
        [SerializeField] private int[] m_PowerMannaCost = new int[Enum.GetNames(typeof(Power)).Length];

        [Header("Powers")]
        [SerializeField] private int m_EarthquakeRadius = 3;
        [SerializeField] private int m_SwampRadius = 3;
        [SerializeField] private int m_VolcanoRadius = 3;








        private static GameController m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static GameController Instance { get => m_Instance; }





        public int MaxManna { get => m_MaxManna; }


        /// <summary>
        /// An array of the colors of each team. 
        /// </summary>
        /// <remarks>The color at each index is the color of the team with that index.</remarks>
        public Color[] TeamColors { get => m_TeamColors; }

        private bool m_IsPaused;

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

        private readonly IFocusableObject[] m_FocusedObject = new IFocusableObject[2];
        /// <summary>
        /// An array containing the index of the next fight the player's camera will focus on if the Zoom to FIGHT action is performed.
        /// </summary>
        private readonly int[] m_FightIndex = new int[2];
        /// <summary>
        /// An array containing the index of the next knight the player's camera will focus on if the Zoom to KNIGHT action is performed.
        /// </summary>
        private readonly int[] m_KnightsIndex = new int[2];
        /// <summary>
        /// An array containing the index of the next settlement the player's camera will focus on if the Zoom to SETTLEMENT action is performed.
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
            // for each player
            PlayerController.Instance.SetPlayerInfo(GameData.Instance.GetPlayerInfoByNetworkId(NetworkManager.Singleton.LocalClientId));
            Terrain.Instance.CreateTerrain();

            //StructureManager.Instance.PlaceTreesAndRocks();
            //UnitManager.Instance.SpawnStartingUnits();
            //StructureManager.Instance.SpawnTeamSymbols();
        }


        #region Pause Game

        /// <summary>
        /// Pauses the game if it is unpaused and unpauses the game if it is paused for both players.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void TogglePauseGame_ServerRpc()
        {
            m_IsPaused = !m_IsPaused;
            SetPauseGameClientRpc(m_IsPaused);
        }

        /// <summary>
        /// Pauses the game if it is unpaused and unpauses the game if it is paused for the client.
        /// </summary>
        /// <param name="isPaused">True if the game is paused, false otherwise.</param>
        [ClientRpc]
        private void SetPauseGameClientRpc(bool isPaused)
            => PlayerController.Instance.SetPause(isPaused);

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
        /// Gets the <c>SETTLEMENT</c> the team leader is part of, if such a settlement exists.
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


        #region Focused Object

        [ServerRpc(RequireOwnership = false)]
        public void SetFocusedObject_ServerRpc(NetworkObjectReference focusedObject, Team team, ServerRpcParams serverRpcParams = default)
        {
            if (!focusedObject.TryGet(out NetworkObject networkObject) || networkObject.GetComponent<IFocusableObject>() == null)
                return;

            IFocusableObject focusObject = networkObject.GetComponent<IFocusableObject>();
            IFocusableObject lastFocusedObject = m_FocusedObject[(int)team];

            if (m_FocusedObject[(int)team] != null)
            {
                lastFocusedObject.SetHighlight(false);
                m_FocusedObject[(int)team] = null;
                HideFocusedData/*ClientRpc*/(new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                    }
                });
            }

            if (lastFocusedObject == focusObject) return;

            m_FocusedObject[(int)team] = focusObject;

            if (focusObject.GetType() == typeof(Unit))
            {
                Unit unit = (Unit)focusObject;

                ShowFocusedUnitData/*ClientRpc*/(
                    unit.Team, unit.Class, unit.Strength, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                        }
                    }
                );
            }

            if (focusObject.GetType() == typeof(Settlement))
            {
                Settlement settlement = (Settlement)focusObject;

                ShowFocusedSettlementData/*ClientRpc*/(
                    settlement.Team, settlement.Type, settlement.FollowersInSettlement, settlement.Capacity, new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams
                        {
                            TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                        }
                    }
                );
            }
        }

        public IFocusableObject GetFocusedObject(Team team) => m_FocusedObject[(int)team];

        public bool IsFocusedObject(IFocusableObject focusable) => Array.Exists(m_FocusedObject, x => x == focusable);

        public int GetPlayerFocusingOnObject(IFocusableObject focusable) => Array.IndexOf(m_FocusedObject, focusable);


        //[ClientRpc]
        private void ShowFocusedUnitData/*ClientRpc*/(Team team, UnitClass unitClass, int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowFocusedUnit(team, unitClass, strength);

        //[ClientRpc]
        private void ShowFocusedSettlementData/*ClientRpc*/(Team team, SettlementType type, int unitsInSettlement, int maxUnitsInSettlement, ClientRpcParams clientParams = default)
            => GameUI.Instance.ShowFocusedSettlement(team, type, unitsInSettlement, maxUnitsInSettlement);

        //[ClientRpc]
        private void HideFocusedData/*ClientRpc*/(ClientRpcParams clientParams = default) => GameUI.Instance.HideFocusedData();

        public void RemoveFocusedObject(IFocusableObject focusable)
        {
            int index = GetPlayerFocusingOnObject(focusable);
            // nobody is focusing on the object
            if (index < 0) return;

            m_FocusedObject[index] = null;
            HideFocusedData/*ClientRpc*/(//new ClientRpcParams
            //    { 
            //        Send = new ClientRpcSendParams
            //        {
            //            TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
            //        }
            //    }
            );
        }


        public void UpdateFocusedUnit(Unit unit, bool updateClass = false, bool updateStrength = false)
        {
            int index = GetPlayerFocusingOnObject(unit);
            if (index < 0) return;

            if (updateClass)
            {
                UpdateFocusedUnitClass/*ClientRpc*/(unit.Class//, new ClientRpcParams
                //    { 
                //        Send = new ClientRpcSendParams
                //        {
                //            TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
                //        }
                //    }
                );
            }

            if (updateStrength)
            {
                UpdateFocusedUnitStrength/*ClientRpc*/(unit.Strength//, new ClientRpcParams
                //    { 
                //        Send = new ClientRpcSendParams
                //        {
                //            TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
                //        }
                //    }
                );
            }
        }

        //[ClientRpc]
        private void UpdateFocusedUnitClass/*ClientRpc*/(UnitClass unitClass, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateFocusedUnitClass(unitClass);

        //[ClientRpc]
        private void UpdateFocusedUnitStrength/*ClientRpc*/(int strength, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateFocusedUnitStrength(strength);

        public void UpdateFocusedSettlement(Settlement settlement, bool updateTeam = false, bool updateType = false, bool updateFollowers = false)
        {
            int index = GetPlayerFocusingOnObject(settlement);
            if (index < 0) return;

            if (updateTeam)
            {
                UpdateFocusedSettlementTeam/*ClientRpc*/(settlement.Team//, new ClientRpcParams
                //    { 
                //        Send = new ClientRpcSendParams
                //        {
                //            TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
                //        }
                //    }
                );
            }

            if (updateType)
            {
                UpdateFocusedSettlementType/*ClientRpc*/(settlement.Type, settlement.Capacity//, new ClientRpcParams
                //    { 
                //        Send = new ClientRpcSendParams
                //        {
                //            TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
                //        }
                //    }
                );
            }

            if (updateFollowers)
            {
                UpdateFocusedSettlementFollowers/*ClientRpc*/(settlement.FollowersInSettlement//, new ClientRpcParams
                //    { 
                //        Send = new ClientRpcSendParams
                //        {
                //            TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(index) }
                //        }
                //    }
                );
            }
        }

        //[ClientRpc]
        private void UpdateFocusedSettlementTeam/*ClientRpc*/(Team team, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateFocusedSettlementTeam(team);

        //[ClientRpc]
        private void UpdateFocusedSettlementType/*ClientRpc*/(SettlementType type, int maxUnitsInSettlement, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateFocusedSettlementType(type, maxUnitsInSettlement);

        //[ClientRpc]
        private void UpdateFocusedSettlementFollowers/*ClientRpc*/(int followers, ClientRpcParams clientParams = default)
            => GameUI.Instance.UpdateFocusedSettlementFollowers(followers);

        #endregion


        #region Camera

        /// <summary>
        /// Sends the camera of the player of the given team to the location of the focused object.
        /// </summary>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowFocusedObject_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            IFocusableObject focusable = GetFocusedObject(team);

            if (focusable == null)
            {
                NotifyCannotSnapClientRpc(CameraSnap.FOCUSED_OBJECT);
                return;
            }

            CameraController.Instance.SetCameraLookPositionClientRpc(
                focusable.GameObject.transform.position,
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
        /// Sends the camera of the player of the given team to the location of the team's symbol.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose camera should be moved.</param>
        /// <param name="serverRpcParams">RPC data for the client RPC.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ShowTeamSymbol_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            CameraController.Instance.SetCameraLookPositionClientRpc(
                StructureManager.Instance.GetSymbolPosition(team),
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
        public void ShowLeader_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            GameObject leader = GetLeaderObject(team);

            if (!leader)
            {
                NotifyCannotSnapClientRpc(CameraSnap.LEADER);
                return;
            }

            CameraController.Instance.SetCameraLookPositionClientRpc(
                leader.transform.position,
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
        public void ShowSettlements_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Vector3? position = StructureManager.Instance.GetSettlementLocation(m_SettlementIndex[teamIndex], team);
            if (!position.HasValue)
            {
                NotifyCannotSnapClientRpc(CameraSnap.SETTLEMENT);
                return;
            }

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
        public void ShowFights_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Vector3? position = UnitManager.Instance.GetFightLocation(m_FightIndex[(int)team]);
            if (!position.HasValue)
            {
                NotifyCannotSnapClientRpc(CameraSnap.FIGHT);
                return;
            }

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
        public void ShowKnights_ServerRpc(Team team, ServerRpcParams serverRpcParams = default)
        {
            int teamIndex = (int)team;

            Unit knight = UnitManager.Instance.GetKnight(m_KnightsIndex[teamIndex], team);
            if (!knight)
            {
                NotifyCannotSnapClientRpc(CameraSnap.KNIGHT);
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

        [ClientRpc]
        private void NotifyCannotSnapClientRpc(CameraSnap snapOption)
            => GameUI.Instance.CannotSnapToOption(snapOption);

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

            int activePowers = -1;
            foreach (float threshold in m_PowerActivationPercent)
            {
                if (amount < threshold * m_MaxManna) break;
                activePowers++;
            }

            UpdateMannaUI/*ClientRpc*/(amount, activePowers//, new ClientRpcParams
            //{
            //    Send = new ClientRpcSendParams
            //    {
            //        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(team) }
            //    }
            //}
            );
        }

        /// <summary>
        /// Updates a player's UI 
        /// </summary>
        /// <param name="manna"></param>
        /// <param name="activePowers"></param>
        /// <param name="clientParams"></param>
        //[ClientRpc]
        private void UpdateMannaUI/*ClientRpc*/(int manna, int activePowers, ClientRpcParams clientParams = default)
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
                    CauseFlood();

                if (power == Power.ARMAGHEDDON)
                    StartArmageddon();
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
        /// Executes the Mold Terrain power, modifying the given point on the terrain..
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> which should be modified.</param>
        /// <param name="lower">True if the point should be lowered, false if the point should be elevated.</param>
        [ServerRpc(RequireOwnership = false)]
        public void MoldTerrain_ServerRpc(TerrainPoint point, bool lower)
        {
            if (point.Y == Terrain.Instance.MaxHeight)
                return;

            MoldTerrainClientRpc(point, lower);
        }

        /// <summary>
        /// Executes the Mold Terrain power on the client.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> which should be modified.</param>
        /// <param name="lower">True if the point should be lowered, false if the point should be elevated.</param>
        [ClientRpc]
        private void MoldTerrainClientRpc(TerrainPoint point, bool lower)
        {
            Terrain.Instance.ModifyTerrain(point, lower);
        }

        #endregion


        #region Guide Followers

        [ServerRpc(RequireOwnership = false)]
        public void MoveFlag_ServerRpc(TerrainPoint point, Team team)
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
        /// <param name="point">The <c>TerrainPoint</c> at the center of the earthquake.</param>
        [ServerRpc(RequireOwnership = false)]
        public void Earthquake_ServerRpc(TerrainPoint point)
            => EarthquakeClientRpc(point, new Random().Next());

        [ClientRpc]
        private void EarthquakeClientRpc(TerrainPoint point, int randomizerSeed)
            => Terrain.Instance.CauseEarthquake(point, m_EarthquakeRadius, randomizerSeed);

        #endregion


        #region Swamp

        /// <summary>
        /// Executes the Swamp power on the server.
        /// </summary>
        /// <param name="tile">The <c>TerrainPoint</c> at the center of the area affected by the Swamp power.</param>
        [ServerRpc(RequireOwnership = false)]
        public void CreateSwamp_ServerRpc(TerrainPoint tile)
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
                settlement.SetSettlementType();
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
        /// <param name="point">The <c>TerrainPoint</c> at the center of the volcano.</param>
        [ServerRpc(RequireOwnership = false)]
        public void Volcano_ServerRpc(TerrainPoint point)
        {
            VolcanoClientRpc(point);
            StructureManager.Instance.PlaceVolcanoRocks(point, m_VolcanoRadius);
        }

        [ClientRpc]
        private void VolcanoClientRpc(TerrainPoint point)
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


        /// <summary>
        /// Switches the behavior of all the units in the given team to the given behavior.
        /// </summary>
        /// <param name="behavior">The <c>UnitBehavior</c> that should be applied to all units in the team.</param>
        /// <param name="team">The <c>Team</c> whose units should be targeted.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ChangeUnitBehavior_ServerRpc(UnitBehavior behavior, Team team)
        {
            UnitManager.Instance.ChangeUnitBehavior(behavior, team);
        }
    }
}