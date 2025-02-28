using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The <c>StructureManager</c> class manages all the structures in the game.
    /// </summary>
    public class StructureManager : NetworkBehaviour
    {
        #region Inspector Fields

        [Header("Prefabs")]
        [SerializeField] private GameObject m_TreePrefab;
        [SerializeField] private GameObject m_BlackRockPrefab;
        [SerializeField] private GameObject m_WhiteRockPrefab;
        [SerializeField] private GameObject m_SettlementPrefab;
        [SerializeField] private GameObject m_RuinPrefab;
        [SerializeField] private GameObject m_FieldPrefab;
        [SerializeField] private GameObject m_SwampPrefab;


        [Header("Trees and Rocks Properties")]

        [Tooltip("The percentage of the terrain that should be covered by trees (includes water).")]
        [SerializeField, Range(0, 1)] private float m_TreePercentage;

        [Tooltip("The percentage of the terrain that should be covered by black rocks (includes water).")]
        [SerializeField, Range(0, 1)] private float m_BlackRockPercentage;

        [Tooltip("The percentage of the terrain that should be covered by white rocks (includes water).")]
        [SerializeField, Range(0, 1)] private float m_WhiteRockPercentage;

        [Tooltip("The percentage of the volcano area that should be covered by white rocks.")]
        [SerializeField, Range(0, 1)] private float m_VolcanoRockPercentage;


        [Header("UI")]

        [Tooltip("The scale of the settlement icons on the minimap.")]
        [SerializeField] private int m_MinimapIconScale;

        [Tooltip("The colors of the settlement icons on the minimap, where 0 is the color of the red faction and 1 is the color of the blue faciton.")]
        [SerializeField] private Color[] m_MinimapSettlementColors;

        #endregion


        #region Class Fields

        private static StructureManager m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static StructureManager Instance { get => m_Instance; }

        /// <summary>
        /// Each cell represents a terrain tile and the value of the cell is the structure occupying that tile.
        /// </summary>
        private Structure[,] m_StructureOnTile;

        /// <summary>
        /// An array with a list for each team containing the locations of their settlements.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly List<Vector2>[] m_SettlementLocations = new List<Vector2>[] { new(), new() };

        /// <summary>
        /// An array of the leaders in each team that are part of a settlement, null if the team's leader is not in a settlement.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the team in the <c>Team</c> enum.</remarks>
        private readonly Settlement[] m_LeaderSettlements = new Settlement[2];

        /// <summary>
        /// The scale for the settlement icons on the minimap.
        /// </summary>
        public int MinimapIconScale { get => m_MinimapIconScale; }
        /// <summary>
        /// The colors of the settlement icons on the minimap.
        /// </summary>
        /// <remarks>The index of the list in the array corresponds to the value of the faction in the <c>Faction</c> enum.</remarks>
        public Color[] MinimapSettlementColors { get => m_MinimapSettlementColors; }

        #endregion


        #region Actions

        /// <summary>
        /// Action to be called when a settlement is despawned to remove references to it from other objects.
        /// </summary>
        public Action<Settlement> OnRemoveReferencesToSettlement;

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

        private void Start()
        {
            m_StructureOnTile = new Structure[Terrain.Instance.TilesPerSide, Terrain.Instance.TilesPerSide];
            //GameUtils.ResizeGameObject(m_FieldPrefab, Terrain.Instance.UnitsPerTileSide);
            GameUtils.ResizeGameObject(m_SwampPrefab, Terrain.Instance.UnitsPerTileSide);
        }

        #endregion


        #region Spawn / Despawn

        /// <summary>
        /// Creates a structure belonging to the given faction on the given tile and spawns it on the network.
        /// </summary>
        /// <param name="prefab">The <c>GameObject</c> for the structure that should be created.</param>
        /// <param name="tile">The <c>TerrainTile</c> the created structure should occupy.</param>
        /// <param name="faction">The <c>Faction</c> the created structure should belong to.</param>
        /// <returns>The <c>GameObject</c> of the created structure.</returns>
        private GameObject SpawnStructure(GameObject prefab, TerrainTile tile, Faction faction = Faction.NONE)
        {
            //if (!IsHost) return null;

            GameObject structureObject = Instantiate(
                prefab,
                prefab.transform.position + tile.GetCenterPosition(),
                Quaternion.identity
            );

            //structureObject.GetComponent<NetworkObject>().Spawn(true);

            // Structure properties
            Structure structure = structureObject.GetComponent<Structure>();
            structure.Setup(faction, tile);
            SetOccupiedTile(tile, structure);

            // Settlement properties
            if (structure.GetType() == typeof(Settlement) && structure.Faction != Faction.NONE)
                AddSettlementPosition(new Vector2(structure.transform.position.x, structure.transform.position.z), faction);

            return structureObject;
        }

        /// <summary>
        /// Despawns the given structure from the network and destroys is.
        /// </summary>
        /// <param name="structureObject">The <c>GameObject</c> of the structrue to be destroyed.</param>
        public void DespawnStructure(GameObject structureObject)
        {
            //if (!IsHost) return;

            Structure structure = structureObject.GetComponent<Structure>();

            if (!structure.OccupiedTile.IsOccupied())
                return;

            SetOccupiedTile(structure.OccupiedTile, null);

            structure.Cleanup();

            if (structure.GetType() == typeof(Settlement) && structure.Faction != Faction.NONE)
            {
                RemoveSettlementPosition(
                    new Vector2(structure.transform.position.x, structure.transform.position.z),
                    structure.Faction
                );
            }

            //structure.GetComponent<NetworkObject>().Despawn();
            Destroy(structureObject);
        }

        #endregion


        #region Structures

        /// <summary>
        /// Gets the <c>Structure</c> occupying the given tile.
        /// </summary>
        /// <param name="tile">The <c>TerrainTile</c> whose structure should be returned.</param>
        /// <returns>The <c>Structure</c> occupying the given tile, or <c>null</c> if the tile is unoccupied.</returns>
        public Structure GetStructureOnTile(TerrainTile tile) => m_StructureOnTile[tile.Z, tile.X];

        /// <summary>
        /// Sets the given structure to occupy the given tile.
        /// </summary>
        /// <param name="tile">The <c>TerrainTile</c> that should be set.</param>
        /// <param name="structure">The <c>Structure</c> that should be placed on the tile.</param>
        public void SetOccupiedTile(TerrainTile tile, Structure structure) => m_StructureOnTile[tile.Z, tile.X] = structure;

        /// <summary>
        /// Updates the structures in the given area, moving or destroying them if the terrain under them has been modified.
        /// </summary>
        /// <param name="bottomLeft">The bottom-left corner of a rectangular area containing all modified terrain points.</param>
        /// <param name="topRight">The top-right corner of a rectangular area containing all modified terrain points.</param>
        public void UpdateStructuresInArea(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            // +-2 to accound for settlements
            for (int z = bottomLeft.Z - 3; z <= topRight.Z + 2; ++z)
            {
                for (int x = bottomLeft.X - 3; x <= topRight.X + 2; ++x)
                {
                    TerrainTile tile = new(x, z);
                    if (!tile.IsInBounds()) continue;

                    Structure structure = GetStructureOnTile(tile);

                    // for tiles away from the normal area, only update settlements
                    if (!structure || ((x < bottomLeft.X - 1 || x > topRight.X || z < bottomLeft.Z - 1 || z > topRight.Z) &&
                        structure.GetType() != typeof(Settlement))) 
                        continue;

                    structure.ReactToTerrainChange();
                }
            }
        }

        #endregion


        #region Trees and Rocks

        /// <summary>
        /// Populates the terrain with trees and rocks according to the regular percentages.
        /// </summary>
        public void PlaceTreesAndRocks()
            => PlaceTreesAndRocks(m_TreePercentage, m_BlackRockPercentage, m_WhiteRockPercentage, 
                new(0, 0), new(Terrain.Instance.TilesPerSide - 1, Terrain.Instance.TilesPerSide - 1));

        /// <summary>
        /// Populates the area affected by a volcano, centered on the given point and with the given radius, with white rocks.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the volcano area.</param>
        /// <param name="radius">The number of tiles in each direction from the center that are affected by the volcano.</param>
        public void PlaceVolcanoRocks(TerrainPoint center, int radius) 
            => PlaceTreesAndRocks(0, 0, m_VolcanoRockPercentage, new(center.X - radius, center.Z - radius), new(center.X + radius, center.Z + radius));

        /// <summary>
        /// Populates the given area of terrain tiles with a certain percentage of trees and rocks.
        /// </summary>
        /// <param name="treePercent">The percentage of the given area that should be populated by trees.</param>
        /// <param name="blackRockPercent">The percentage of the given area that should be populated by black rocks.</param>
        /// <param name="whiteRockPercent">The percentage of the given area that should be populated by white rocks.</param>
        /// <param name="bottomLeft">The <c>TerrainTile</c> at the bottom left of the given area.</param>
        /// <param name="topRight">The <c>TerrainTile</c> at the top right of the given area.</param>
        private void PlaceTreesAndRocks(float treePercent, float blackRockPercent, float whiteRockPercent, TerrainTile bottomLeft, TerrainTile topRight)
        {
            Random random = new(!GameData.Instance ? 0 : GameData.Instance.MapSeed);

            int height = topRight.Z - bottomLeft.Z;
            int width = topRight.X - bottomLeft.X;
            int totalTiles = height * width;

            int lastTreeIndex = Mathf.RoundToInt(totalTiles * treePercent);
            int lastBlackRockIndex = lastTreeIndex + Mathf.RoundToInt(totalTiles * blackRockPercent);
            int lastWhiteRockIndex = lastBlackRockIndex + Mathf.RoundToInt(totalTiles * whiteRockPercent);

            List<int> spawnIndices = Enumerable.Range(0, totalTiles).ToList();
            int count = spawnIndices.Count;

            for (int i = 0; i < totalTiles; ++i)
            {
                TerrainTile tile = new(bottomLeft.X + i % width, bottomLeft.Z + Mathf.FloorToInt((float)i / height));

                if (!tile.IsInBounds()) continue;

                count--;
                int randomIndex = random.Next(count + 1);
                (spawnIndices[count], spawnIndices[randomIndex]) = (spawnIndices[randomIndex], spawnIndices[count]);

                if (tile.IsUnderwater()) continue;

                if (spawnIndices[count] < lastTreeIndex)
                    SpawnStructure(m_TreePrefab, tile);

                else if (spawnIndices[count] < lastBlackRockIndex)
                    SpawnStructure(m_BlackRockPrefab, tile);

                else if (spawnIndices[count] < lastWhiteRockIndex)
                    SpawnStructure(m_WhiteRockPrefab, tile);
            }
        }

        #endregion


        #region Settlements

        /// <summary>
        /// Creates a settlement of the given faction on the given tile.
        /// </summary>
        /// <param name="tile">The tile that the settlement should occupy.</param>
        /// <param name="faction">The team the settlement should belong to.</param>
        public void CreateSettlement(TerrainTile tile, Faction faction) 
            => SpawnStructure(m_SettlementPrefab, tile, faction);

        /// <summary>
        /// Transforms the given settlement into a ruin.
        /// </summary>
        /// <param name="settlement">The <c>Settlement</c> that should be transformed.</param>
        public void CreateRuin(Settlement settlement)
        {
            TerrainTile tile = settlement.OccupiedTile;
            DespawnStructure(settlement.gameObject);
            SpawnStructure(m_RuinPrefab, tile);
        }

        /// <summary>
        /// Changes the faction the given settlement belongs to.
        /// </summary>
        /// <param name="settlement">The <c>Settlemnet</c> whose faction should be switched.</param>
        /// <param name="faction">The new <c>Faction</c> the settlement should belong to.</param>
        public void ChangeSettlementFaction(Settlement settlement, Faction faction)
        {
            if (faction == settlement.Faction) return;

            RemoveSettlementPosition(settlement.transform.position, settlement.Faction);
            settlement.ChangeFaction(faction);

            if (faction != Faction.NONE)
                AddSettlementPosition(settlement.transform.position, faction);
        }

        /// <summary>
        /// Gets the settlement location at the given index in the list of settlement locations of the given team.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> the settlement that should be returned belongs to.</param>
        /// <param name="index">The index in the settlement location list of the settlement.</param>
        /// <returns>The position at the given index in the settlement position list, null if the index is out of bounds.</returns>
        public Vector3? GetSettlementLocation(Faction faction, int index) 
            => index >= m_SettlementLocations[(int)faction].Count ? null : m_SettlementLocations[(int)faction][index];

        /// <summary>
        /// Gets the number of settlements of the given faction currently on the terrain.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose number of settlements should be returned.</param>
        /// <returns>The number of settlements of the given team.</returns>
        public int GetSettlementsNumber(Faction faction) => m_SettlementLocations[(int)faction].Count;

        /// <summary>
        /// Adds the position of a settlement of the given faction to the settlement locations list.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose settlement position should be removed.</param>
        /// <param name="position">The position of the settlement.</param>
        public void AddSettlementPosition(Vector2 position, Faction faction) 
            => m_SettlementLocations[(int)faction].Add(position);

        /// <summary>
        /// Removes the position of a settlement of the given faction from the settlement locations list.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose settlement position should be removed.</param>
        /// <param name="position">The position of the settlement.</param>
        public void RemoveSettlementPosition(Vector2 position, Faction faction) 
            => m_SettlementLocations[(int)faction].Remove(position);


        #region Leader

        /// <summary>
        /// Checks whether the given faction has a leader that is in a settlement.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be checked.</param>
        /// <returns>True if the faction has a leader that is in a settlement, false otherwise.</returns>
        public bool HasSettlementLeader(Faction faction) => m_LeaderSettlements[(int)faction];

        /// <summary>
        /// Gets the <c>Settlement</c> the faction leader is part of, if such a settlement exists.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader should be returned.</param>
        /// <returns>The <c>Settlement</c> of the team's leader, null if the leader is not part of a settlement.</returns>
        public Settlement GetLeaderSettlement(Faction faction) => m_LeaderSettlements[(int)faction];

        /// <summary>
        /// Sets the given settlement as the leader of the given faction.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose settlement should be set.</param>
        /// <param name="settlement">The <c>Settlement</c> that should be set as the leader.</param>
        public void SetLeaderSettlement(Faction faction, Settlement settlement)
        {
            UnsetLeaderSettlement(faction);

            m_LeaderSettlements[(int)faction] = settlement;
            settlement.SetLeader(true);
            UnitManager.Instance.OnNewLeaderGained?.Invoke();
        }

        /// <summary>
        /// Removes the leader settlement of the given faction, if it exists.
        /// </summary>
        /// <param name="faction">The <c>Faction</c> whose leader settlement should be removed.</param>
        public void UnsetLeaderSettlement(Faction faction)
        {
            if (!HasSettlementLeader(faction)) return;

            m_LeaderSettlements[(int)faction].SetLeader(false);
            m_LeaderSettlements[(int)faction] = null;
        }

        #endregion

        #endregion


        #region Fields

        /// <summary>
        /// Creates a field belonging to the given faction on the given tile.
        /// </summary>
        /// <param name="tile">The <c>TerrainTile</c> the field should occupy.</param>
        /// <param name="faction">The <c>Faction</c> the field should belong to.</param>
        /// <returns>The <c>Field</c> that was created.</returns>
        private void SpawnField(TerrainTile tile, Faction faction, Settlement spawningSettlement) 
        {
            Field field = SpawnStructure(m_FieldPrefab, tile, faction).GetComponent<Field>();
            field.AddSettlementServed(spawningSettlement);
        }

        /// <summary>
        /// Creates fields in the flat spaces in a 5x5 square around the settlement.
        /// </summary>
        /// <returns>The number of fields created around the settlement.</returns>
        public int CreateSettlementFields(Settlement settlement)
        {
            int fields = 0;

            for (int z = -2; z <= 2; ++z)
            {
                for (int x = -2; x <= 2; ++x)
                {
                    // we can only place fields on the tiles above, below, to the left, to the right, or diagonally
                    // from the tile occupied by the structure
                    if ((x, z) == (0, 0) || (x != 0 && z != 0 && Mathf.Abs(x) != Mathf.Abs(z)))
                        continue;

                    TerrainTile tile = new(settlement.OccupiedTile.X + x, settlement.OccupiedTile.Z + z);

                    if (!tile.IsInBounds() || !tile.IsFlat())
                        continue;

                    Structure structure = tile.GetStructure();

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Swamp) || structureType == typeof(Settlement))
                            continue;

                        else if (structureType == typeof(Rock))
                        {
                            fields--;
                            continue;
                        }

                        else if (structureType == typeof(Field))
                        {
                            if (structure.Faction == settlement.Faction)
                            {
                                ((Field)structure).AddSettlementServed(settlement);
                                fields++;
                            }

                            continue;
                        }

                        else if (structureType == typeof(Tree))
                            DespawnStructure(structure.gameObject);
                    }

                    SpawnField(tile, settlement.Faction, settlement);
                    fields++;
                }
            }

            return fields;
        }

        /// <summary>
        /// Adds extra fields to fill in the 5x5 square around a city.
        /// </summary>
        public void AddCityFields(Settlement city)
        {
            //fill in the blank spaces between the parallels and diagonals with fields
            for (int z = -2; z <= 2; ++z)
            {
                for (int x = -2; x <= 2; ++x)
                {
                    if (x == 0 || z == 0 || Mathf.Abs(x) == Mathf.Abs(z)) continue;

                    TerrainTile neighborTile = new(city.OccupiedTile.X + x, city.OccupiedTile.Z + z);

                    if (!neighborTile.IsInBounds() || !neighborTile.IsFlat()) continue;

                    Structure structure = neighborTile.GetStructure();

                    if (structure)
                    {
                        Type structureType = structure.GetType();

                        if (structureType == typeof(Field) && structure.Faction == city.Faction)
                        {
                            ((Field)structure).AddSettlementServed(city);
                            continue;
                        }

                        else if (structureType == typeof(Tree))
                            DespawnStructure(structure.gameObject);

                        else
                            continue;
                    }

                    SpawnField(neighborTile, city.Faction, city);
                }
            }
        }

        /// <summary>
        /// Removes the extra fields added to fill in the 5x5 square around the city.
        /// </summary>
        public void RemoveCityFields(Settlement city)
        {
            // remove fields between the diagonals and parallels
            for (int z = -2; z <= 2; ++z)
            {
                for (int x = -2; x <= 2; ++x)
                {
                    if (x == 0 || z == 0 || Mathf.Abs(x) == Mathf.Abs(z))
                        continue;

                    TerrainTile neighborTile = new(city.OccupiedTile.X + x, city.OccupiedTile.Z + z);
                    Structure structure = neighborTile.GetStructure();

                    if (!structure || structure.GetType() != typeof(Field) || structure.Faction != city.Faction)
                        continue;

                    ((Field)structure).RemoveSettlementServed(city);
                }
            }
        }

        #endregion


        #region Swamps

        /// <summary>
        /// Creates a swamp on the given tile.
        /// </summary>
        /// <param name="tile">The <c>TerrainTile</c> the swamp should occupy.</param>
        public void SpawnSwamp(TerrainTile tile) => SpawnStructure(m_SwampPrefab, tile);

        #endregion
    }
}