using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    public class StructureManager : NetworkBehaviour
    {
        private static StructureManager m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static StructureManager Instance { get => m_Instance; }

        [SerializeField] private GameObject[] m_FlagPrefabs;
        [SerializeField] private GameObject m_SwampPrefab;
        [SerializeField] private GameObject m_FieldPrefab;

        [Header("Settlements")]
        [SerializeField] private GameObject m_SettlementPrefab;

        [Header("Trees and Rocks Properties")]
        [SerializeField, Range(0, 1)] private float m_TreeDensity;
        [SerializeField, Range(0, 1)] private float m_WhiteRockDensity;
        [SerializeField, Range(0, 1)] private float m_BlackRockDensity;
        [SerializeField] private GameObject m_TreePrefab;
        [SerializeField] private GameObject m_WhiteRockPrefab;
        [SerializeField] private GameObject m_BlackRockPrefab;

        private GameObject[] m_Flags;
        private List<(int x, int z)>[] m_SettlementTiles = new List<(int x, int z)>[] { new(), new() };

        public Action<Settlement> OnRemoveReferencesToSettlement;



        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            GameUtils.ResizeGameObject(m_SwampPrefab, Terrain.Instance.UnitsPerTileSide);

            foreach (GameObject flag in m_FlagPrefabs)
                GameUtils.ResizeGameObject(flag, 10, scaleY: true);
        }


        /// <summary>
        /// Creates a game object in the world for a structure and sets up its occupied points.
        /// </summary>
        /// <param name="prefab">The prefab of the structure that should be spawned.</param>
        /// <param name="occupiedPoints">A <c>List</c> of the <c>MapPoint</c>s that the structure occupies.</param>
        public GameObject SpawnStructure(GameObject prefab, (int x, int z) tile, List<MapPoint> occupiedPoints, Team team = Team.NONE)
        {
            //if (!IsServer) return null;

            GameObject structureObject = Instantiate(
                prefab,
                new Vector3(
                    (tile.x + 0.5f) * Terrain.Instance.UnitsPerTileSide,
                    prefab.transform.position.y + Terrain.Instance.GetTileCenterHeight(tile),
                    (tile.z + 0.5f) * Terrain.Instance.UnitsPerTileSide),
                Quaternion.identity
            );

            //structureObject.GetComponent<NetworkObject>().Spawn(true);

            Structure structure = structureObject.GetComponent<Structure>();
            Terrain.Instance.SetOccupiedTile(tile, structure);
            structure.Team = team;
            structure.OccupiedPointHeights = occupiedPoints.ToDictionary(x => x, x => x.Y);
            structure.OccupiedTile = tile;
            GameController.Instance.OnFlood += structure.ReactToTerrainChange;

            if (structure.GetType() == typeof(Settlement))
            {
                AddSettlementPosition(structure.OccupiedTile, team);
                //SetupSettlementClientRpc(structureObject.GetComponent<NetworkObject>().NetworkObjectId, $"{team} Settlement", LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]));
                structureObject.name = $"{team} Settlement";
                structureObject.layer = LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]);
            }

            return structureObject;
        }


        [ClientRpc]
        private void SetupSettlementClientRpc(ulong unitNetworkId, string name, int layer)
        {
            GameObject settlementObject = GetNetworkObject(unitNetworkId).gameObject;
            settlementObject.name = name;
            settlementObject.layer = layer;
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

            if (GameController.Instance.OnFlood != null)
                GameController.Instance.OnFlood -= structure.ReactToTerrainChange;

            Terrain.Instance.SetOccupiedTile(structure.OccupiedTile, null);

            if (structure.GetType() == typeof(Settlement))
            {
                RemoveSettlementPosition(structure.OccupiedTile, structure.Team);
                OnRemoveReferencesToSettlement?.Invoke((Settlement)structure);
            }

            structure.Cleanup();
            structure.GetComponent<NetworkObject>().Despawn();
            Destroy(structureObject);
        }


        #region Settlements

        public void CreateSettlement(MapPoint tile, Team team)
            => SpawnStructure(m_SettlementPrefab, (tile.GridX, tile.GridZ), tile.TileCorners, team);

        public void EnterSettlement(MapPoint tile, Unit unit)
        {
            Settlement settlement = (Settlement)Terrain.Instance.GetStructureOnTile((tile.GridX, tile.GridZ));
            settlement.AddUnit(unit.Class == UnitClass.LEADER);
            UnitManager.Instance.DespawnUnit(unit.gameObject);
        }

        public void SwitchTeam(Settlement settlement, Team team)
        {
            settlement.Team = team;
            //SetupSettlementClientRpc(settlement.GetComponent<NetworkObject>().NetworkObjectId, $"{team} Settlement", LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]));

            settlement.gameObject.name = $"{team} Settlement";
            settlement.gameObject.layer = LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]);
        }

        public void RuinSettlement(Settlement settlement)
        {
            settlement.RuinSettlement();
            SwitchTeam(settlement, Team.NONE);
        }

        public (int x, int z) GetSettlementTile(int index, Team team) => m_SettlementTiles[(int)team][index];

        public int GetSettlementsNumber(Team team) => m_SettlementTiles[(int)team].Count;

        public void AddSettlementPosition((int x, int z) tile, Team team) => m_SettlementTiles[(int)team].Add(tile);

        public void RemoveSettlementPosition((int x, int z) tile, Team team) => m_SettlementTiles[(int)team].Remove(tile);

        #endregion


        #region Swamp

        public void SpawnSwamp((int x, int z) tile, List<MapPoint> occupiedPoints)
            => SpawnStructure(m_SwampPrefab, tile, occupiedPoints);

        #endregion


        #region Fields

        public Field SpawnField((int x, int z) tile, Team team)
        {
            //if (!IsServer) return null;

            Field field = SpawnStructure(m_FieldPrefab, tile, Terrain.Instance.GetTileCorners(tile)).GetComponent<Field>();
            field.Team = team;
            return field;
        }

        #endregion


        #region Trees and Rocks

        public void PlaceTreesAndRocks() => PlaceTreesAndRocks(m_TreeDensity, m_WhiteRockDensity, m_BlackRockDensity);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="treeDensity"></param>
        /// <param name="whiteRockDensity"></param>
        /// <param name="blackRockDensity"></param>
        public void PlaceTreesAndRocks(float treeDensity, float whiteRockDensity, float blackRockDensity)
        {
            //if (!IsHost) return;

            Random random = new(GameData.Instance == null ? 0 : GameData.Instance.MapSeed);

            for (int z = 0; z < Terrain.Instance.TilesPerSide; ++z)
            {
                for (int x = 0; x < Terrain.Instance.TilesPerSide; ++x)
                {
                    if (Terrain.Instance.IsTileOccupied((x, z)) || Terrain.Instance.IsTileUnderwater((x, z)))
                        continue;

                    List<MapPoint> occupiedPoints = Terrain.Instance.GetTileCorners((x, z));

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

        #endregion


        #region Flags

        public void SpawnFlags()
        {
            //if (!IsServer) return;

            m_Flags = new GameObject[m_FlagPrefabs.Length];
            for (int i = 0; i < m_FlagPrefabs.Length; ++i)
            {
                GameObject flagObject = Instantiate(m_FlagPrefabs[i], Vector3.zero, Quaternion.identity);
                //flagObject.GetComponent<NetworkObject>().Spawn(true);
                m_Flags[i] = flagObject;

                Flag flag = flagObject.GetComponent<Flag>();

                flag.Team = i == 0 ? Team.RED : Team.BLUE;
                flagObject.transform.Rotate(new Vector3(1, -90, 1));
                MapPoint location = UnitManager.Instance.GetLeader(flag.Team).ClosestMapPoint;
                flagObject.transform.position = location.ToWorldPosition();

                flag.OccupiedTile = (location.GridX, location.GridZ);
                flag.OccupiedPointHeights = new() { { location, location.Y } };

                GameController.Instance.OnTerrainMoved += flag.ReactToTerrainChange;
            }
        }

        public Vector3 GetSymbolPosition(Team team) => m_Flags[(int)team].transform.position;

        //[ClientRpc]
        public void SetFlagPosition/*ClientRpc*/(Team team, Vector3 position) => m_Flags[(int)team].transform.position = position;

        #endregion
    }
}