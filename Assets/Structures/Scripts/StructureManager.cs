using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The <c>UnitManager</c> class is a <c>MonoBehavior</c> which manages all the structures in the game.
    /// </summary>
    public class StructureManager : NetworkBehaviour
    {
        [SerializeField] private GameObject[] m_FlagPrefabs;
        [SerializeField] private GameObject m_SwampPrefab;
        [SerializeField] private GameObject m_FieldPrefab;

        [Header("Settlements")]
        [SerializeField] private GameObject m_SettlementPrefab;

        [Header("Trees and Rocks Properties")]
        [SerializeField, Range(0, 1)] private float m_TreeProbability;
        [SerializeField, Range(0, 1)] private float[] m_RockProbability;
        [SerializeField, Range(0, 1)] private float[] m_VolcanoRockProbability;
        [SerializeField] private GameObject m_TreePrefab;
        [SerializeField] private GameObject[] m_RockPrefab;

        private static StructureManager m_Instance;
        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        public static StructureManager Instance { get => m_Instance; }

        /// <summary>
        /// A list of the team symbols for each team.
        /// </summary>
        private TeamSymbol[] m_TeamSymbols;
        /// <summary>
        /// An array of lists of the tiles occupied by settlements for each team.
        /// </summary>
        private readonly List<(int x, int z)>[] m_SettlementLocations = new List<(int x, int z)>[] { new(), new() };

        /// <summary>
        /// Action to be called when a settlement is despawned to remove references to it from other objects.
        /// </summary>
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


        #region Spawn / Despawn

        /// <summary>
        /// Creates a structure of the given team on the given tile and spawns it on the network.
        /// </summary>
        /// <param name="prefab">The <c>GameObject</c> for the structure that should be created.</param>
        /// <param name="tile">The tile the created structure should occupy.</param>
        /// <param name="occupiedPoints">A list of the points that the created structure should occupy.</param>
        /// <param name="team">The team the created structure should belong to.</param>
        /// <returns>The <c>GameObject</c> of the created structure.</returns>
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

        /// <summary>
        /// Sets up some <c>GameObject</c> properties for the given settlement on each client.
        /// </summary>
        /// <param name="settlementNetworkId">The <c>NetworkObjectId</c> of the settlement.</param>
        /// <param name="name">The name for the <c>GameObject</c> of the settlement.</param>
        /// <param name="layer">An <c>int</c> representing the layer the settlement should be on.</param>
        [ClientRpc]
        private void SetupSettlementClientRpc(ulong settlementNetworkId, string name, int layer)
        {
            GameObject settlementObject = GetNetworkObject(settlementNetworkId).gameObject;
            settlementObject.name = name;
            settlementObject.layer = layer;
        }

        /// <summary>
        /// Despawns the given structure from the network and destroys is.
        /// </summary>
        /// <param name="structureObject">The <c>GameObject</c> of the structrue to be destroyed.</param>
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

        #endregion


        #region Trees and Rocks

        /// <summary>
        /// Populates the terrain with trees and rocks.
        /// </summary>
        public void PlaceTreesAndRocks() => PlaceTreesAndRocks(m_TreeProbability, m_RockProbability);

        public void PlaceVolcanoRocks() => PlaceTreesAndRocks(0, m_VolcanoRockProbability);

        /// <summary>
        /// Populates the terrain with trees and rocks.
        /// </summary>
        /// <param name="treeProbability">The probability of placing a tree.</param>
        /// <param name="whiteRockProbability">The probability of placing a white rock.</param>
        /// <param name="blackRockProbability">The probability of placing a black rock.</param>
        private void PlaceTreesAndRocks(float treeProbability, float[] rockProbabilities)
        {
            //if (!IsHost) return;

            int[] rockIndices = Enumerable.Range(0, rockProbabilities.Length).ToArray();
            Array.Sort(rockProbabilities, rockIndices);

            Random random = new(GameData.Instance == null ? 0 : GameData.Instance.MapSeed);

            for (int z = 0; z < Terrain.Instance.TilesPerSide; ++z)
            {
                for (int x = 0; x < Terrain.Instance.TilesPerSide; ++x)
                {
                    if (Terrain.Instance.IsTileOccupied((x, z)) || Terrain.Instance.IsTileUnderwater((x, z)))
                        continue;

                    List<MapPoint> occupiedPoints = Terrain.Instance.GetTileCorners((x, z));

                    double randomValue = random.NextDouble();

                    bool spawned = false;
                    for (int i = 0; i < rockIndices.Length; ++i)
                    {
                        if (randomValue < rockProbabilities[i] && (treeProbability > rockProbabilities[i] || randomValue >= treeProbability))
                        {
                            SpawnStructure(m_RockPrefab[rockIndices[i]], (x, z), occupiedPoints);
                            spawned = true;
                            break;
                        }
                    }

                    if (spawned) return;

                    if (randomValue < treeProbability)
                        SpawnStructure(m_TreePrefab, (x, z), occupiedPoints);
                }
            }
        }

        #endregion


        #region Team Symbols

        /// <summary>
        /// Creates the team symbols for all the teams.
        /// </summary>
        public void SpawnTeamSymbols()
        {
            //if (!IsServer) return;

            m_TeamSymbols = new TeamSymbol[m_FlagPrefabs.Length];
            for (int i = 0; i < m_FlagPrefabs.Length; ++i)
            {
                GameObject flagObject = Instantiate(m_FlagPrefabs[i], Vector3.zero, Quaternion.identity);
                //flagObject.GetComponent<NetworkObject>().Spawn(true);
                TeamSymbol flag = flagObject.GetComponent<TeamSymbol>();
                m_TeamSymbols[i] = flag;

                flag.Team = i == 0 ? Team.RED : Team.BLUE;
                flagObject.transform.Rotate(new Vector3(1, -90, 1));
                MapPoint location = UnitManager.Instance.GetLeader(flag.Team).ClosestMapPoint;
                flagObject.transform.position = location.ToWorldPosition();

                flag.OccupiedTile = (location.GridX, location.GridZ);
                flag.OccupiedPointHeights = new() { { location, location.Y } };

                GameController.Instance.OnTerrainMoved += flag.ReactToTerrainChange;
            }
        }

        /// <summary>
        /// Gets the position of the team symbol of the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> the symbol whose position should be returned belongs to.</param>
        /// <returns>A <c>Vector3</c> of the position of the symbol.</returns>
        public Vector3 GetSymbolPosition(Team team) => m_TeamSymbols[(int)team].transform.position;

        /// <summary>
        /// Sets the position of the symbol of the given team to the given team.
        /// </summary>
        /// <param name="team">The <c>Team</c> the symbol whose position should be changed belongs to.</param>
        /// <param name="position">The position that the symbol should be set to.</param>
        public void SetSymbolPosition(Team team, Vector3 position) => m_TeamSymbols[(int)team].SetSymbolPositionClient/*Rpc*/(position);

        #endregion


        #region Settlements

        /// <summary>
        /// Creates a settlement of the given team on the given tile.
        /// </summary>
        /// <param name="tile">The tile that the settlement should be created on.</param>
        /// <param name="team">The team the settlement should belong to.</param>
        public void CreateSettlement(MapPoint tile, Team team) => SpawnStructure(m_SettlementPrefab, (tile.GridX, tile.GridZ), tile.TileCorners, team);

        /// <summary>
        /// Switches the team the given settlement belongs to.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> whose team should be switched.</param>
        /// <param name="team">The new <c>Team</c> the settlement should belong to.</param>
        public void SwitchTeam(Settlement settlement, Team team)
        {
            RemoveSettlementPosition(settlement.OccupiedTile, settlement.Team);

            settlement.Team = team;
            //SetupSettlementClientRpc(settlement.GetComponent<NetworkObject>().NetworkObjectId, $"{team} Settlement", LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]));
            settlement.gameObject.name = $"{team} Settlement";
            settlement.gameObject.layer = LayerMask.NameToLayer(GameController.Instance.TeamLayers[(int)team]);

            AddSettlementPosition(settlement.OccupiedTile, settlement.Team);
        }

        /// <summary>
        /// Burns the given settlement down.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be burned down.</param>
        public void BurnSettlement(Settlement settlement)
        {
            settlement.BurnSettlement();
            SwitchTeam(settlement, Team.NONE);
        }

        /// <summary>
        /// Gets the tile on which the settlement of the given team sits.
        /// </summary>
        /// <param name="index">The index in the settlement tile list of the settlement whose occupied tile should be returned.</param>
        /// <param name="team">The <c>Team</c> the settlement that should be returned belongs to.</param>
        /// <returns>The (x, z) coordinates of the tile on which the settlement sits.</returns>
        public (int x, int z) GetSettlementTile(int index, Team team) => m_SettlementLocations[(int)team][index];

        /// <summary>
        /// Gets the number of settlements of the given team currently on the terrain.
        /// </summary>
        /// <param name="team">The <c>Team</c> whose number of settlements should be returned.</param>
        /// <returns>The number of active settlements of the given team.</returns>
        public int GetSettlementsNumber(Team team) => m_SettlementLocations[(int)team].Count;

        /// <summary>
        /// Adds a settlememt location of the given team to the settlement locations list.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile representing the settlement location.</param>
        /// <param name="team">The <c>Team</c> whose settlement should be added.</param>
        public void AddSettlementPosition((int x, int z) tile, Team team) => m_SettlementLocations[(int)team].Add(tile);

        /// <summary>
        /// Removes a settlement location of the given team from the settlement locations list.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile representing the settlement location.</param>
        /// <param name="team">The <c>Team</c> whose settlement should be removed.</param>
        public void RemoveSettlementPosition((int x, int z) tile, Team team) => m_SettlementLocations[(int)team].Remove(tile);

        #endregion


        #region Fields

        /// <summary>
        /// Creates a field belonging to the given team on the given tile.
        /// </summary>
        /// <param name="tile">The tile the field should be created on.</param>
        /// <param name="team">The <c>Team</c> the field should belong to.</param>
        /// <returns>The <c>Field</c> that was created.</returns>
        public Field SpawnField((int x, int z) tile, Team team)
        {
            //if (!IsServer) return null;

            Field field = SpawnStructure(m_FieldPrefab, tile, Terrain.Instance.GetTileCorners(tile)).GetComponent<Field>();
            field.Team = team;
            return field;
        }

        #endregion


        #region Swamp

        /// <summary>
        /// Creates a swamp on the given tile.
        /// </summary>
        /// <param name="tile">The tile the swamp should be created on.</param>
        public void SpawnSwamp((int x, int z) tile) => SpawnStructure(m_SwampPrefab, tile, Terrain.Instance.GetTileCorners(tile));

        #endregion
    }
}