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
        /// A walker which carries the faction symbol and can become a knight.
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
        /// The unit goes to its faction's symbol.
        /// </summary>
        GO_TO_SYMBOL,
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
    /// The <c>UnitManager</c> class is a <c>MonoBehavior</c> which manages all the units in the player's team.
    /// </summary>
    public class UnitManager : NetworkBehaviour
    {
        [SerializeField] private GameObject m_UnitPrefab;
        [SerializeField] private int m_StartingUnits = 15;
        [SerializeField] private int m_MaxUnits = 100;
        [SerializeField] private int m_MaxUnitStrength = 100;
        [SerializeField] private int m_UnitDecayRate = 20;

        private static UnitManager m_Instance;
        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        public static UnitManager Instance { get => m_Instance; }

        /// <summary>
        /// Gets the maximum strength a unit can have.
        /// </summary>
        public int MaxStrength { get => m_MaxUnitStrength; }
        /// <summary>
        /// Gets the number of steps after which a unit loses one strength.
        /// </summary>
        public int DecayRate { get => m_UnitDecayRate; }

        /// <summary>
        /// An array of representations of the points on the terrain grid and the number of times a unit of a team has stepped on each point.
        /// </summary>
        private readonly int[][,] m_GridPointSteps = new int[Enum.GetValues(typeof(Team)).Length - 1][,];
        /// <summary>
        /// An array of the sizes of the population of each team.
        /// </summary>
        private readonly int[] m_Population = new int[Enum.GetValues(typeof(Team)).Length];

        /// <summary>
        /// An array of the leaders of each team.
        /// </summary>
        private readonly Unit[] m_Leaders = new Unit[Enum.GetValues(typeof(Team)).Length];
        /// <summary>
        /// An array of lists containing all the active knights of each team.
        /// </summary>
        private readonly List<Unit>[] m_Knights = new List<Unit>[] { new(), new() };
        /// <summary>
        /// A list of the locations of all active battles.
        /// </summary>
        private readonly List<int> m_FightIds = new();
        private readonly Dictionary<int, (Unit red, Unit blue)> m_Fights = new();
        private int m_NextFightId = 0;

        /// <summary>
        /// Action to be called when the state of the units in the red team is changed.
        /// </summary>
        public Action<UnitBehavior> OnRedStateChange;
        /// <summary>
        /// Action to be called when the state of the units in the blue team is changed.
        /// </summary>
        public Action<UnitBehavior> OnBlueStateChange;
        /// <summary>
        /// Action to be called when a unit is despawned to remove references to it from other objects.
        /// </summary>
        public Action<Unit> OnRemoveReferencesToUnit;


        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }


        #region Spawn/Despawn

        /// <summary>
        /// Creates a unit of the given team at the given location and spawns it on the network.
        /// </summary>
        /// <param name="location">The <c>MapPoint</c> at which the new unit should be spawned.</param>
        /// <param name="team">The team the new unit should belong to.</param>
        /// <param name="strength">The initial strength of the unit.</param>
        /// <param name="isLeader">True if the created unit is a leader, false otherwise.</param>
        /// <returns>The <c>GameObject</c> of the newly spawned unit.</returns>
        public GameObject SpawnUnit(MapPoint location, Team team, int strength = 1, bool isLeader = false)
        {
            //if (!IsServer) return null;

            GameObject unitObject = Instantiate(
                m_UnitPrefab,
                new Vector3(
                    location.GridX * Terrain.Instance.UnitsPerTileSide,
                    Terrain.Instance.GetPointHeight((location.GridX, location.GridZ)),
                    location.GridZ * Terrain.Instance.UnitsPerTileSide),
                Quaternion.identity
            );

            Unit unit = unitObject.GetComponent<Unit>();
            GameController.Instance.OnTerrainMoved += unit.RecomputeHeight;
            OnRemoveReferencesToUnit += unit.RemoveRefrencesToUnit;
            StructureManager.Instance.OnRemoveReferencesToSettlement += unit.RemoveRefrencesToSettlement;

            if (team == Team.RED)
            {
                OnRedStateChange += unit.SetBehavior;
                GameController.Instance.OnRedFlagMoved += unit.SymbolLocationChanged;
            }
            else if (team == Team.BLUE)
            {
                OnBlueStateChange += unit.SetBehavior;
                GameController.Instance.OnBlueFlagMoved += unit.SymbolLocationChanged;
            }

            unit.Setup(team, strength);

            if (isLeader)
                SetLeader(unit, team);

            //NetworkObject networkUnit = unitObject.GetComponent<NetworkObject>();
            //networkUnit.Spawn(true);
            //SetupUnitClientRpc(
            //    networkUnit.NetworkObjectId,
            //    $"{team} Unit",
            //    GameController.Instance.TeamColors[(int)team],
            //    LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team])
            //);
            // unitObject.transform.parent = gameObject.transform;

            unitObject.GetComponent<MeshRenderer>().material.color = GameController.Instance.TeamColors[(int)team];
            unitObject.name = $"{team} Unit";
            unitObject.layer = LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]);

            AddPopulation(team);
            GameController.Instance.AddManna(team);

            return unitObject;
        }

        /// <summary>
        /// Sets up the some <c>GameObject</c> properties for the given unit on each client.
        /// </summary>
        /// <param name="unitNetworkId">The <c>NetworkObjectId</c> of the unit.</param>
        /// <param name="name">The name for the <c>GameObject</c> of the unit.</param>
        /// <param name="color">The color the body of the unit should be set to.</param>
        /// <param name="layer">An <c>int</c> representing the layer the unit should be on.</param>
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
        public void DespawnUnit(GameObject unitObject)
        {
            //if (!IsServer) return;

            Unit unit = unitObject.GetComponent<Unit>();
            GameController.Instance.OnTerrainMoved -= unit.RecomputeHeight;

            if (unit.Team == Team.RED)
                OnRedStateChange -= unit.SetBehavior;
            else if (unit.Team == Team.BLUE)
                OnBlueStateChange -= unit.SetBehavior;

            if (unit.Class == UnitClass.LEADER)
                RemoveLeader(unit.Team);

            if (unit.Class == UnitClass.KNIGHT)
                DestroyKnight(unit.Team, unit);

            OnRemoveReferencesToUnit?.Invoke(unit);

            RemovePopulation(unit.Team);

            //unitObject.GetComponent<NetworkObject>().Despawn();
            Destroy(unitObject);
        }

        #endregion


        #region Population

        /// <summary>
        /// Adds the given amount to the population of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> the population should be added to.</param>
        /// <param name="amount">The amount of population that should be added.</param>
        public void AddPopulation(Team team, int amount = 1)
        {
            int population = Mathf.Clamp(m_Population[(int)team] + amount, 0, m_MaxUnits);

            if (population == m_Population[(int)team]) return;
            m_Population[(int)team] = population;

            //UpdatePopulationUIClientRpc(team, m_MaxUnits, population);
        }

        /// <summary>
        /// Removes the given amount from the population of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> the population should be removed from.</param>
        /// <param name="amount">The amount of population that should be removed.</param>
        public void RemovePopulation(Team team, int amount = 1)
        {
            int population = Mathf.Clamp(m_Population[(int)team] - amount, 0, m_MaxUnits);

            if (population == m_Population[(int)team]) return;
            m_Population[(int)team] = population;

            //UpdatePopulationUIClientRpc(team, m_MaxUnits, population);
        }

        /// <summary>
        /// Updates the display of the population numbers shown on the player UI.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose population should be updated.</param>
        /// <param name="maxPopulation">The maximum value of the population.</param>
        /// <param name="currentPopulation">The current value of the population.</param>
        [ClientRpc]
        private void UpdatePopulationUIClientRpc(Team team, int maxPopulation, int currentPopulation)
            => GameUI.Instance.UpdatePopulationBar(team, maxPopulation, currentPopulation);

        #endregion


        #region Starter Units

        /// <summary>
        /// Creates the starting units for both teams.
        /// </summary>
        public void SpawnStartingUnits()
        {
            //if (!IsHost) return;

            ResetGridSteps(Team.RED);
            ResetGridSteps(Team.BLUE);

            Random random = new(GameData.Instance == null ? 0 : GameData.Instance.MapSeed);

            List<(int, int)> redSpawns = new();
            List<(int, int)> blueSpawns = new();

            FindSpawnPoints(ref redSpawns, ref blueSpawns);

            for (int team = 0; team <= 1; ++team)
            {
                List<(int, int)> spawns = team == 0 ? redSpawns : blueSpawns;
                List<int> spawnIndices = Enumerable.Range(0, spawns.Count).ToList();
                int leader = random.Next(0, m_StartingUnits);

                int spawned = 0;
                int count = spawnIndices.Count;
                foreach ((int x, int z) spawn in spawns)
                {
                    count--;
                    int randomIndex = random.Next(count + 1);
                    (spawnIndices[count], spawnIndices[randomIndex]) = (spawnIndices[randomIndex], spawnIndices[count]);

                    if (spawnIndices[count] < m_StartingUnits)
                    {
                        SpawnUnit(new MapPoint(spawn.x, spawn.z), team == 0 ? Team.RED : Team.BLUE, isLeader: spawned == leader);
                        spawned++;
                    }
                }

                if (spawned == m_StartingUnits)
                    continue;

                for (int i = 0; i < m_StartingUnits - spawned; ++i)
                {
                    (int x, int z) point = spawns[random.Next(spawns.Count)];
                    SpawnUnit(new MapPoint(point.x, point.z), team == 0 ? Team.RED : Team.BLUE, isLeader: spawned == leader);
                    spawned++;
                }
            }
        }

        /// <summary>
        /// Collects a list of available spawn locations on the terrain for each team.
        /// </summary>
        /// <param name="redSpawns">A reference to the list of available spawns for the red team.</param>
        /// <param name="blueSpawns">A reference to the list of available spawns for the blue team.</param>
        private void FindSpawnPoints(ref List<(int x, int z)> redSpawns, ref List<(int x, int z)> blueSpawns)
        {
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
                        if (redSpawns.Count < 2 * m_StartingUnits && !blueSpawns.Contains(tile) &&
                            !Terrain.Instance.IsTileOccupied(tile) && !Terrain.Instance.IsTileUnderwater(tile))
                            redSpawns.Add(tile);

                        (int x, int z) oppositeTile = (Terrain.Instance.TilesPerSide - tile.x - 1, Terrain.Instance.TilesPerSide - tile.z - 1);

                        if (blueSpawns.Count < 2 * m_StartingUnits && !redSpawns.Contains(oppositeTile) &&
                            !Terrain.Instance.IsTileOccupied(oppositeTile) && !Terrain.Instance.IsTileUnderwater(oppositeTile))
                            blueSpawns.Add(oppositeTile);

                        if (redSpawns.Count >= 2 * m_StartingUnits && blueSpawns.Count >= 2 * m_StartingUnits)
                            return;
                    }
                }
            }
        }

        #endregion


        #region Leader

        /// <summary>
        /// Gets the leader of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be returned.</param>
        /// <returns>The <c>Unit</c> who is the leader of the given team, null if the team doesn't have a leader.</returns>
        public Unit GetLeader(Team team) => m_Leaders[(int)team];

        /// <summary>
        /// Assigns the given unit as the leader of the given team.
        /// </summary>
        /// <param name="leader">The <c>Unit</c> who should be assigned as a leader.</param>
        /// <param name="team">The <c>Team</c> for which the leader should be assigned.</param>
        public void SetLeader(Unit leader, Team team)
        {
            RemoveLeader(team);

            m_Leaders[(int)team] = leader;
            leader.SetClass(UnitClass.LEADER);
        }

        /// <summary>
        /// Removes the leader of the given team, if the team has a leader.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose leader should be removed.</param>
        public void RemoveLeader(Team team)
        {
            Unit leader = m_Leaders[(int)team];

            if (!leader) return;

            leader.SetClass(UnitClass.WALKER);
            m_Leaders[(int)team] = null;
        }

        #endregion


        #region Knight

        /// <summary>
        /// Gets the knight from the given team that is at the given index in the list of knights.
        /// </summary>
        /// <param name="index">The index of the knight that should be returned.</param>
        /// <param name="team">The <c>Team</c> that the returned knight should belong to.</param>
        /// <returns>A <c>Unit</c> of the Knight class from the given team.</returns>
        public Unit GetKnight(int index, Team team) => index >= m_Knights[(int)team].Count ? null : m_Knights[(int)team][index];
        /// <summary>
        /// Gets the last knight of the given team entered in the list of knights.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the returned knight should belong to.</param>
        /// <returns>A <c>Unit</c> of the Knight class from the given team.</returns>
        public Unit GetNewestKnight(Team team) => m_Knights[(int)team].Count == 0 ? null : m_Knights[(int)team][^1];
        /// <summary>
        /// Returns the number of knights in the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the returned knight should belong to.</param>
        /// <returns>The number of knights in the given team.</returns>
        public int GetKnightsNumber(Team team) => m_Knights[(int)team].Count;

        /// <summary>
        /// Turns the leader of the given team into a knight, if a leader exists.
        /// </summary>
        /// <param name="team">The <c>Team</c> that the new knight should belong to.</param>
        public void CreateKnight(Team team)
        {
            Unit unit = m_Leaders[(int)team];
            if (unit == null) return;

            RemoveLeader(team);
            m_Knights[(int)team].Add(unit);
            unit.SetClass(UnitClass.KNIGHT);
        }

        /// <summary>
        /// Destroys the given knight.
        /// </summary>
        /// <param name="team">The <c>Team</c> the knight that should be destroyed belongs to.</param>
        /// <param name="knight">The <c>Unit</c> of the Knight class that should be destroyed.</param>
        public void DestroyKnight(Team team, Unit knight)
        {
            m_Knights[(int)team].Remove(knight);
            knight.SetClass(UnitClass.WALKER);
        }

        #endregion


        #region Unit Behavior

        /// <summary>
        /// Switches the state of all the units in the given team to the given state.
        /// </summary>
        /// <param name="state">The <c>UnitBehavior</c> that should be applied to all units in the team.</param>
        /// <param name="team">The <c>Team</c> whose units should be targeted.</param>
        //[ServerRpc(RequireOwnership = false)]
        public void UnitStateChange/*ServerRpc*/(UnitBehavior state, Team team)
        {
            if (team == Team.RED)
                OnRedStateChange?.Invoke(state);
            else if (team == Team.BLUE)
                OnBlueStateChange?.Invoke(state);
        }

        #endregion


        #region Grid Steps

        /// <summary>
        /// Increments the number of times a point has been stepped on by units of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose unit steps should be incremented.</param>
        /// <param name="point">The (x, z)-coordinates of the point on the terrain grid that is being stepped on.</param>
        public void AddStepAtPoint(Team team, (int x, int z) point) => m_GridPointSteps[(int)team][point.x, point.z]++;

        /// <summary>
        /// Gets the number of times a point on the terrain grid has been stepped on by units of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose unit steps should be returned.</param>
        /// <param name="point">The (x, z)-coordinates of the point on the terrain grid whose number of steps should be returned.</param>
        /// <returns>The number of times the point has been stepped on by units of the team.</returns>
        public int GetStepsAtPoint(Team team, (int x, int z) point) => m_GridPointSteps[(int)team][point.x, point.z];

        /// <summary>
        /// Removes the step counts of units of the given team from all the points on the terrain grid.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose unit steps should be removed.</param>
        public void ResetGridSteps(Team team) 
            => m_GridPointSteps[(int)team] = new int[Terrain.Instance.TilesPerSide + 1, Terrain.Instance.TilesPerSide + 1];

        #endregion


        #region Fights

        /// <summary>
        /// Gets the location of the fight at the given index in the fight locations list.
        /// </summary>
        /// <param name="index">The index of the fight whose location we want.</param>
        /// <returns>A <c>Vector3</c> of the position of the fight.</returns>
        public Vector3 GetFightPosition(int index) => m_Fights[m_FightIds[index]].red.gameObject.transform.position;
        /// <summary>
        /// Gets the number of fights currently happening.
        /// </summary>
        /// <returns>The number of fights.</returns>
        public int GetFightsNumber() => m_FightIds.Count;

        /// <summary>
        /// Sets up and begins a fight between two units.
        /// </summary>
        /// <param name="red">The <c>Unit</c> from the red team.</param>
        /// <param name="blue">The <c>Unit</c> from the blue team.</param>
        /// <param name="settlementDefense">A <c>Settlement</c> if the fight occured due to an attempt to claim a settlement, null otherwise.</param>
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
        /// Ends the fight and destroys the loser.
        /// </summary>
        /// <param name="winner">The <c>Unit</c> who won the battle.</param>
        /// <param name="loser">The <c>Unit</c> who lost the battle.</param>
        public void EndBattle(Unit winner, Unit loser)
        {
            m_Fights.Remove(winner.FightId);
            m_FightIds.Remove(winner.FightId);

            DespawnUnit(loser.gameObject);
            winner.EndFight();
        }

        /// <summary>
        /// Handles the fighting between two units.
        /// </summary>
        /// <param name="red">The <c>Unit</c> from the red team.</param>
        /// <param name="blue">The <c>Unit</c> from the blue team.</param>
        /// <param name="settlementDefense">A <c>Settlement</c> if the fight occured due to an attempt to claim a settlement, null otherwise.</param>
        /// <returns>An <c>IEnumerator</c> which waits for a number of seconds before simulating another attack.</returns>
        private IEnumerator Fight(Unit red, Unit blue, Settlement settlementDefense = null)
        {
            while (true)
            {
                yield return new WaitForSeconds(0.1f);

                red.LoseStrength(1);
                blue.LoseStrength(1);

                if (red.Strength == 0 || blue.Strength == 0)
                    break;
            }

            Unit winner = red.Strength == 0 ? blue : red;
            Unit loser = red.Strength == 0 ? red : blue;

            if (settlementDefense)
                ResolveSettlementAttack(winner, settlementDefense);

            EndBattle(winner, loser);
        }

        /// <summary>
        /// Handles the attack of a unit on a settlement from the enemy team.
        /// </summary>
        /// <param name="unit">the <c>Unit</c> which is attacking the settlement.</param>
        /// <param name="settlement">The <c>Settlement</c> that is being attacked.</param>
        public void AttackSettlement(Unit unit, Settlement settlement)
        {
            settlement.IsAttacked = true;
            unit.ToggleMovement(true);
            Unit other = SpawnUnit(unit.ClosestMapPoint, settlement.Team, settlement.UnitStrength, false).GetComponent<Unit>();

            StartFight(unit.Team == Team.RED ? unit : other, unit.Team == Team.BLUE ? unit : other, settlement);
        }

        /// <summary>
        /// Handles the aftermath of the attack by a unit on the enemy settlement.
        /// </summary>
        /// <param name="winner">The <c>Unit</c> that won the battle for the settlement.</param>
        /// <param name="settlement">The <c>Settlement</c> that was being attacked.</param>
        private void ResolveSettlementAttack(Unit winner, Settlement settlement)
        {
            if (winner.Team == settlement.Team) return;

            if (winner.Class == UnitClass.KNIGHT)
                settlement.RuinSettlement();
            else
                StructureManager.Instance.SwitchTeam(settlement, winner.Team);

            settlement.IsAttacked = false;
        }

        /// <summary>
        /// Shows or hides the fight info on the UI.
        /// </summary>
        /// <param name="show">True if the fight info should be shown, false otherwise.</param>
        /// <param name="fightId">The id of the fight for which the info should be displayed.</param>
        /// <param name="clientId">The id of the client whose screen the UI should be displayed on.</param>
        public void ToggleFightUI(bool show, int fightId, ulong clientId)
        {
            (Unit red, Unit blue) = m_Fights[fightId];
            ToggleFightUIClient/*Rpc*/(show, red.Strength, blue.Strength, new ClientRpcParams()
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { clientId }
                }
            });
        }

        /// <summary>
        /// Shows or hides the fight info on the UI.
        /// </summary>
        /// <param name="show">True if the fight info should be shown, false otherwise.</param>
        /// <param name="redStrength">The strength of the red unit.</param>
        /// <param name="blueStrength">The strength of the blue unit.</param>
        /// <param name="parameters">RPC data for the server RPC.</param>
        //[ClientRpc]
        private void ToggleFightUIClient/*Rpc*/(bool show, int redStrength, int blueStrength, ClientRpcParams parameters = default)
            => GameUI.Instance.ToggleFightStrengthBars(show, redStrength, blueStrength);

        /// <summary>
        /// Updates the fight info on the UI.
        /// </summary>
        /// <param name="fightId">The id of the fight for which the info should be displayed.</param>
        public void UpdateFightUI(int fightId)
        {
            (Unit red, Unit blue) = m_Fights[fightId];
            UpdateFightUIClient/*Rpc*/(red.Strength, blue.Strength);
        }

        /// <summary>
        /// Updates the fight info on the UI.
        /// </summary>
        /// <param name="redStrength">The strength of the red unit.</param>
        /// <param name="blueStrength">The strength of the blue unit.</param>
        /// <param name="parameters">RPC data for the server RPC.</param>
        //[ClientRpc]
        private void UpdateFightUIClient/*Rpc*/(int redStrength, int blueStrength, ClientRpcParams parameters = default)
            => GameUI.Instance.UpdateFightUI(redStrength, blueStrength);

        #endregion
    }
}