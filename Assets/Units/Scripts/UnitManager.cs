using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    public class UnitManager : NetworkBehaviour
    {
        [SerializeField] private GameObject m_UnitPrefab;
        [SerializeField] private UnitData[] m_UnitData;
        [SerializeField] private Color[] m_UnitColors;
        [SerializeField] private int m_StarterUnits = 15;

        private static UnitManager m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static UnitManager Instance { get => m_Instance; }

        private readonly Unit[] m_Leaders = new Unit[Enum.GetNames(typeof(Team)).Length];
        private readonly List<Unit>[] m_Knights = new List<Unit>[] { new(), new() };

        public Color[] UnitColors { get => m_UnitColors; }

        public Action<UnitState> OnRedStateChange;
        public Action<UnitState> OnBlueStateChange;

        private List<Vector2> m_BattlePositions = new();



        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }


        public GameObject SpawnUnit(MapPoint location, Team team, SettlementType origin, bool isLeader)
        {
            //if (!IsServer) return null;

            GameObject unitObject = Instantiate(
                m_UnitPrefab,
                new Vector3(
                    location.TileX * Terrain.Instance.UnitsPerTileSide,
                    m_UnitPrefab.transform.position.y + Terrain.Instance.GetTileCenterHeight((location.TileX, location.TileZ)),
                    location.TileZ * Terrain.Instance.UnitsPerTileSide),
                Quaternion.identity
            );

            Unit unit = unitObject.GetComponent<Unit>();
            GameController.Instance.OnTerrainMoved += unit.RecalculateHeight;

            if (team == Team.RED)
            {
                OnRedStateChange += unit.SwitchState;
                GameController.Instance.OnRedFlagMoved += unit.GoToFlag;
            }
            else if (team == Team.BLUE)
            {
                OnBlueStateChange += unit.SwitchState;
                GameController.Instance.OnBlueFlagMoved += unit.GoToFlag;
            }

            unit.Setup(team, m_UnitData[(int)origin]);
            unit.GetComponent<MeshRenderer>().material.color = m_UnitColors[(int)team];

            if (isLeader)
                SetLeader(unit, team);

            //NetworkObject networkUnit = unitObject.GetComponent<NetworkObject>();
            //networkUnit.Spawn(true);
            //ChangeUnitColorClientRpc(networkUnit.NetworkObjectId, m_UnitColors[(int)team]);

            //unitObject.transform.parent = gameObject.transform;
            unitObject.name = $"{team} Unit";

            return unitObject;
        }

        [ClientRpc]
        private void ChangeUnitColorClientRpc(ulong unitNetworkId, Color color)
            => GetNetworkObject(unitNetworkId).GetComponent<MeshRenderer>().material.color = color;

        public void DespawnUnit(GameObject unitObject)
        {
            if (!IsServer) return;

            Unit unit = unitObject.GetComponent<Unit>();
            GameController.Instance.OnTerrainMoved -= unit.RecalculateHeight;

            if (unit.Team == Team.RED)
                OnRedStateChange -= unit.SwitchState;
            else if (unit.Team == Team.BLUE)
                OnBlueStateChange -= unit.SwitchState;

            if (unit.IsLeader)
                SetLeader(null, unit.Team);

            if (unit.IsKnight)
                DestroyKnight(unit.Team, unit);

            unitObject.GetComponent<NetworkObject>().Despawn();
            Destroy(unitObject);
        }


        #region Starter Units

        public void SpawnStarterUnits()
        {
            Random random = new(GameData.Instance == null ? 0 : GameData.Instance.MapSeed);

            List<(int, int)> redSpawns = new();
            List<(int, int)> blueSpawns = new();

            FindSpawnPoints(ref redSpawns, ref blueSpawns);

            for (int team = 0; team <= 1; ++team)
            {
                List<(int, int)> spawns = team == 0 ? redSpawns : blueSpawns;
                List<int> spawnIndices = Enumerable.Range(0, spawns.Count).ToList();
                int leader = random.Next(0, m_StarterUnits);

                int spawned = 0;
                int count = spawnIndices.Count;
                foreach ((int x, int z) spawn in spawns)
                {
                    count--;
                    int randomIndex = random.Next(count + 1);
                    (spawnIndices[count], spawnIndices[randomIndex]) = (spawnIndices[randomIndex], spawnIndices[count]);

                    if (spawnIndices[count] < m_StarterUnits)
                    {
                        SpawnUnit(new MapPoint(spawn.x, spawn.z), team == 0 ? Team.RED : Team.BLUE, SettlementType.TENT, isLeader: spawned == leader);
                        spawned++;
                    }
                }

                if (spawned == m_StarterUnits)
                    continue;

                for (int i = 0; i < m_StarterUnits - spawned; ++i)
                {
                    (int x, int z) point = spawns[random.Next(spawns.Count)];
                    SpawnUnit(new MapPoint(point.x, point.z), team == 0 ? Team.RED : Team.BLUE, SettlementType.TENT, isLeader: spawned == leader);
                    spawned++;
                }
            }
        }

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
                        if (redSpawns.Count < 2 * m_StarterUnits && !blueSpawns.Contains(tile) &&
                            !Terrain.Instance.IsTileOccupied(tile) && !Terrain.Instance.IsTileUnderwater(tile))
                            redSpawns.Add(tile);

                        (int x, int z) oppositeTile = (Terrain.Instance.TilesPerSide - tile.x - 1, Terrain.Instance.TilesPerSide - tile.z - 1);

                        if (blueSpawns.Count < 2 * m_StarterUnits && !redSpawns.Contains(oppositeTile) &&
                            !Terrain.Instance.IsTileOccupied(oppositeTile) && !Terrain.Instance.IsTileUnderwater(oppositeTile))
                            blueSpawns.Add(oppositeTile);

                        if (redSpawns.Count >= 2 * m_StarterUnits && blueSpawns.Count >= 2 * m_StarterUnits)
                            return;
                    }
                }
            }
        }

        #endregion


        #region Leader

        public Unit GetLeader(Team team) => m_Leaders[(int)team];

        public void SetLeader(Unit leader, Team team)
        {
            RemoveLeader(team);

            m_Leaders[(int)team] = leader;
            leader.IsLeader = true;
        }

        public void RemoveLeader(Team team)
        {
            Unit leader = m_Leaders[(int)team];

            if (!leader) return;

            leader.IsLeader = false;
            m_Leaders[(int)team] = null;
        }

        #endregion


        #region Knight

        public Unit GetKnight(int index, Team team) => index >= m_Knights[(int)team].Count ? null : m_Knights[(int)team][index];
        public Unit GetNewestKnight(Team team) => m_Knights[(int)team].Count == 0 ? null : m_Knights[(int)team][^1];
        public int GetKnightsNumber(Team team) => m_Knights[(int)team].Count;

        public void CreateKnight(Team team)
        {
            Unit unit = m_Leaders[(int)team];
            if (unit == null) return;

            RemoveLeader(team);
            m_Knights[(int)team].Add(unit);
            unit.IsKnight = true;
        }

        public void DestroyKnight(Team team, Unit knight)
        {
            m_Knights[(int)team].Remove(knight);
            knight.IsKnight = false;
        }

        #endregion


        #region Battles

        public Vector3 GetBattlePosition(int index) => m_BattlePositions[index];
        public int GetBattlesNumber() => m_BattlePositions.Count;

        public void StartBattle(Unit a, Unit b)
        {
            m_BattlePositions.Add(new(a.transform.position.x, a.transform.position.z));
        }

        public void EndBattle(Unit a, Unit b)
        {
            m_BattlePositions.Remove(new(a.transform.position.x, a.transform.position.z));
        }

        #endregion


        //[ServerRpc(RequireOwnership = false)]
        public void UnitStateChange/*ServerRpc*/(UnitState state, Team team)
        {
            if (team == Team.RED)
                OnRedStateChange?.Invoke(state);
            else if (team == Team.BLUE)
                OnBlueStateChange?.Invoke(state);
        }
    }
}