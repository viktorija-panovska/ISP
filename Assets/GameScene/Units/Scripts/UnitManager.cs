using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

using Random = System.Random;


namespace Populous
{    
    /// <summary>
    /// Types of units.
    /// </summary>
    public enum UnitType
    {
        /// <summary>
        /// A standard unit, which can exhibit all behaviors.
        /// </summary>
        WALKER,
        /// <summary>
        /// A walker which carries a leader symbol and can become a Knight.
        /// </summary>
        LEADER,
        /// <summary>
        /// A unit who seeks out enemy units to fight and can destroy enemy settlements.
        /// </summary>
        KNIGHT
    }

    /// <summary>
    /// Behaviors a unit can exhibit.
    /// </summary>
    public enum UnitBehavior
    {
        /// <summary>
        /// The unit roams the terrain looking for flat spaces to build settlements on.
        /// </summary>
        SETTLE,
        /// <summary>
        /// The unit goes to its faction's unit magnet.
        /// </summary>
        GO_TO_MAGNET,
        /// <summary>
        /// The unit seeks out other units of its faction to combine with.
        /// </summary>
        GATHER,
        /// <summary>
        /// The unit seeks out units from the enemy faction to fight with.
        /// </summary>
        FIGHT
    }


    /// <summary>
    /// The <c>UnitManager</c> class manages the creation, destruction, and behavior of all the units in the game.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class UnitManager : NetworkBehaviour
    {
        #region Inspector Fields

        [Tooltip("The GameObject that should be created when a unit is spawned.")]
        [SerializeField] private GameObject m_UnitPrefab;

        [Tooltip("The maximum number of followers for each team.")]
        [SerializeField] private int m_MaxFollowersInFaction = 1000;

        [Tooltip("The maximum possible number of followers that can be in a single unit.")]
        [SerializeField] private int m_MaxUnitStrength = 100;

        [Tooltip("The number of unit steps after which the unit loses one follower.")]
        [SerializeField] private int m_UnitDecayRate = 20;

        [Tooltip("The number of seconds between each time the units in a fight deal damage to each other.")]
        [SerializeField] private float m_FightWaitDuration = 5f;

        [Tooltip("The amount of manna lost when a leader dies.")]
        [SerializeField] private int m_LeaderDeathManna = 10;


        [Header("Starting Units")]

        [Tooltip("The number of units each team has at the start of the match.")]
        [SerializeField] private int m_StartingUnits = 15;

        [Tooltip("The amount of followers in each of the starting units.")]
        [SerializeField] private int m_StartingUnitStrength = 1;


        [Header("UI")]

        [Tooltip("The scale of the unit icons on the minimap.")]
        [SerializeField] private int m_MinimapIconScale;

        [Tooltip("The colors of the icons for the units on the minimap, where 0 is the color of the red faction and 1 is the color of the blue faciton.")]
        [SerializeField] private Color[] m_MinimapUnitColors;

        #endregion


        #region Class Fields

        private static UnitManager m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static UnitManager Instance { get => m_Instance; }

        /// <summary>
        /// Gets the maximum number of followers a faction can have.
        /// </summary>
        public int MaxFollowersInFaction { get => m_MaxFollowersInFaction; }
        /// <summary>
        /// Gets the maximum possible number of followers that can be in a single unit.
        /// </summary>
        public int MaxUnitStrength { get => m_MaxUnitStrength; }
        /// <summary>
        /// Gets the number of steps after which a unit loses one follower.
        /// </summary>
        public int UnitDecayRate { get => m_UnitDecayRate; }
        /// <summary>
        /// Gets the number of units of each faction at the start of the match.
        /// </summary>
        public int StartingUnits { get => m_StartingUnits; }

        /// <summary>
        /// An array storing the number of followers in each faction.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        private readonly int[] m_Followers = new int[2];

        /// <summary>
        /// An array storing the active unit behavior for each faction.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        private readonly UnitBehavior[] m_ActiveBehavior = new UnitBehavior[2];

        /// <summary>
        /// An array of grids representing the terrain which store the amount of times the units of each faction have visited each terrain point.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        private readonly int[][,] m_GridPointSteps = new int[2][,];

        /// <summary>
        /// An array of the leaders in each faction that are part of a unit, null if the faction's leader is not in a unit.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        private readonly Unit[] m_LeaderUnits = new Unit[2];

        /// <summary>
        /// An array of lists containing all the active knights of each faction.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        private readonly List<Unit>[] m_Knights = new List<Unit>[] { new(), new() };

        /// <summary>
        /// A list of the IDs of the active fights.
        /// </summary>
        private readonly List<int> m_FightIds = new();
        /// <summary>
        /// A map of fight IDs to the pair of units involved in the fight with that ID.
        /// </summary>
        private readonly Dictionary<int, (Unit red, Unit blue)> m_Fights = new();
        /// <summary>
        /// The ID that should be assigned to the next fight.
        /// </summary>
        private int m_NextFightId = 0;

        /// <summary>
        /// The scale for the unit icons on the minimap.
        /// </summary>
        public int MinimapIconScale { get => m_MinimapIconScale; }
        /// <summary>
        /// The colors of the icons for the units on the minimap.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        public Color[] MinimapUnitColors { get => m_MinimapUnitColors; }

        #endregion


        #region Actions

        /// <summary>
        /// Action to be called when the behavior of the units in the red faction is changed.
        /// </summary>
        public Action<UnitBehavior> OnRedBehaviorChange;
        /// <summary>
        /// Action to be called when the behavior of the units in the blue faction is changed.
        /// </summary>
        public Action<UnitBehavior> OnBlueBehaviorChange;
        /// <summary>
        /// Action to be called when a new unit is assigned as the leader of the team.
        /// </summary>
        public Action OnNewLeaderGained;
        /// <summary>
        /// Action to be called when a unit is despawned to remove references to it from other objects.
        /// </summary>
        public Action<Unit> OnRemoveReferencesToUnit;

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

        #endregion


        #region Spawn/Despawn

        /// <summary>
        /// Creates a unit belonging to the given faction at the given location and spawns it on the network.
        /// </summary>
        /// <param name="location">The <c>TerrainPoint</c> at which the new unit should be spawned.</param>
        /// <param name="faction">The faction the new unit should belong to.</param>
        /// <param name="unitClass">The class of the unit, Walker by default.</param>
        /// <param name="strength">The initial number of followers in the unit, 1 by default.</param>
        /// <param name="origin">The settlement the unit was created by, null for the starting units.</param>
        /// <returns>The <c>GameObject</c> of the newly spawned unit.</returns>
        public GameObject SpawnUnit(TerrainPoint location, Faction faction, UnitType unitClass = UnitType.WALKER, int strength = 1, Settlement origin = null)
        {
            if (!IsHost || strength == 0) return null;

            GameObject unitObject = Instantiate(
                m_UnitPrefab,
                new Vector3(
                    location.X * Terrain.Instance.UnitsPerTileSide,
                    location.GetHeight(),
                    location.Z * Terrain.Instance.UnitsPerTileSide),
                Quaternion.identity
            );

            Unit unit = unitObject.GetComponent<Unit>();

            unit.Setup(faction, strength, origin);
            unit.SetBehavior(m_ActiveBehavior[(int)faction]);

            if (unitClass == UnitType.LEADER)
                SetUnitLeader(faction, unit);

            // TODO Knight
            if (unitClass == UnitType.KNIGHT)
                unit.SetType(UnitType.KNIGHT);

            // spawn on network
            NetworkObject networkUnit = unitObject.GetComponent<NetworkObject>();
            networkUnit.Spawn(true);

            return unitObject;
        }

        /// <summary>
        /// Despawns the given unit from the network and destroys is.
        /// </summary>
        /// <param name="unitObject">The <c>GameObject</c> of the unit to be destroyed.</param>
        /// <param name="hasDied">True if the unit is being despawned because it died, false if it is being despawned because it entered a settlement.</param>
        public void DespawnUnit(GameObject unitObject, bool hasDied)
        {
            if (!IsHost) return;

            Unit unit = unitObject.GetComponent<Unit>();
            unit.Cleanup();
            OnRemoveReferencesToUnit?.Invoke(unit);

            if (unit.Type == UnitType.LEADER)
            {
                UnsetUnitLeader(unit.Faction);

                if (hasDied)
                {
                    GameController.Instance.RemoveManna(unit.Faction, m_LeaderDeathManna);
                    GameController.Instance.AddManna(unit.Faction == Faction.RED ? Faction.BLUE : Faction.RED, m_LeaderDeathManna);
                    GameController.Instance.PlaceUnitMagnetAtPoint(unit.Faction, unit.ClosestTerrainPoint);
                }
            }

            if (unit.Type == UnitType.KNIGHT)
            {
                // TODO Knight
                m_Knights[(int)unit.Faction].Remove(unit);
                unit.SetType(UnitType.WALKER);
            }

            unitObject.GetComponent<NetworkObject>().Despawn();
            Destroy(unitObject);
        }

        #endregion


        #region Starter Units

        /// <summary>
        /// Creates the starting units for both teams.
        /// </summary>
        public void SpawnStartingUnits()
        {
            if (!IsHost) return;

            ResetGridSteps(Faction.RED);
            ResetGridSteps(Faction.BLUE);

            if (m_StartingUnits * m_StartingUnitStrength > m_MaxFollowersInFaction)
                m_StartingUnitStrength = 1;

            if (m_StartingUnits > m_MaxFollowersInFaction)
                m_StartingUnits = m_MaxFollowersInFaction;

            List<(int, int)> redSpawnPoints = new();
            List<(int, int)> blueSpawnPoints = new();

            FindSpawnPoints(ref redSpawnPoints, ref blueSpawnPoints);

            Random random = new(!GameData.Instance ? 0 : GameData.Instance.GameSeed);

            // go over both teams
            for (int team = 0; team <= 1; ++team)
            {
                List<(int, int)> spawnPoints = team == 0 ? redSpawnPoints : blueSpawnPoints;
                List<int> spawnIndices = Enumerable.Range(0, spawnPoints.Count).ToList();
                int leader = random.Next(0, m_StartingUnits);

                int spawned = 0;

                // shuffle algorithm
                int count = spawnIndices.Count;
                foreach ((int x, int z) spawnPoint in spawnPoints)
                {
                    count--;
                    int randomIndex = random.Next(count + 1);
                    (spawnIndices[count], spawnIndices[randomIndex]) = (spawnIndices[randomIndex], spawnIndices[count]);

                    if (spawnIndices[count] < m_StartingUnits)
                    {
                        SpawnUnit(
                            new TerrainPoint(spawnPoint.x, spawnPoint.z), 
                            team == 0 ? Faction.RED : Faction.BLUE, 
                            strength: m_StartingUnitStrength, 
                            unitClass: spawned == leader ? UnitType.LEADER : UnitType.WALKER, 
                            origin: null
                        );
                        spawned++;
                    }
                }

                if (spawned == m_StartingUnits || spawnPoints.Count == 0)
                    continue;

                // if there weren't enough places to spawn all the units we want on, spawn the remaining units randomly
                for (int i = 0; i < m_StartingUnits - spawned; ++i)
                {
                    (int x, int z) point = spawnPoints[random.Next(spawnPoints.Count)];
                    SpawnUnit(
                        new TerrainPoint(point.x, point.z),
                        team == 0 ? Faction.RED : Faction.BLUE,
                        strength: m_StartingUnitStrength,
                        unitClass: spawned == leader ? UnitType.LEADER : UnitType.WALKER,
                        origin: null
                    );
                    spawned++;
                }
            }
        }

        /// <summary>
        /// Collects a list of available terrain points for each team to spawn the starting units on.
        /// </summary>
        /// <param name="redSpawns">A reference to the list of available spawn points for the red team.</param>
        /// <param name="blueSpawns">A reference to the list of available spawn points for the blue team.</param>
        private void FindSpawnPoints(ref List<(int x, int z)> redSpawns, ref List<(int x, int z)> blueSpawns)
        {
            // going from the corners towards the center of the terrain
            for (int dist = 0; dist < Terrain.Instance.TilesPerSide; ++dist)
            {
                for (int tile_z = 0; tile_z <= dist; ++tile_z)
                {
                    (int, int)[] tiles;
                    if (tile_z == dist)
                        tiles = new (int, int)[] { (dist, dist) };                       // diagonal
                    else
                        tiles = new (int, int)[] { (tile_z, dist), (dist, tile_z) };     // up and down

                    //foreach ((int x, int z) tile in tiles)
                    //{
                    //    // we want to get at most twice the amount of tiles as there are units to spawn
                    //    if (redSpawns.Count <= 2 * m_StartingUnits && !blueSpawns.Contains(tile) &&
                    //        !StructureManager.Instance.IsTileOccupied(tile) && !(new TerrainTile(tile).IsUnderwater()))
                    //        redSpawns.Add(tile);

                    //    (int x, int z) oppositeTile = (Terrain.Instance.TilesPerSide - tile.x - 1, Terrain.Instance.TilesPerSide - tile.z - 1);

                    //    if (blueSpawns.Count <= 2 * m_StartingUnits && !redSpawns.Contains(oppositeTile) &&
                    //        !StructureManager.Instance.IsTileOccupied(oppositeTile) && !(new TerrainTile(oppositeTile).IsUnderwater()))
                    //        blueSpawns.Add(oppositeTile);

                    //    if (redSpawns.Count > 2 * m_StartingUnits && blueSpawns.Count > 2 * m_StartingUnits)
                    //        return;
                    //}
                }
            }
        }

        #endregion







        #region Followers

        /// <summary>
        /// Checks whether the given faction has reached the maximum number of followers.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose population should be checked.</param>
        /// <returns>True if the faction is full, false otherwise.</returns>
        public bool IsFactionFull(Faction faction) => m_Followers[(int)faction] == m_MaxFollowersInFaction;

        /// <summary>
        /// Adds the given amount of followers to the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the followers should be added to.</param>
        /// <param name="amount">The amount of followers that should be added.</param>
        public void AddFollowers(Faction faction, int amount = 1)
        {
            amount = Mathf.Clamp(m_Followers[(int)faction] + amount, 0, m_MaxFollowersInFaction);
            SetFollowers(faction, amount);
            GameController.Instance.AddManna(faction, amount);
        }

        /// <summary>
        /// Removes the given amount of followers from the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the followers should be removed from.</param>
        /// <param name="amount">The amount of followers that should be removed.</param>
        public void RemovePopulation(Faction faction, int amount = 1)
            => SetFollowers(faction, Mathf.Clamp(m_Followers[(int)faction] - amount, 0, m_MaxFollowersInFaction));

        /// <summary>
        /// Sets the number of followers of the given faction to the given amount.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose number of followers should be set.</param>
        /// <param name="amount">The amount of followers the given faction should have.</param>
        private void SetFollowers(Faction faction, int amount)
        {
            if (amount == m_Followers[(int)faction]) return;

            m_Followers[(int)faction] = amount;

            UpdateFollowersUI_ClientRpc(faction, amount);

            if (m_Followers[(int)faction] == 0)
                GameController.Instance.EndGame_ClientRpc(winner: faction == Faction.RED ? Faction.BLUE : Faction.RED); ;
        }

        /// <summary>
        /// Updates the display of the follower numbers shown on the UI of both players.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose followers should be updated.</param>
        /// <param name="currentFollowers">The current amount of followers of the faction.</param>
        [ClientRpc]
        public void UpdateFollowersUI_ClientRpc(Faction faction, int currentFollowers)
            => GameUI.Instance.UpdatePopulationBar(faction, currentFollowers);

        #endregion


        #region Behavior

        /// <summary>
        /// Switches the behavior of all the units of the given faction to the given behavior.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose units should be targeted.</param>
        /// <param name="behavior">The <c>UnitBehavior</c> that should be applied to all units in the faction.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ChangeUnitBehavior_ServerRpc(Faction faction, UnitBehavior behavior)
        {
            if (m_ActiveBehavior[(int)faction] == behavior) return;

            m_ActiveBehavior[(int)faction] = behavior;

            if (faction == Faction.RED)
                OnRedBehaviorChange?.Invoke(behavior);
            else if (faction == Faction.BLUE)
                OnBlueBehaviorChange?.Invoke(behavior);
        }

        /// <summary>
        /// Gets the currently active behavior of the units in the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose active unit behavior should be returned.</param>
        /// <returns>The currently active <c>UnitBehavior</c>.</returns>
        public UnitBehavior GetActiveBehavior(Faction faction) => m_ActiveBehavior[(int)faction];

        #endregion


        #region Grid Steps

        /// <summary>
        /// Increments the number of times a terrain point has been stepped on by units of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose unit steps should be incremented.</param>
        /// <param name="point">The <c>TerrainPoint</c> that is being stepped on.</param>
        public void AddStepAtPoint(Faction faction, TerrainPoint point) => m_GridPointSteps[(int)faction][point.Z, point.X]++;

        /// <summary>
        /// Gets the number of times a terrain point has been stepped on by units of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose unit steps should be returned.</param>
        /// <param name="point">The <c>TerrainPoint</c> whose number of steps should be returned.</param>
        /// <returns>The number of times the terrain point has been stepped on by units of the faction.</returns>
        public int GetStepsAtPoint(Faction faction, TerrainPoint point) => m_GridPointSteps[(int)faction][point.Z, point.X];

        /// <summary>
        /// Removes the step counts of units of the given faction from all the terrain points.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose unit steps should be removed.</param>
        public void ResetGridSteps(Faction faction)
            => m_GridPointSteps[(int)faction] = new int[Terrain.Instance.TilesPerSide + 1, Terrain.Instance.TilesPerSide + 1];

        #endregion


        #region Leader Unit

        /// <summary>
        /// Checks whether the given faction has a leader that is in a unit.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be checked.</param>
        /// <returns>True if the faction has a leader that is in a unit, false otherwise.</returns>
        public bool HasUnitLeader(Faction faction) => m_LeaderUnits[(int)faction];

        /// <summary>
        /// Gets the leader unit of the faction, if such a unit exists.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be returned.</param>
        /// <returns>The <c>Unit</c> of the faction's leader, null if the leader is not part of a unit.</returns>
        public Unit GetLeaderUnit(Faction faction) => m_LeaderUnits[(int)faction];

        /// <summary>
        /// Sets the given unit as the leader of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be set.</param>
        /// <param name="unit">The <c>Unit</c> that should be set as the leader.</param>
        public void SetUnitLeader(Faction faction, Unit unit)
        {
            UnsetUnitLeader(faction);

            m_LeaderUnits[(int)faction] = unit;
            unit.SetType(UnitType.LEADER);
            OnNewLeaderGained?.Invoke();
        }

        public void UnsetUnitLeader(Faction team)
        {
            if (!HasUnitLeader(team)) return;

            m_LeaderUnits[(int)team].SetType(UnitType.WALKER);
            m_LeaderUnits[(int)team] = null;
        }


        #endregion



        #region Knight

        /// <summary>
        /// Gets the knight from the given team that is at the given index in the list of knights.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the returned knight should belong to.</param>
        /// <param name="index">The index of the knight that should be returned.</param>
        /// <returns>A <c>Unit</c> of the Knight class from the given team.</returns>
        public Unit GetKnight(Faction team, int index) => index >= m_Knights[(int)team].Count ? null : m_Knights[(int)team][index];

        /// <summary>
        /// Returns the number of knights in the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the returned knight should belong to.</param>
        /// <returns>The number of knights in the given team.</returns>
        public int GetKnightsNumber(Faction team) => m_Knights[(int)team].Count;

        public void AddKnight(Faction faction, Unit knight) => m_Knights[(int)faction].Add(knight);


        /// <summary>
        /// Turns the leader of the given team into a knight, if the team has a leader.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the new knight should belong to.</param>
        /// <returns>The <c>Unit</c> that has been turned into a knight, or null if no such unit exists.</returns>
        private Unit CreateKnight(Faction team)
        {
            Unit knight = null;

            // if the leader is in a unit, just turn that unit into a knight
            if (HasUnitLeader(team))
            {
                knight = GetLeaderUnit(team);
                UnsetUnitLeader(team);
                knight.SetType(UnitType.KNIGHT);
                m_Knights[(int)team].Add(knight);
            }

            // if the leader is in a origin, destroy that origin and spawnPoint a knight in its position
            if (StructureManager.Instance.HasSettlementLeader(team))
            {
                //Settlement settlement = StructureManager.Instance.GetLeaderSettlement(team);
                //StructureManager.Instance.UnsetLeaderSettlement(team);
                //knight = SpawnUnit(
                //    location: settlement.OccupiedTile, 
                //    team, 
                //    unitClass: UnitClass.KNIGHT, 
                //    followers: settlement.FollowersInSettlement,
                //    origin: settlement
                //).GetComponent<Unit>();
                //settlement.DestroySettlement(updateNeighbors: true);
            }

            return knight;
        }

        #endregion


        #region Fights

        /// <summary>
        /// Gets the location of the fight at the given index in the fight IDs list.
        /// </summary>
        /// <param name="index">The index of the fight in the fight IDs list whose location we want.</param>
        /// <returns>The position of the fight.</returns>
        public Vector3? GetFightLocation(int index) 
            => index >= m_FightIds.Count ? null : m_Fights[m_FightIds[index]].red.gameObject.transform.position;

        /// <summary>
        /// Gets the number of fights currently happening.
        /// </summary>
        /// <returns>The number of fights.</returns>
        public int GetFightsNumber() => m_FightIds.Count;

        /// <summary>
        /// Gets the two units participating in the fight with the given ID.
        /// </summary>
        /// <param name="fightId">The ID of the fight.</param>
        /// <returns>A tuple of participants in the fight, where the first element is the red unit and the second element is the blue unit.</returns>
        public (Unit red, Unit blue) GetFightParticipants(int fightId) => m_Fights[fightId];

        /// <summary>
        /// Sets up and begins a fight between two units.
        /// </summary>
        /// <param name="red">The <c>Unit</c> from the red team.</param>
        /// <param name="blue">The <c>Unit</c> from the blue team.</param>
        /// <param name="settlementDefense">A <c>Settlement</c> if the fight occured due to an attempt to claim a origin, null otherwise.</param>
        public void StartFight(Unit red, Unit blue, Settlement settlementDefense = null)
        {
            m_Fights.Add(m_NextFightId, (red, blue));
            m_FightIds.Add(m_NextFightId);

            red.StartFight(m_NextFightId);
            blue.StartFight(m_NextFightId);

            m_NextFightId++;

            StartCoroutine(Fight(red, blue, settlementDefense));
        }

        /// <summary>
        /// Handles the fighting between two units.
        /// </summary>
        /// <param name="red">The <c>Unit</c> from the red team.</param>
        /// <param name="blue">The <c>Unit</c> from the blue team.</param>
        /// <param name="settlementDefense">A <c>Settlement</c> if the fight occured due to an attempt to claim a origin, null otherwise.</param>
        /// <returns>An <c>IEnumerator</c> which waits for a number of seconds before simulating another attack.</returns>
        private IEnumerator Fight(Unit red, Unit blue, Settlement settlementDefense = null)
        {
            int fightId = red.FightId;

            while (true)
            {
                yield return new WaitForSeconds(m_FightWaitDuration);

                // check whether a unit has been destroyed due to circumstances outside the fight
                if (!red || !blue) break;

                // simulate a strike
                red.LoseStrength(1);
                blue.LoseStrength(1);

                if (red.Strength == 0 || blue.Strength == 0)
                    break;
            }

            Unit winner = null, loser = null;
            if (!red) winner = blue;
            else if (!blue) winner = red;
            else if (red && blue)
            {
                winner = red.Strength == 0 ? blue : red;
                loser = red.Strength == 0 ? red : blue;
            }

            if (winner)
            {
                if (settlementDefense)
                    ResolveSettlementAttack(winner, settlementDefense);

                EndFight(winner, loser);
            }
            else
            {
                // both of them were destroyed outside the fight, so there is no winner, but the fight should be removed
                m_Fights.Remove(fightId);
                m_FightIds.Remove(fightId);
            }
        }

        /// <summary>
        /// Ends the fight and destroys the loser.
        /// </summary>
        /// <param name="winner">The <c>Unit</c> who won the battle.</param>
        /// <param name="loser">The <c>Unit</c> who lost the battle.</param>
        public void EndFight(Unit winner, Unit loser)
        {
            m_Fights.Remove(winner.FightId);
            m_FightIds.Remove(winner.FightId);

            winner.EndFight();
            if (loser) loser.EndFight();
        }

        /// <summary>
        /// Handles the attack of a unit on an enemy origin.
        /// </summary>
        /// <param name="unit">the <c>Unit</c> which is attacking the origin.</param>
        /// <param name="settlement">The <c>Settlement</c> that is being attacked.</param>
        public void AttackSettlement(Unit unit, Settlement settlement)
        {
            // one unit can attack a origin at a time
            if (settlement.IsAttacked) return;

            settlement.IsAttacked = true;
            unit.ToggleMovement(pause: true);

            // unit leaves the origin
            int followersInUnit = 0;
            if (settlement.FollowersInSettlement >= settlement.ReleasedUnitStrength)
                followersInUnit = settlement.ReleasedUnitStrength;
            else if (settlement.FollowersInSettlement > 0)
                followersInUnit = settlement.FollowersInSettlement;

            Unit other = SpawnUnit(
                location: unit.ClosestTerrainPoint, 
                faction: settlement.Faction, 
                unitClass: UnitType.WALKER, 
                strength: followersInUnit,
                origin: settlement
            ).GetComponent<Unit>();

            if (!other)
            {
                ResolveSettlementAttack(unit, settlement);
                return;
            }

            StartFight(unit.Faction == Faction.RED ? unit : other, unit.Faction == Faction.BLUE ? unit : other, settlement);
        }

        /// <summary>
        /// Handles the aftermath of the attack by a unit on the enemy origin.
        /// </summary>
        /// <param name="winner">The <c>Unit</c> that won the battle for the origin.</param>
        /// <param name="settlement">The <c>Settlement</c> that was being attacked.</param>
        private void ResolveSettlementAttack(Unit winner, Settlement settlement)
        {
            if (winner.Faction == settlement.Faction) return;

            if (winner.Type == UnitType.KNIGHT)
                settlement.BurnDown();
            else
                StructureManager.Instance.ChangeSettlementFaction(settlement, winner.Faction);

            settlement.IsAttacked = false;
        }

        #endregion

    }
}