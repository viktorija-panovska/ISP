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

        [Header("Powers")]
        [SerializeField] private int m_EarthquakeRadius = 3;
        [SerializeField] private int m_SwampRadius = 3;
        [SerializeField] private GameObject m_SwampPrefab;
        [SerializeField] private int m_VolcanoRadius = 3;
        [SerializeField, Range(0, 1)] private float m_VolcanoRockDensity = 0.4f;

        [Header("Settlements")]
        [SerializeField] private GameObject m_RuinedSettlementPrefab;
        [SerializeField] private GameObject m_SettlementPrefab;
        [SerializeField] private GameObject m_FieldPrefab;

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

        /// <summary>
        /// Action to be called when an event happens that might move or destroy structures i.e. modifying the terrain or raising the water level.
        /// </summary>
        public Action OnFlood;


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
            GameUtils.ResizeGameObject(m_SwampPrefab, Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_RuinedSettlementPrefab, Terrain.Instance.UnitsPerTileSide - 5f, scaleY: true);
        }

        #endregion



        #region Structures

        /// <summary>
        /// Creates a game object in the world for a structure and sets up its occupied points.
        /// </summary>
        /// <param name="prefab">The prefab of the structure that should be spawned.</param>
        /// <param name="occupiedPoints">A <c>List</c> of the <c>MapPoint</c>s that the structure occupies.</param>
        public GameObject SpawnStructure(GameObject prefab, (int x, int z) tile, List<MapPoint> occupiedPoints)
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

            structureObject.GetComponent<NetworkObject>().Spawn();

            Structure structure = structureObject.GetComponent<Structure>();
            Terrain.Instance.SetOccupiedTile(tile, structure);
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


        public void CreateHouse(MapPoint point)
        {
            if (point.X == Terrain.Instance.TilesPerSide || point.Z == Terrain.Instance.TilesPerSide ||
                !Terrain.Instance.IsTileFlat((point.X, point.Z)) || Terrain.Instance.IsTileOccupied((point.X, point.Z)))
                return;

            SpawnStructure(m_SettlementPrefab, (point.X, point.Z), Terrain.Instance.GetTilePoints((point.X, point.Z)));
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
            //Terrain.Instance.ModifyTerrain(point, lower);

            MoldTerrainServerRpc(point, lower);
        }

        [ServerRpc(RequireOwnership = false)]
        private void MoldTerrainServerRpc(MapPoint point, bool lower)
            => MoldTerrainClientRpc(point, lower);

        [ClientRpc]
        private void MoldTerrainClientRpc(MapPoint point, bool lower)
            => Terrain.Instance.ModifyTerrain(point, lower);

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
            Terrain.Instance.PlaceTreesAndRocks(0, m_VolcanoRockDensity, 0);
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