using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// Powers available to each player.
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
    /// The <c>GameController</c> class 
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class GameController : NetworkBehaviour
    {
        [Header("Manna")]
        [SerializeField] private int m_MaxManna = 100;
        [SerializeField] private int[] m_PowerActivationThreshold = new int[Enum.GetNames(typeof(Power)).Length];
        [SerializeField] private int[] m_PowerMannaCost = new int[Enum.GetNames(typeof(Power)).Length];

        [Header("Unit")]
        [SerializeField] private GameObject m_UnitPrefab;
        [SerializeField] private int m_StarterUnits = 15;

        [Header("Powers")]
        [SerializeField] private int m_EarthquakeRadius = 3;
        [SerializeField] private int m_SwampRadius = 3;
        [SerializeField] private GameObject m_SwampPrefab;
        [SerializeField] private int m_VolcanoRadius = 3;
        [SerializeField, Range(0, 1)] private float m_VolcanoRockDensity = 0.4f;
        [SerializeField] private GameObject[] m_FlagPrefabs;

        [Header("Settlements")]
        [SerializeField] private GameObject m_RuinedSettlementPrefab;
        [SerializeField] private GameObject m_SettlementPrefab;
        [SerializeField] private GameObject m_FieldPrefab;

        [Header("Trees and Rocks Properties")]
        [SerializeField, Range(0, 1)] private float m_TreeDensity;
        [SerializeField, Range(0, 1)] private float m_WhiteRockDensity;
        [SerializeField, Range(0, 1)] private float m_BlackRockDensity;
        [SerializeField] private GameObject m_TreePrefab;
        [SerializeField] private GameObject m_WhiteRockPrefab;
        [SerializeField] private GameObject m_BlackRockPrefab;

        private static GameController m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static GameController Instance { get => m_Instance; }

        public bool IsPlayerHosting { get => IsHost; }

        public int MaxManna { get => m_MaxManna; }
        public int[] PowerActivationThreshold { get => m_PowerActivationThreshold; }
        public int[] PowerMannaCost { get => m_PowerMannaCost; }
        public int EarthquakeRadius { get => m_EarthquakeRadius; }
        public int SwampRadius { get => m_SwampRadius; }
        public int VolcanoRadius { get => m_VolcanoRadius; }

        private GameObject[] m_Flags;

        public Action OnFlood;
        public Action OnTerrainMoved;
        public Action<UnitState> OnRedStateChange;
        public Action<UnitState> OnBlueStateChange;


        public Team Winner;  // TODO: Remove - for testing only



        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            Vector3 startingScale = m_SwampPrefab.transform.localScale;

            GameUtils.ResizeGameObject(m_SwampPrefab, Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_RuinedSettlementPrefab, Terrain.Instance.UnitsPerTileSide - 5f, scaleY: true);

            foreach (GameObject flag in m_FlagPrefabs)
                GameUtils.ResizeGameObject(flag, 10, scaleY: true);

            Terrain.Instance.CreateTerrain();
            PlaceTreesAndRocks(m_TreeDensity, m_WhiteRockDensity, m_BlackRockDensity);
            SpawnStarterUnits();
            SpawnFlags();
        }

        #endregion



        #region Units

        public GameObject SpawnUnit(MapPoint location, Team team, SettlementType origin, bool isLeader)
        {
            if (!IsServer) return null;

            GameObject unitObject = Instantiate(
                m_UnitPrefab,
                new Vector3(
                    (location.X + 0.5f) * Terrain.Instance.UnitsPerTileSide,
                    m_UnitPrefab.transform.position.y + Terrain.Instance.GetTileCenterHeight((location.X, location.Z)),
                    (location.Z + 0.5f) * Terrain.Instance.UnitsPerTileSide),
                Quaternion.identity
            );

            Unit unit = unitObject.GetComponent<Unit>();
            OnTerrainMoved += unit.RecalculateHeight;

            if (team == Team.RED)
                OnRedStateChange += unit.SwitchState;
            else if (team == Team.BLUE)
                OnBlueStateChange += unit.SwitchState;

            NetworkObject networkUnit = unitObject.GetComponent<NetworkObject>();
            networkUnit.Spawn(true);
            ChangeUnitColorClientRpc(networkUnit.NetworkObjectId, team);

            return unitObject;
        }

        [ClientRpc]
        private void ChangeUnitColorClientRpc(ulong unitNetworkId, Team team)
            => GetNetworkObject(unitNetworkId).GetComponent<MeshRenderer>().material.color = team == Team.RED ? Color.red : Color.blue;

        public void DespawnUnit(GameObject unitObject)
        {
            if (!IsServer) return;

            Unit unit = unitObject.GetComponent<Unit>();
            OnTerrainMoved -= unit.RecalculateHeight;

            if (unit.Team == Team.RED)
                OnRedStateChange -= unit.SwitchState;
            else if (unit.Team == Team.BLUE)
                OnBlueStateChange -= unit.SwitchState;

            unitObject.GetComponent<NetworkObject>().Despawn();
            Destroy(unitObject);
        }


        private void SpawnStarterUnits()
        {
            if (!IsServer) return;

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



        #region Structures

        /// <summary>
        /// Creates a game object in the world for a structure and sets up its occupied points.
        /// </summary>
        /// <param name="prefab">The prefab of the structure that should be spawned.</param>
        /// <param name="occupiedPoints">A <c>List</c> of the <c>MapPoint</c>s that the structure occupies.</param>
        public GameObject SpawnStructure(GameObject prefab, (int x, int z) tile, List<MapPoint> occupiedPoints, Team team = Team.NONE)
        {
            if (!IsServer) return null;

            GameObject structureObject = Instantiate(
                prefab,
                new Vector3(
                    (tile.x + 0.5f) * Terrain.Instance.UnitsPerTileSide,
                    prefab.transform.position.y + Terrain.Instance.GetTileCenterHeight(tile),
                    (tile.z + 0.5f) * Terrain.Instance.UnitsPerTileSide),
                Quaternion.identity
            );

            structureObject.GetComponent<NetworkObject>().Spawn(true);

            Structure structure = structureObject.GetComponent<Structure>();
            Terrain.Instance.SetOccupiedTile(tile, structure);
            structure.Team = team;
            structure.OccupiedPointHeights = occupiedPoints.ToDictionary(x => x, x => x.Y);
            structure.OccupiedTile = tile;
            OnFlood += structure.ReactToTerrainChange;

            return structureObject;
        }

        /// <summary>
        /// Destroys a structure and cleans up references to it in the terrain.
        /// </summary>
        /// <param name="structureObject">The structure object to be destroyed.</param>
        public void DespawnStructure(GameObject structureObject)
        {
            if (!IsServer) return;

            Structure structure = structureObject.GetComponent<Structure>();

            if (!Terrain.Instance.IsTileOccupied(structure.OccupiedTile))
                return;

            if (OnFlood != null)
                OnFlood -= structure.ReactToTerrainChange;

            Terrain.Instance.SetOccupiedTile(structure.OccupiedTile, null);

            structure.Cleanup();
            structure.GetComponent<NetworkObject>().Despawn();
            Destroy(structureObject);
        }

        public Field SpawnField((int x, int z) tile, Team team)
        {
            if (!IsServer) return null;

            Field field = SpawnStructure(m_FieldPrefab, tile, Terrain.Instance.GetTilePoints(tile)).GetComponent<Field>();
            field.Team = team;
            return field;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="treeDensity"></param>
        /// <param name="whiteRockDensity"></param>
        /// <param name="blackRockDensity"></param>
        private void PlaceTreesAndRocks(float treeDensity, float whiteRockDensity, float blackRockDensity)
        {
            if (!IsHost) return;

            Random random = new(GameData.Instance == null ? 0 : GameData.Instance.MapSeed);

            for (int z = 0; z < Terrain.Instance.TilesPerSide; ++z)
            {
                for (int x = 0; x < Terrain.Instance.TilesPerSide; ++x)
                {
                    if (Terrain.Instance.IsTileOccupied((x, z)) || Terrain.Instance.IsTileUnderwater((x, z)))
                        continue;

                    List<MapPoint> occupiedPoints = Terrain.Instance.GetTilePoints((x, z));

                    double randomValue = random.NextDouble();

                    if (randomValue < whiteRockDensity)
                        SpawnStructure(m_WhiteRockPrefab, (x, z), occupiedPoints);
                    else if (randomValue < blackRockDensity)
                        SpawnStructure(m_BlackRockPrefab, (x, z), occupiedPoints);
                    else if (randomValue < treeDensity)
                        SpawnStructure(m_TreePrefab, (x, z), occupiedPoints);
                }
            }
        }

        private void SpawnFlags()
        {
            if (!IsServer) return;

            m_Flags = new GameObject[m_FlagPrefabs.Length];
            for (int i = 0; i < m_FlagPrefabs.Length; ++i)
            {
                GameObject flagObject = Instantiate(m_FlagPrefabs[i], Vector3.zero, Quaternion.identity);
                flagObject.GetComponent<NetworkObject>().Spawn(true);
                m_Flags[i] = flagObject;

                SetFlagClientRpc(m_Flags[i].GetComponent<NetworkObject>().NetworkObjectId, false, Vector3.zero);
            }
        }

        #endregion



        #region Powers

        #region MoldTerrain

        /// <summary>
        /// Executes the Mold Terrain power from the server.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> which should be modified.</param>
        /// <param name="lower">Whether the point should be lowered or elevated.</param>
        public void MoldTerrain(MapPoint point, bool lower)
        {
            Terrain.Instance.ModifyTerrain(point, lower);

            //MoldTerrainServerRpc(point, lower);
        }

        [ServerRpc(RequireOwnership = false)]
        private void MoldTerrainServerRpc(MapPoint point, bool lower)
            => MoldTerrainClientRpc(point, lower);

        [ClientRpc]
        private void MoldTerrainClientRpc(MapPoint point, bool lower)
            => Terrain.Instance.ModifyTerrain(point, lower);

        #endregion


        #region Guide Followers

        [ServerRpc(RequireOwnership = false)]
        public void GuideFollowersServerRpc(MapPoint point, Team team)
        {
            SetFlagClientRpc(m_Flags[(int)team].GetComponent<NetworkObject>().NetworkObjectId, true, new Vector3(
                point.X * Terrain.Instance.UnitsPerTileSide,
                point.Y,
                point.Z * Terrain.Instance.UnitsPerTileSide
            ));

            if (team == Team.RED)
                OnRedStateChange?.Invoke(UnitState.GO_TO_FLAG);
            else if (team == Team.BLUE)
                OnBlueStateChange?.Invoke(UnitState.GO_TO_FLAG);
        }

        [ClientRpc]
        private void SetFlagClientRpc(ulong networkId, bool isActive, Vector3 position)
        {
            NetworkObject net = GetNetworkObject(networkId);
            if (net != null)
            {
                GameObject flag = net.gameObject;
                flag.transform.position = position;
                flag.transform.Rotate(new Vector3(1, -90, 1));
                flag.SetActive(isActive);
            }
        }

        #endregion


        #region Earthquake

        /// <summary>
        /// Executes the Earthquake power on server.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> at the center of the earthquake.</param>
        [ServerRpc(RequireOwnership = false)]
        public void EarthquakeServerRpc(MapPoint point)
        {
            EarthquakeClientRpc(point, new Random().Next());
        }

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
        public void SwampServerRpc(MapPoint tile)
        {
            List<(int, int)> flatTiles = new();
            for (int z = -m_SwampRadius; z < m_SwampRadius; ++z)
            {
                for (int x = -m_SwampRadius; x < m_SwampRadius; ++x)
                {
                    (int x, int z) neighborTile = (tile.X + x, tile.Z + z);
                    if (tile.X + x < 0 || tile.X + x >= Terrain.Instance.TilesPerSide ||
                        tile.Z + z < 0 || tile.Z + z >= Terrain.Instance.TilesPerSide ||
                        !Terrain.Instance.IsTileFlat(neighborTile))
                        continue;

                    Structure structure = Terrain.Instance.GetStructureOccupyingTile(neighborTile);

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Rock) || structureType == typeof(Tree) ||
                            structureType == typeof(Swamp) || structureType == typeof(Settlement))
                            continue;

                        if (structureType == typeof(Field))
                        {
                            ((Field)structure).OnFieldDestroyed?.Invoke();
                            DespawnStructure(structure.gameObject);
                        }
                    }

                    flatTiles.Add(neighborTile);
                }
            }

            Random random = new();
            List<int> tiles = Enumerable.Range(0, flatTiles.Count).ToList();
            int swampTiles = random.Next(Mathf.RoundToInt(flatTiles.Count * 0.5f), flatTiles.Count);

            int count = tiles.Count;
            foreach ((int x, int z) flatTile in flatTiles)
            {
                count--;
                int randomIndex = random.Next(count + 1);
                (tiles[count], tiles[randomIndex]) = (tiles[randomIndex], tiles[count]);

                if (tiles[count] <= swampTiles)
                    SpawnStructure(m_SwampPrefab, flatTile, Terrain.Instance.GetTilePoints(flatTile));
            }
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
            PlaceTreesAndRocks(0, m_VolcanoRockDensity, 0);
        }

        [ClientRpc]
        private void VolcanoClientRpc(MapPoint point)
            => Terrain.Instance.CauseVolcano(point, m_VolcanoRadius);

        #endregion


        #region Flood

        /// <summary>
        /// Executes the Flood power on server.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void FloodServerRpc()
        {
            if (Terrain.Instance.WaterLevel == Terrain.Instance.MaxHeight)
                return;

            FloodClientRpc();
        }

        [ClientRpc]
        private void FloodClientRpc()
        {
            Terrain.Instance.RaiseWaterLevel();
            Water.Instance.Raise();

            if (IsHost)
                OnFlood?.Invoke();
        }

        #endregion

        #endregion
    }
}