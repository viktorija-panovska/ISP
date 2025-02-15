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
    /// Classes of units.
    /// </summary>
    public enum UnitClass
    {
        /// <summary>
        /// A standard unit, which can exhibit all behaviors.
        /// </summary>
        WALKER,
        /// <summary>
        /// A walker which carries the team symbol and can become a knight.
        /// </summary>
        LEADER,
        /// <summary>
        /// A unit who seeks out enemy units to fight and can destroy enemy settlements.
        /// </summary>
        KNIGHT
    }

    /// <summary>
    /// Types of behaviors a unit can exhibit.
    /// </summary>
    public enum UnitBehavior
    {
        /// <summary>
        /// The unit roams the terrain looking for flat spaces to build settlements on.
        /// </summary>
        SETTLE,
        /// <summary>
        /// The unit goes to its team's unit magnet.
        /// </summary>
        GO_TO_MAGNET,
        /// <summary>
        /// The unit seeks out other units of its team to combine with.
        /// </summary>
        GATHER,
        /// <summary>
        /// The unit seeks out units from the enemy team to fight with.
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

        [Tooltip("The game object that should be created when a unit is spawned.")]
        [SerializeField] private GameObject m_UnitPrefab;

        [Tooltip("The maximum population for each team.")]
        [SerializeField] private int m_MaxPopulation = 1000;

        [Tooltip("The maximum possible number of followers that can be in a single unit.")]
        [SerializeField] private int m_MaxUnitPopulation = 100;

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
        [SerializeField] private int m_StartingUnitPopulation = 1;

        #endregion


        #region Class Fields

        private static UnitManager m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static UnitManager Instance { get => m_Instance; }


        /// <summary>
        /// Gets the maximum size of population of a team.
        /// </summary>
        public int MaxPopulation { get => m_MaxPopulation; }

        /// <summary>
        /// Gets the maximum possible number of followersInUnit that can be in a single unit.
        /// </summary>
        public int MaxUnitPopulation { get => m_MaxUnitPopulation; }

        /// <summary>
        /// Gets the number of steps after which a unit loses one follower.
        /// </summary>
        public int DecayRate { get => m_UnitDecayRate; }

        /// <summary>
        /// Gets the number of units of each team at the start of the match.
        /// </summary>
        public int StartingUnits { get => m_StartingUnits; }


        /// <summary>
        /// An array storing the number of followersInUnit in each team.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly int[] m_Population = new int[2];

        /// <summary>
        /// An array storing the active unit behavior for each team.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly UnitBehavior[] m_ActiveBehavior = new UnitBehavior[2];

        /// <summary>
        /// An array of lists containing all the active knights of each team.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly List<Unit>[] m_Knights = new List<Unit>[] { new(), new() };

        /// <summary>
        /// An array of grids representing the terrain which store the amount of times the units of each team have visited each terrain point.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly int[][,] m_GridPointSteps = new int[2][,];


        /// <summary>
        /// An array of the leaders in each team that are part of a unit, null if the team's leader is not in a unit.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly Unit[] m_LeaderUnits = new Unit[2];


        #region Fights

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

        #endregion

        #endregion


        #region Actions

        /// <summary>
        /// Action to be called when the behavior of the units in the red team is changed.
        /// </summary>
        public Action<UnitBehavior> OnRedBehaviorChange;
        /// <summary>
        /// Action to be called when the behavior of the units in the blue team is changed.
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
        /// Creates a unit of the given team at the given location and spawns it on the network.
        /// </summary>
        /// <param name="location">The <c>TerrainPoint</c> at which the new unit should be spawned.</param>
        /// <param name="team">The team the new unit should belong to.</param>
        /// <param name="unitClass">The class of the unit, Walker by default.</param>
        /// <param name="followers">The initial number of followersInUnit in the unit.</param>
        /// <param name="origin">The settlement the unit came out from, null for the starting units.</param>
        /// <returns>The <c>GameObject</c> of the newly spawned unit.</returns>
        public GameObject SpawnUnit(TerrainPoint location, Team team, UnitClass unitClass = UnitClass.WALKER, int followers = 1, Settlement origin = null)
        {
            if (!IsHost || followers == 0) return null;

            GameObject unitObject = Instantiate(
                m_UnitPrefab,
                new Vector3(
                    location.GridX * Terrain.Instance.UnitsPerTileSide,
                    Terrain.Instance.GetPointHeight((location.GridX, location.GridZ)),
                    location.GridZ * Terrain.Instance.UnitsPerTileSide),
                Quaternion.identity
            );

            Unit unit = unitObject.GetComponent<Unit>();

            // subscribe to events
            OnNewLeaderGained += unit.NewLeaderUnitGained;
            OnRemoveReferencesToUnit += unit.RemoveRefrencesToUnit;
            GameController.Instance.OnTerrainModified += unit.CheckIfTargetTileFlat;
            GameController.Instance.OnTerrainModified += unit.RecomputeHeight;
            GameController.Instance.OnFlood += unit.CheckIfTargetTileFlat;
            GameController.Instance.OnFlood += unit.RecomputeHeight;
            StructureManager.Instance.OnRemoveReferencesToSettlement += unit.RemoveRefrencesToSettlement;

            if (team == Team.RED)
            {
                OnRedBehaviorChange += unit.SetBehavior;
                GameController.Instance.OnRedMagnetMoved += unit.SymbolLocationChanged;
            }
            else if (team == Team.BLUE)
            {
                OnBlueBehaviorChange += unit.SetBehavior;
                GameController.Instance.OnBlueMagnetMoved += unit.SymbolLocationChanged;
            }

            unit.Setup(team, followers, origin);
            unit.SetBehavior(m_ActiveBehavior[(int)team]);

            if (unitClass == UnitClass.LEADER)
                SetUnitLeader(team, unit);

            if (unitClass == UnitClass.KNIGHT)
                unit.SetClass(UnitClass.KNIGHT);

            // spawn on network
            NetworkObject networkUnit = unitObject.GetComponent<NetworkObject>();
            networkUnit.Spawn(true);
            SetupUnitClientRpc(networkUnit.NetworkObjectId, $"{team} Unit", 
                GameController.Instance.TeamColors[(int)team], LayerData.TeamLayers[(int)team]);
            unitObject.transform.parent = gameObject.transform;

            return unitObject;
        }

        /// <summary>
        /// Sets up some <c>GameObject</c> properties for the given unit on each client.
        /// </summary>
        /// <param name="unitNetworkId">The <c>NetworkObjectId</c> used to identify the unit.</param>
        /// <param name="name">The name for the <c>GameObject</c> of the unit.</param>
        /// <param name="color">The color the body of the unit should be set to.</param>
        /// <param name="layer">An integer representing the layer the unit should be on.</param>
        [ClientRpc]
        private void SetupUnitClientRpc(ulong unitNetworkId, string name, Color color, int layer)
        {
            GameObject unitObject = GetNetworkObject(unitNetworkId).gameObject;
            unitObject.name = name;
            unitObject.GetComponent<MeshRenderer>().material.color = color;
            unitObject.layer = layer;
        }

        /// <summary>
        /// Despawns the given unit from the network and destroys is.
        /// </summary>
        /// <param name="unitObject">The <c>GameObject</c> of the unit to be destroyed.</param>
        /// <param name="hasDied">True if the unit is being despawned because it died, 
        /// false if it is being despawned because it entered a origin.</param>
        public void DespawnUnit(GameObject unitObject, bool hasDied)
        {
            if (!IsHost) return;

            Unit unit = unitObject.GetComponent<Unit>();

            // cleanup event subscriptions
            GameController.Instance.OnTerrainModified -= unit.RecomputeHeight;
            GameController.Instance.OnTerrainModified -= unit.CheckIfTargetTileFlat;
            GameController.Instance.OnFlood -= unit.RecomputeHeight;
            GameController.Instance.OnFlood -= unit.CheckIfTargetTileFlat;

            if (unit.Team == Team.RED)
                OnRedBehaviorChange -= unit.SetBehavior;
            else if (unit.Team == Team.BLUE)
                OnBlueBehaviorChange -= unit.SetBehavior;


            if (unit.Class == UnitClass.LEADER)
            {
                UnsetUnitLeader(unit.Team);

                if (hasDied)
                {
                    GameController.Instance.RemoveManna(unit.Team, m_LeaderDeathManna);
                    GameController.Instance.AddManna(unit.Team == Team.RED ? Team.BLUE : Team.RED, m_LeaderDeathManna);
                    StructureManager.Instance.SetMagnetPosition(unit.Team, unit.ClosestMapPoint.ToWorldPosition());
                }
            }

            if (unit.Class == UnitClass.KNIGHT)
            {
                m_Knights[(int)unit.Team].Remove(unit);
                unit.SetClass(UnitClass.WALKER);
            }

            GameController.Instance.RemoveVisibleObject_ClientRpc(
                unitObject.GetComponent<NetworkObject>().NetworkObjectId, 
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { GameData.Instance.GetNetworkIdByTeam(unit.Team) }
                    }
                }
            );

            if (unit.IsInspected)
                GameController.Instance.RemoveInspectedObject(unit);

            OnRemoveReferencesToUnit?.Invoke(unit);

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

            ResetGridSteps(Team.RED);
            ResetGridSteps(Team.BLUE);

            List<(int, int)> redSpawnPoints = new();
            List<(int, int)> blueSpawnPoints = new();

            FindSpawnPoints(ref redSpawnPoints, ref blueSpawnPoints);

            if (m_StartingUnits * m_StartingUnitPopulation > m_MaxPopulation)
                m_StartingUnitPopulation = 1;

            if (m_StartingUnits > m_MaxPopulation)
                m_StartingUnits = m_MaxPopulation;

            Random random = new(!GameData.Instance ? 0 : GameData.Instance.MapSeed);

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
                            team == 0 ? Team.RED : Team.BLUE, 
                            followers: m_StartingUnitPopulation, 
                            unitClass: spawned == leader ? UnitClass.LEADER : UnitClass.WALKER, 
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
                        team == 0 ? Team.RED : Team.BLUE,
                        followers: m_StartingUnitPopulation,
                        unitClass: spawned == leader ? UnitClass.LEADER : UnitClass.WALKER,
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

                    foreach ((int x, int z) tile in tiles)
                    {
                        // we want to get at most twice the amount of tiles as there are units to spawn
                        if (redSpawns.Count <= 2 * m_StartingUnits && !blueSpawns.Contains(tile) &&
                            !StructureManager.Instance.IsTileOccupied(tile) && !Terrain.Instance.IsTileUnderwater(tile))
                            redSpawns.Add(tile);

                        (int x, int z) oppositeTile = (Terrain.Instance.TilesPerSide - tile.x - 1, Terrain.Instance.TilesPerSide - tile.z - 1);

                        if (blueSpawns.Count <= 2 * m_StartingUnits && !redSpawns.Contains(oppositeTile) &&
                            !StructureManager.Instance.IsTileOccupied(oppositeTile) && !Terrain.Instance.IsTileUnderwater(oppositeTile))
                            blueSpawns.Add(oppositeTile);

                        if (redSpawns.Count > 2 * m_StartingUnits && blueSpawns.Count > 2 * m_StartingUnits)
                            return;
                    }
                }
            }
        }

        #endregion


        #region Population

        /// <summary>
        /// Checks whether the population of the given team has reached the maximum number of followersInUnit.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose population should be checked.</param>
        /// <returns>True if the team is full, false otherwise.</returns>
        public bool IsTeamFull(Team team) => m_Population[(int)team] == m_MaxPopulation;

        /// <summary>
        /// Adds the given amount to the population of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> the population should be added to.</param>
        /// <param name="amount">The amount of population that should be added.</param>
        public void AddPopulation(Team team, int amount = 1)
        {
            SetPopulation(team, Mathf.Clamp(m_Population[(int)team] + amount, 0, m_MaxPopulation));
            GameController.Instance.AddManna(team);
        }

        /// <summary>
        /// Removes the given amount from the population of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> the population should be removed from.</param>
        /// <param name="amount">The amount of population that should be removed.</param>
        public void RemovePopulation(Team team, int amount = 1)
            => SetPopulation(team, Mathf.Clamp(m_Population[(int)team] - amount, 0, m_MaxPopulation));

        /// <summary>
        /// Sets the population of the given team to the given amount.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose population should be set.</param>
        /// <param name="amount">The amount of population the given team should have.</param>
        private void SetPopulation(Team team, int amount)
        {
            if (amount == m_Population[(int)team]) return;
            m_Population[(int)team] = amount;

            UpdatePopulationUI_ClientRpc(team, amount);

            if (m_Population[(int)team] == 0)
                GameController.Instance.EndGame_ClientRpc(winner: team == Team.RED ? Team.BLUE : Team.RED); ;
        }

        /// <summary>
        /// Updates the display of the population numbers shown on the UI of both players.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose population should be updated.</param>
        /// <param name="currentPopulation">The current amount of population.</param>
        [ClientRpc]
        public void UpdatePopulationUI_ClientRpc(Team team, int currentPopulation)
            => GameUI.Instance.UpdatePopulationBar(team, currentPopulation);

        #endregion


        #region Behavior

        /// <summary>
        /// Switches the behavior of all the units in the given team to the given behavior.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose units should be targeted.</param>
        /// <param name="behavior">The <c>UnitBehavior</c> that should be applied to all units in the team.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ChangeUnitBehavior_ServerRpc(Team team, UnitBehavior behavior)
        {
            if (m_ActiveBehavior[(int)team] == behavior) return;

            m_ActiveBehavior[(int)team] = behavior;

            if (team == Team.RED)
                OnRedBehaviorChange?.Invoke(behavior);
            else if (team == Team.BLUE)
                OnBlueBehaviorChange?.Invoke(behavior);
        }

        #endregion


        #region Knight

        /// <summary>
        /// Gets the knight from the given team that is at the given index in the list of knights.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the returned knight should belong to.</param>
        /// <param name="index">The index of the knight that should be returned.</param>
        /// <returns>A <c>Unit</c> of the Knight class from the given team.</returns>
        public Unit GetKnight(Team team, int index) => index >= m_Knights[(int)team].Count ? null : m_Knights[(int)team][index];

        /// <summary>
        /// Returns the number of knights in the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the returned knight should belong to.</param>
        /// <returns>The number of knights in the given team.</returns>
        public int GetKnightsNumber(Team team) => m_Knights[(int)team].Count;

        /// <summary>
        /// Turns the leader of the given team into a knight, if the team has a leader.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the new knight should belong to.</param>
        /// <returns>The <c>Unit</c> that has been turned into a knight, or null if no such unit exists.</returns>
        public Unit CreateKnight(Team team)
        {
            Unit knight = null;

            // if the leader is in a unit, just turn that unit into a knight
            if (HasUnitLeader(team))
            {
                knight = GetLeaderUnit(team);
                UnsetUnitLeader(team);
                knight.SetClass(UnitClass.KNIGHT);
                m_Knights[(int)team].Add(knight);
            }

            // if the leader is in a origin, destroy that origin and spawnPoint a knight in its position
            if (StructureManager.Instance.HasSettlementLeader(team))
            {
                Settlement settlement = StructureManager.Instance.GetLeaderSettlement(team);
                StructureManager.Instance.UnsetLeaderSettlement(team);
                knight = SpawnUnit(
                    location: settlement.OccupiedTile, 
                    team, 
                    unitClass: UnitClass.KNIGHT, 
                    followers: settlement.FollowersInSettlement,
                    origin: settlement
                ).GetComponent<Unit>();
                settlement.DestroySettlement(updateNeighbors: true);
            }

            return knight;
        }

        #endregion


        #region Grid Steps

        /// <summary>
        /// Increments the number of times a terrain point has been stepped on by units of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose unit steps should be incremented.</param>
        /// <param name="point">The (x, z)-coordinates of the terrain point that is being stepped on.</param>
        public void AddStepAtPoint(Team team, (int x, int z) point) => m_GridPointSteps[(int)team][point.x, point.z]++;

        /// <summary>
        /// Gets the number of times a terrain point has been stepped on by units of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose unit steps should be returned.</param>
        /// <param name="point">The (x, z)-coordinates of the terrain point whose number of steps should be returned.</param>
        /// <returns>The number of times the terrain point has been stepped on by units of the team.</returns>
        public int GetStepsAtPoint(Team team, (int x, int z) point) => m_GridPointSteps[(int)team][point.x, point.z];

        /// <summary>
        /// Removes the step counts of units of the given team from all the terrain points.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose unit steps should be removed.</param>
        public void ResetGridSteps(Team team) 
            => m_GridPointSteps[(int)team] = new int[Terrain.Instance.TilesPerSide + 1, Terrain.Instance.TilesPerSide + 1];

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
                red.LoseFollowers(1);
                blue.LoseFollowers(1);

                if (red.Followers == 0 || blue.Followers == 0)
                    break;
            }

            Unit winner = null, loser = null;
            if (!red) winner = blue;
            else if (!blue) winner = red;
            else if (red && blue)
            {
                winner = red.Followers == 0 ? blue : red;
                loser = red.Followers == 0 ? red : blue;
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
            if (settlement.FollowersInSettlement >= settlement.FollowersInUnit)
                followersInUnit = settlement.FollowersInUnit;
            else if (settlement.FollowersInSettlement > 0)
                followersInUnit = settlement.FollowersInSettlement;

            Unit other = SpawnUnit(
                location: unit.ClosestMapPoint, 
                team: settlement.Team, 
                unitClass: UnitClass.WALKER, 
                followers: followersInUnit,
                origin: settlement
            ).GetComponent<Unit>();

            if (!other)
            {
                ResolveSettlementAttack(unit, settlement);
                return;
            }

            StartFight(unit.Team == Team.RED ? unit : other, unit.Team == Team.BLUE ? unit : other, settlement);
        }

        /// <summary>
        /// Handles the aftermath of the attack by a unit on the enemy origin.
        /// </summary>
        /// <param name="winner">The <c>Unit</c> that won the battle for the origin.</param>
        /// <param name="settlement">The <c>Settlement</c> that was being attacked.</param>
        private void ResolveSettlementAttack(Unit winner, Settlement settlement)
        {
            if (winner.Team == settlement.Team) return;

            if (winner.Class == UnitClass.KNIGHT)
                settlement.BurnSettlementDown();
            else
                StructureManager.Instance.SwitchTeam(settlement, winner.Team);

            settlement.IsAttacked = false;
        }

        #endregion


        /// <summary>
        /// Checks whether the given team has a leader that is in a unit.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be checked.</param>
        /// <returns>True if the team as a leader that is in a unit, false otherwise.</returns>
        public bool HasUnitLeader(Team team) => m_LeaderUnits[(int)team];

        /// <summary>
        /// Gets the <c>Unit</c> the team leader is part of, if such a unit exists.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be returned.</param>
        /// <returns>The <c>Unit</c> of the team's leader, null if the leader is not part of a unit..</returns>
        public Unit GetLeaderUnit(Team team) => m_LeaderUnits[(int)team];

        public void SetUnitLeader(Team team, Unit unit)
        {
            UnsetUnitLeader(team);

            m_LeaderUnits[(int)team] = unit;
            unit.SetClass(UnitClass.LEADER);
            OnNewLeaderGained?.Invoke();
        }

        public void UnsetUnitLeader(Team team)
        {
            if (!HasUnitLeader(team)) return;

            m_LeaderUnits[(int)team].SetClass(UnitClass.WALKER);
            m_LeaderUnits[(int)team] = null;
        }

    }
}