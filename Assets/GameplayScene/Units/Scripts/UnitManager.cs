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
    public class UnitManager : NetworkBehaviour
    {
        #region Inspector Fields

        [Tooltip("The GameObject that should be created when a unit is spawned.")]
        [SerializeField] private GameObject m_UnitPrefab;
        [Tooltip("The maximum number of followers for each faction.")]
        [SerializeField] private int m_MaxFollowers = 100;
        [Tooltip("The number of unit steps after which the unit loses one follower.")]
        [SerializeField] private int m_UnitDecayRate = 20;
        [Tooltip("The number of seconds between each time the units in a fight deal damage to each other.")]
        [SerializeField] private float m_FightWaitDuration = 5f;
        [Tooltip("The amount of manna lost when a leader dies.")]
        [SerializeField] private int m_LeaderDeathMannaLoss = 10;

        [Header("Starting Units")]
        [Tooltip("The number of units each faction has at the start of the match.")]
        [SerializeField] private int m_StartingUnits = 15;
        [Tooltip("The amount of followers in each of the starting units.")]
        [SerializeField] private int m_StartingUnitStrength = 1;

        [Header("UI")]
        [Tooltip("The scale of the unit icons on the minimap.")]
        [SerializeField] private int m_MinimapIconScale = 30;
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
        public int MaxFollowers { get => m_MaxFollowers; }
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
        /// Action to be called when a new unit is assigned as the leader of the red faction.
        /// </summary>
        public Action OnRedLeaderChange;
        /// <summary>
        /// Action to be called when a new unit is assigned as the leader of the blue faction.
        /// </summary>
        public Action OnBlueLeaderChange;
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
        /// <param name="type">The type of the unit, Walker by default.</param>
        /// <param name="strength">The initial number of followers in the unit, 1 by default.</param>
        /// <param name="origin">The settlement the unit was created by, null for the starting units.</param>
        /// <returns>The <c>GameObject</c> of the newly spawned unit.</returns>
        public GameObject SpawnUnit(TerrainPoint location, Faction faction, UnitType type = UnitType.WALKER, int strength = 1, Settlement origin = null)
        {
            if (!IsHost || strength == 0) return null;

            if (m_UnitPrefab.GetComponent<Renderer>())
                m_UnitPrefab.GetComponent<Renderer>().enabled = false;

            GameObject unitObject = Instantiate(m_UnitPrefab, location.ToScenePosition(), Quaternion.identity);

            // spawn on network
            NetworkObject networkUnit = unitObject.GetComponent<NetworkObject>();
            networkUnit.Spawn(true);

            Unit unit = unitObject.GetComponent<Unit>();

            unit.Setup(faction, strength, origin);
            unit.SetBehavior(m_ActiveBehavior[(int)faction]);

            if (type == UnitType.LEADER)
                GameController.Instance.SetLeader(faction, unit);

            if (type == UnitType.KNIGHT)
                SetKnight(faction, unit);

            return unitObject;
        }

        /// <summary>
        /// Despawns the given unit from the network and destroys is.
        /// </summary>
        /// <param name="unitObject">The <c>GameObject</c> of the unit to be destroyed.</param>
        /// <param name="hasDied">True if the unit is being despawned because it died, false if it is being despawned because it entered a settlement.</param>
        public void DespawnUnit(Unit unit, bool hasDied)
        {
            if (!IsHost) return;

            unit.Cleanup();
            OnRemoveReferencesToUnit?.Invoke(unit);

            GameController.Instance.RemoveVisibleObject_ClientRpc(
                GetComponent<NetworkObject>().NetworkObjectId,
                GameUtils.GetClientParams(GameData.Instance.GetNetworkIdByFaction(unit.Faction))
            );

            if (unit.IsInspected)
                QueryModeController.Instance.RemoveInspectedObject(unit);

            if (unit.Type == UnitType.LEADER)
            {
                GameController.Instance.SetLeader(unit.Faction, null);

                if (hasDied)
                {
                    DivineInterventionController.Instance.RemoveManna(unit.Faction, m_LeaderDeathMannaLoss);
                    DivineInterventionController.Instance.AddManna(unit.Faction == Faction.RED ? Faction.BLUE : Faction.RED, m_LeaderDeathMannaLoss);
                    GameController.Instance.PlaceUnitMagnetAtPoint(unit.Faction, unit.ClosestTerrainPoint);
                }
            }

            if (unit.Type == UnitType.KNIGHT)
                RemoveKnight(unit.Faction, unit);

            unit.GetComponent<NetworkObject>().Despawn();
            Destroy(unit.gameObject);
        }

        #endregion


        #region Starter Units

        /// <summary>
        /// Creates the starting units for both factions.
        /// </summary>
        public void SpawnStartingUnits()
        {
            ResetGridSteps(Faction.RED);
            ResetGridSteps(Faction.BLUE);

            if (m_StartingUnits * m_StartingUnitStrength > m_MaxFollowers)
                m_StartingUnitStrength = 1;

            if (m_StartingUnits > m_MaxFollowers)
                m_StartingUnits = m_MaxFollowers;

            Random random = new(!GameData.Instance ? 0 : GameData.Instance.GameSeed);

            (List<TerrainPoint> redSpawnPoints, List<TerrainPoint> blueSpawnPoints) = FindSpawnPoints();

            // go over both factions
            for (int faction = 0; faction <= 1; ++faction)
            {
                List<TerrainPoint> spawnPoints = faction == 0 ? redSpawnPoints : blueSpawnPoints;
                List<int> spawnIndices = Enumerable.Range(0, spawnPoints.Count).ToList();
                int leader = random.Next(0, m_StartingUnits);

                int spawned = 0;

                // shuffle algorithm
                int count = spawnIndices.Count;
                foreach (TerrainPoint spawnPoint in spawnPoints)
                {
                    count--;
                    int randomIndex = random.Next(count + 1);
                    (spawnIndices[count], spawnIndices[randomIndex]) = (spawnIndices[randomIndex], spawnIndices[count]);

                    if (spawnIndices[count] < m_StartingUnits)
                    {
                        SpawnUnit(
                            location: spawnPoint,
                            faction: faction == 0 ? Faction.RED : Faction.BLUE,
                            strength: m_StartingUnitStrength,
                            type: spawned == leader ? UnitType.LEADER : UnitType.WALKER,
                            origin: null
                        );
                        spawned++;
                    }
                }

                if (spawned == m_StartingUnits || spawnPoints.Count == 0) continue;

                // if there weren't enough places to spawn all the units we want on, spawn the remaining units randomly
                for (int i = 0; i < m_StartingUnits - spawned; ++i)
                {
                    SpawnUnit(
                        location: spawnPoints[random.Next(spawnPoints.Count)],
                        faction: faction == 0 ? Faction.RED : Faction.BLUE,
                        strength: m_StartingUnitStrength,
                        type: spawned == leader ? UnitType.LEADER : UnitType.WALKER,
                        origin: null
                    );
                    spawned++;
                }
            }
        }

        /// <summary>
        /// Finds a list of terrain points for each faction that that faction's units can be spawned on.
        /// </summary>
        /// <returns>A tuple, the first element of which is a list of <c>TerrainPoints</c> the red units can be spawned on,
        /// and the second element is a list of <c>TerrainPoints</c> the blue units can be spawned on.</returns>
        private (List<TerrainPoint>, List<TerrainPoint>) FindSpawnPoints()
        {
            List<TerrainPoint> redSpawns = new();
            List<TerrainPoint> blueSpawns = new();

            for (int z = 0; z <= Terrain.Instance.TilesPerSide / 2; ++z)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerSide / 2; ++x)
                {
                    TerrainPoint redPoint = new(x, z);
                    TerrainPoint bluePoint = new(Terrain.Instance.TilesPerSide - x, Terrain.Instance.TilesPerSide - z);

                    if (!redPoint.IsUnderwater()) 
                        redSpawns.Add(redPoint);

                    if (!bluePoint.IsUnderwater()) 
                        blueSpawns.Add(bluePoint);
                }
            }

            return (redSpawns, blueSpawns);
        }

        #endregion


        #region Followers

        public int GetFactionSize(Faction faction) => m_Followers[(int)faction];

        /// <summary>
        /// Checks whether the given faction has reached the maximum number of followers.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose population should be checked.</param>
        /// <returns>True if the faction is full, false otherwise.</returns>
        public bool IsFactionFull(Faction faction) => GetFactionSize(faction) == m_MaxFollowers;

        /// <summary>
        /// Adds the given amount of followers to the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the followers should be added to.</param>
        /// <param name="amount">The amount of followers that should be added.</param>
        public void AddFollowers(Faction faction, int amount = 1)
        {
            amount = Mathf.Clamp(m_Followers[(int)faction] + amount, 0, m_MaxFollowers);
            SetFollowers(faction, amount);
            DivineInterventionController.Instance.AddManna(faction, amount);
        }

        /// <summary>
        /// Removes the given amount of followers from the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the followers should be removed from.</param>
        /// <param name="amount">The amount of followers that should be removed.</param>
        public void RemoveFollowers(Faction faction, int amount = 1)
            => SetFollowers(faction, Mathf.Clamp(m_Followers[(int)faction] - amount, 0, m_MaxFollowers));

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
            ResetGridSteps(faction);

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

        /// <summary>
        /// Called when a new leader has been created, informs the units of the change.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> that got a new leader.</param>
        public void SwitchLeaderTarget(Faction faction)
        {
            if (m_ActiveBehavior[(int)faction] != UnitBehavior.GO_TO_MAGNET) return;

            if (faction == Faction.RED)
                OnRedLeaderChange?.Invoke();

            if (faction == Faction.BLUE)
                OnBlueLeaderChange?.Invoke();
        }

        #endregion


        #region Knight

        /// <summary>
        /// Gets the knight from the given faction that is at the given index in the list of knights.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> that the returned knight should belong to.</param>
        /// <param name="index">The index of the knight that should be returned.</param>
        /// <returns>A <c>Unit</c> of the Knight class from the given faction.</returns>
        public Unit GetKnight(Faction faction, int index) 
            => index >= m_Knights[(int)faction].Count ? null : m_Knights[(int)faction][index];

        /// <summary>
        /// Returns the number of knights in the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose knight number should be returned.</param>
        /// <returns>The number of knights in the given faction.</returns>
        public int GetKnightsNumber(Faction faction) => m_Knights[(int)faction].Count;

        /// <summary>
        /// Sets the given unit as a Knight unit of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> that should gain a knight.</param>
        /// <param name="unit">The <c>Unit</c> that should be set as the knight.</param>
        public void SetKnight(Faction faction, Unit unit)
        {
            m_Knights[(int)faction].Add(unit);
            unit.SetType(UnitType.KNIGHT);
        }

        /// <summary>
        /// Removes the given knight unit from the given faction's list of knights.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose knight should be removed.</param>
        /// <param name="knight">The <c>Unit</c> that is the knight that should be removed.</param>
        private void RemoveKnight(Faction faction, Unit knight)
        {
            m_Knights[(int)faction].Remove(knight);
            knight.SetType(UnitType.WALKER);
        }

        #endregion


        #region Fights

        /// <summary>
        /// Gets the number of fights currently happening.
        /// </summary>
        /// <returns>The number of fights.</returns>
        public int GetFightsNumber() => m_FightIds.Count;

        /// <summary>
        /// Gets the location of the fight at the given index in the fight IDs list.
        /// </summary>
        /// <param name="index">The index of the fight in the fight IDs list whose location we want.</param>
        /// <returns>The position of the fight in the scene.</returns>
        public Vector3? GetFightLocation(int index) 
            => index >= m_FightIds.Count ? null : m_Fights[m_FightIds[index]].red.gameObject.transform.position;

        /// <summary>
        /// Gets the two units participating in the fight with the given ID.
        /// </summary>
        /// <param name="fightId">The ID of the fight.</param>
        /// <returns>A tuple of participants in the fight, where the first element is the red unit and the second element is the blue unit.</returns>
        public (Unit red, Unit blue) GetFightParticipants(int fightId) => m_Fights[fightId];


        /// <summary>
        /// Sets up and begins a fight between the given two units.
        /// </summary>
        /// <param name="red">The <c>Unit</c> from the red faction.</param>
        /// <param name="blue">The <c>Unit</c> from the blue faction.</param>
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
        /// Handles the fighting between two units.
        /// </summary>
        /// <param name="red">The <c>Unit</c> from the red faction.</param>
        /// <param name="blue">The <c>Unit</c> from the blue faction.</param>
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

                if (red.IsInspected || blue.IsInspected)
                    QueryModeController.Instance.UpdateInspectedFight(red, blue);
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
        /// Handles the attack of a unit on an enemy settlement.
        /// </summary>
        /// <param name="unit">the <c>Unit</c> which is attacking the settlement.</param>
        /// <param name="settlement">The <c>Settlement</c> that is being attacked.</param>
        public void AttackSettlement(Unit unit, Settlement settlement)
        {
            // one unit can attack a origin at a time
            if (settlement.IsAttacked) return;

            if (settlement.FollowersInSettlement <= 1)
            {
                ResolveSettlementAttack(unit, settlement);
                return;
            }

            unit.StartFight(-1);
            Unit otherUnit = settlement.StartFight(unit.ClosestTerrainPoint);

            StartFight(unit.Faction == Faction.RED ? unit : otherUnit, unit.Faction == Faction.BLUE ? unit : otherUnit, settlement);
        }

        /// <summary>
        /// Handles the aftermath of the attack by a unit on the enemy settlement.
        /// </summary>
        /// <param name="winner">The <c>Unit</c> that won the battle for the settlement.</param>
        /// <param name="settlement">The <c>Settlement</c> that was being attacked.</param>
        private void ResolveSettlementAttack(Unit winner, Settlement settlement)
        {
            settlement.EndFight();

            if (winner.Faction == settlement.Faction) return;

            if (winner.Type == UnitType.KNIGHT)
                StructureManager.Instance.BurnSettlementDown(settlement);
            else
                StructureManager.Instance.ChangeSettlementFaction(settlement, winner.Faction);
        }

        #endregion
    }
}