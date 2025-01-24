using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The <c>Terrain</c> class is a <c>MonoBehavior</c> which handles the generation and modification of the terrain.
    /// </summary>
    public class Terrain : MonoBehaviour
    {
        [SerializeField] private GameObject m_ChunkPrefab;

        [Header("Terrain Properties")]
        [SerializeField] private int m_ChunksPerSide = 2;
        [SerializeField] private int m_TilesPerChunkSide = 5;
        [SerializeField] private int m_UnitsPerTileSide = 50;
        [SerializeField] private int m_MaxHeightSteps = 7;
        [SerializeField] private int m_StepHeight = 20;

        [Header("Terrain Material")]
        [SerializeField] private Material m_TerrainMaterial;

        private static Terrain m_Instance;
        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        public static Terrain Instance { get => m_Instance; }

        private TerrainChunk[,] m_ChunkMap;
        private HashSet<(int, int)> m_ModifiedChunks;

        public INoiseGenerator MapGenerator { get => GetComponent<INoiseGenerator>(); }

        private TerrainPoint m_TerrainCenter;
        public TerrainPoint TerrainCenter { get => m_TerrainCenter; }

        private int m_WaterLevel;
        /// <summary>
        /// Gets the current height of the water.
        /// </summary>
        public int WaterLevel { get => m_WaterLevel; }

        // modified points
        (int lowestX, int lowestZ, int highestX, int highestZ) m_ModifiedPointRange;


        #region Public getters and setters for serialized fields

        /// <summary>
        /// Gets the number of chunks on one side of the terrain.
        /// </summary>
        public int ChunksPerSide { get => m_ChunksPerSide; }
        /// <summary>
        /// Gets the number of tiles on one side of the terrain.
        /// </summary>
        public int TilesPerSide { get => m_TilesPerChunkSide * m_ChunksPerSide; }
        /// <summary>
        /// Gets the number of units on one side of the terrain.
        /// </summary>
        public int UnitsPerSide { get => TilesPerSide * m_UnitsPerTileSide; }
        /// <summary>
        /// Gets the number of tiles on one side of a chunk.
        /// </summary>
        public int TilesPerChunkSide { get => m_TilesPerChunkSide; }
        /// <summary>
        /// Gets the number of units on one side of a chunk.
        /// </summary>
        public int UnitsPerChunkSide { get => m_UnitsPerTileSide * m_TilesPerChunkSide; }
        /// <summary>
        /// Gets the number of units on one side of a tile.
        /// </summary>
        public int UnitsPerTileSide { get => m_UnitsPerTileSide; }
        /// <summary>
        /// Gets the maximum number of times a point on the terrain can be increased.
        /// </summary>
        public int MaxHeightSteps { get => m_MaxHeightSteps; }
        /// <summary>
        /// Gets the height of one terrain point increase.
        /// </summary>
        public int StepHeight { get => m_StepHeight; }
        /// <summary>
        /// Gets the maximum possible height of the terrain.
        /// </summary>
        public int MaxHeight { get => m_MaxHeightSteps * m_StepHeight; }

        /// <summary>
        /// Gets the terrain material.
        /// </summary>
        public Material TerrainMaterial { get => m_TerrainMaterial; }

        #endregion



        private void Awake()
        {
            if (m_Instance)
                Destroy(gameObject);

            m_Instance = this;

            m_ChunkMap = new TerrainChunk[m_ChunksPerSide, m_ChunksPerSide];
            m_TerrainCenter = new(UnitsPerSide / 2, UnitsPerSide / 2, getClosestPoint: true);
        }


        #region Terrain Generation

        /// <summary>
        /// Generates the terrain and sets up the frame, water plane, and border.
        /// </summary>
        public void CreateTerrain()
        {
            MapGenerator.Setup();

            GenerateTerrain();
            SetupTerrainShader();
            GameUI.Instance.SetInitialMinimapTexture();

            Frame.Instance.Create();
            Water.Instance.Create();
            TerrainBorder.Instance.Create();
        }

        /// <summary>
        /// Generates the terrain.
        /// </summary>
        private void GenerateTerrain()
        {
            for (int z = 0; z < m_ChunksPerSide; ++z)
            {
                for (int x = 0; x < m_ChunksPerSide; ++x)
                {                        
                    GameObject newChunk = Instantiate(m_ChunkPrefab,
                        position: new Vector3(x * UnitsPerChunkSide, 0, z * UnitsPerChunkSide),
                        rotation: Quaternion.identity,
                        parent: transform
                    );

                    m_ChunkMap[z, x] = newChunk.GetComponent<TerrainChunk>();
                    m_ChunkMap[z, x].Setup(x, z);
                }
            }
        }

        /// <summary>
        /// Sets up the shader for the texturing of the terrain.
        /// </summary>
        private void SetupTerrainShader()
        {
            m_TerrainMaterial.SetInt("waterLevel", m_WaterLevel);
        }

        #endregion



        #region Terrain Modificiation

        /// <summary>
        /// Calls the <see cref="ChangePointHeight(TerrainPoint, bool)"/> to modify the terrain, 
        /// then resets the meshes of all the chunks that have been modified..
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> which should be modified.</param>
        /// <param name="lower">Whether the point should be lowered or elevated.</param>
        public void ModifyTerrain(TerrainPoint point, bool lower)
        {
            m_ModifiedPointRange.lowestX = m_ModifiedPointRange.lowestZ = TilesPerSide;
            m_ModifiedPointRange.highestX = m_ModifiedPointRange.highestZ = 0;

            m_ModifiedChunks = new();
            ChangePointHeight(point, lower);

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
                GetChunkByIndex(chunkIndex).SetMesh();

            m_ModifiedChunks = new();
            GameController.Instance.OnTerrainModified?.Invoke();
        }

        /// <summary>
        /// Elevates or lowers the given point on the terrain in all the chunks the point belongs to.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> which should be modified.</param>
        /// <param name="lower">Whether the point should be lowered or elevated.</param>
        public void ChangePointHeight(TerrainPoint point, bool lower, int? height = null)
        {
            foreach ((int, int) chunkIndex in point.TouchingChunks)
            {
                if (!m_ModifiedChunks.Contains(chunkIndex))
                    m_ModifiedChunks.Add(chunkIndex);

                if (height.HasValue)
                    GetChunkByIndex(chunkIndex).SetVertexHeight(point, height.Value);
                else
                    GetChunkByIndex(chunkIndex).ChangeHeight(point, lower);
            }

            m_ModifiedPointRange.lowestX = Mathf.Min(point.GridX, m_ModifiedPointRange.lowestX);
            m_ModifiedPointRange.lowestZ = Mathf.Min(point.GridZ, m_ModifiedPointRange.lowestZ);
            m_ModifiedPointRange.highestX = Mathf.Max(point.GridX, m_ModifiedPointRange.highestX);
            m_ModifiedPointRange.highestZ = Mathf.Max(point.GridZ, m_ModifiedPointRange.highestZ);

            if (point.IsOnEdge)
                TerrainBorder.Instance.ModifyWallAtPoint(point);
        }

        public (int lowestX, int lowestZ, int highestX, int highestZ) GetAffectedTileRange()
        {
            return (
                Mathf.Clamp(m_ModifiedPointRange.lowestX - 1, 0, m_ModifiedPointRange.lowestX),
                Mathf.Clamp(m_ModifiedPointRange.lowestZ - 1, 0, m_ModifiedPointRange.lowestZ),
                m_ModifiedPointRange.highestX,
                m_ModifiedPointRange.highestZ
            );
        }


        /// <summary>
        /// Increases the water level by one step.
        /// </summary>
        public void RaiseWaterLevel()
        {
            m_WaterLevel += StepHeight;
            m_TerrainMaterial.SetInt("waterLevel", m_WaterLevel);
        }

        /// <summary>
        /// Gets all points that are a given number of tiles away from the given center point.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center.</param>
        /// <param name="distance">The distance in tiles from the center to the points that should be returned.</param>
        /// <returns></returns>
        private IEnumerable<TerrainPoint> GetPointsAtDistance(TerrainPoint center, int distance)
        {
            for (int z = -distance; z <= distance; ++z)
            {
                int targetZ = center.GridZ + z;
                if (targetZ < 0 || targetZ > TilesPerSide)
                    continue;

                int[] xs;

                if (z == -distance || z == distance)
                    xs = Enumerable.Range(-distance, 2 * distance + 1).ToArray();
                else
                    xs = new[] { -distance, distance };

                foreach (int x in xs)
                {
                    int targetX = center.GridX + x;
                    if (targetX < 0 || targetX > TilesPerSide)
                        continue;

                    yield return new TerrainPoint(targetX, targetZ);
                }
            }
        }

        /// <summary>
        /// Lowers the terrain within the given number of tiles from the given center point. nearly to the water level.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center.</param>
        /// <param name="radius">The number of tiles in each direction that the lowering should affect.</param>
        /// <param name="randomizerSeed">The seed for the randomizer used when choosing random lowering steps.</param>
        public void CauseEarthquake(TerrainPoint center, int radius, int randomizerSeed)
        {
            m_ModifiedPointRange.lowestX = m_ModifiedPointRange.lowestZ = TilesPerSide;
            m_ModifiedPointRange.highestX = m_ModifiedPointRange.highestZ = 0;

            m_ModifiedChunks = new();

            Random random = new(randomizerSeed);
            Dictionary<(int, int), int> newHeights = new();

            for (int z = -radius; z <= radius; ++z)
            {
                for (int x = -radius; x <= radius; ++x)
                {
                    if (center.GridX + x < 0 || center.GridX + x > TilesPerSide ||
                        center.GridZ + z < 0 || center.GridZ + z > TilesPerSide)
                        continue;

                    TerrainPoint point = new(center.GridX + x, center.GridZ + z);
                    int randomStep = random.Next(0, 2);
                    ChangePointHeight(point, false, m_WaterLevel + (randomStep * m_StepHeight));

                    newHeights.Add((point.GridX, point.GridZ), m_WaterLevel);
                }
            }

            int distance = radius;

            // updating the points around the affected area in case they break the height property
            int changedHeightsInIter = 1;
            while (changedHeightsInIter > 0)
            {
                changedHeightsInIter = 0;
                distance++;

                foreach (TerrainPoint point in GetPointsAtDistance(center, distance))
                {
                    foreach (TerrainPoint neighbor in point.Neighbors)
                    {
                        if (Mathf.Abs(neighbor.Y - point.Y) > m_StepHeight)
                        {
                            int x = point.GridX, z = point.GridZ;

                            if (point.GridX == center.GridX - distance)
                                x++;

                            if (point.GridX == center.GridX + distance)
                                x--;

                            if (point.GridZ == center.GridZ - distance)
                                z++;

                            if (point.GridZ == center.GridZ + distance)
                                z--;

                            if ((x, z) == (point.GridX, point.GridZ))
                                continue;

                            ChangePointHeight(point, false, newHeights[(x, z)] + m_StepHeight);
                            changedHeightsInIter++;
                            break;
                        }
                    }
                    newHeights.Add((point.GridX, point.GridZ), point.Y);
                }
            }

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
            {
                TerrainChunk chunk = GetChunkByIndex(chunkIndex);
                chunk.RecomputeAllCenters();
                chunk.SetMesh();
            }

            m_ModifiedChunks = new();
            GameController.Instance.OnTerrainModified?.Invoke();
        }

        /// <summary>
        /// Elevates the terrain within the given number of tiles of the given center point to form a steep mountain.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the elevation.</param>
        /// <param name="radius">The number of tiles in each direction that the elevation should affect.</param>
        public void CauseVolcano(TerrainPoint center, int radius)
        {
            // for updating structures
            m_ModifiedPointRange.lowestX = m_ModifiedPointRange.lowestZ = TilesPerSide;
            m_ModifiedPointRange.highestX = m_ModifiedPointRange.highestZ = 0;

            m_ModifiedChunks = new();

            int maxHeightOnEdge = -1;

            foreach (TerrainPoint point in GetPointsAtDistance(center, radius))
                if (point.Y > maxHeightOnEdge)
                    maxHeightOnEdge = point.Y;

            maxHeightOnEdge += m_StepHeight;

            int distance;
            for (distance = 0; distance <= radius; ++distance)
                foreach (TerrainPoint point in GetPointsAtDistance(center, distance))
                    ChangePointHeight(point, false, maxHeightOnEdge + (radius - distance) * m_StepHeight);

            distance = radius;

            // updating the points around the affected area in case they break the height property
            int changedHeightsInIter = 1;
            while (changedHeightsInIter > 0)
            {
                changedHeightsInIter = 0;
                distance++;

                foreach (TerrainPoint point in GetPointsAtDistance(center, distance))
                {
                    foreach (TerrainPoint neighbor in point.Neighbors)
                    {
                        if (Mathf.Abs(neighbor.Y - point.Y) > m_StepHeight)
                        {
                            ChangePointHeight(point, false, maxHeightOnEdge + (radius - distance) * m_StepHeight);
                            changedHeightsInIter++;
                            break;
                        }
                    }
                }
            }

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
            {
                TerrainChunk chunk = GetChunkByIndex(chunkIndex);
                chunk.RecomputeAllCenters();
                chunk.SetMesh();
            }

            m_ModifiedChunks = new();
            GameController.Instance.OnTerrainModified?.Invoke();
        }

        #endregion



        #region Chunk Info

        /// <summary>
        /// Checks whether the given chunk index is within the bounds of the terrain.
        /// </summary>
        /// <param name="chunkIndex">The (x, z) index of the chunk which is to be tested.</param>
        /// <returns>True if the chunk index is in bounds, false otherwise.</returns>
        public bool IsChunkInBounds((int x, int z) chunkIndex)
            => chunkIndex.x >= 0 && chunkIndex.z >= 0 && chunkIndex.x < ChunksPerSide && chunkIndex.z < ChunksPerSide;

        /// <summary>
        /// Gets the <c>TerrainChunk</c> at the given index of the chunk map.
        /// </summary>
        /// <param name="chunkIndex">The (x, z) index of a chunk in the chunk map.</param>
        /// <returns>The <c>TerrainChunk</c> at the given index.</returns>
        public TerrainChunk GetChunkByIndex((int x, int z) chunkIndex) => m_ChunkMap[chunkIndex.z, chunkIndex.x];

        /// <summary>
        /// Gets the index of the chunk that the current position is contained in.
        /// </summary>
        /// <param name="position">A <c>Vector3</c> representing the current position.</param>
        /// <returns>A tuple representing the (x, z) index of the chunk</returns>
        public (int x, int z) GetChunkIndex(Vector3 position)
            => (position.x == UnitsPerSide ? ChunksPerSide - 1 : (int)position.x / UnitsPerChunkSide,
                position.z == UnitsPerSide ? ChunksPerSide - 1 : (int)position.z / UnitsPerChunkSide);

        /// <summary>
        /// Gets the index of the chunk that the current point is contained in.
        /// </summary>
        /// <param name="point">A tuple representing the (x, z) index of the current point in the terrain mesh.</param>
        /// <returns>A tuple representing the (x, z) index of the chunk.</returns>
        public (int x, int z) GetChunkIndex((int x, int z) point)
            => (point.x == TilesPerSide ? ChunksPerSide - 1 : point.x / TilesPerChunkSide,
                point.z == TilesPerSide ? ChunksPerSide - 1 : point.z / TilesPerChunkSide);

        #endregion



        #region Point Info

        /// <summary>
        /// Checks whether the given point is within the bounds of the terrain.
        /// </summary>
        /// <param name="point">The (x, z) coordinates of the point which is to be tested.</param>
        /// <returns>True if the point is in bounds, false otherwise.</returns>
        public bool IsPointInBounds((int x, int z) point) => point.x >= 0 && point.x <= TilesPerSide && point.z >= 0 && point.z <= TilesPerSide;

        /// <summary>
        /// Checks whether the given point is the last point either on the X axis or the Z axis of the terrain grid.
        /// </summary>
        /// <param name="point">The (x, z) coordinates of the point which is to be tested.</param>
        /// <returns>True if the point is the last point, otherwise false.</returns>
        public bool IsLastPoint((int x, int z) point) => point.x == TilesPerSide || point.z == TilesPerSide;

        /// <summary>
        /// Gets the height of the terrain at the given point.
        /// </summary>
        /// <param name="point">The (x, z) coordinates of the point whose height should be returned.</param>
        /// <returns>An <c>int</c> representing the height at the given point.</returns>
        public int GetPointHeight((int x, int z) point) => GetPointHeight(GetChunkIndex(point), point);

        /// <summary>
        /// Gets the height of the terrain at the given point.
        /// </summary>
        /// <param name="chunkIndex">The (x, z) index of a terrain chunk.</param>
        /// <param name="point">The (x, z) coordinates of the point whose height should be returned.</param>
        /// <returns>An <c>int</c> representing the height at the given point.</returns>
        public int GetPointHeight((int x, int z) chunkIndex, (int x, int z) point) => GetChunkByIndex(chunkIndex).GetPointHeight(point);

        #endregion



        #region Tile Center Info

        /// <summary>
        /// Gets the height of the terrain at the center of the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile whose tile center height should be returned.</param>
        /// <returns>An <c>int</c> representing the height of the center of the given tile.</returns>
        public int GetTileCenterHeight((int x, int z) tile) => GetTileCenterHeight(GetChunkIndex(tile), tile);

        /// <summary>
        /// Gets the height of the terrain at the center of the given tile.
        /// </summary>
        /// <param name="chunkIndex">The (x, z) index of a terrain chunk.</param>
        /// <param name="tile">The (x, z) coordinates of the tile whose tile center height should be returned.</param>
        /// <returns>An <c>int</c> representing the height of the center of the given tile.</returns>
        public int GetTileCenterHeight((int x, int z) chunkIndex, (int x, int z) tile) => GetChunkByIndex(chunkIndex).GetTileCenterHeight(tile);

        #endregion



        #region Tile Info

        /// <summary>
        /// Gets the coordinates on the terrain of the points on the corners of the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile whose corners should be returned.</param>
        /// <returns>A list of <c>TerrainPoint</c>s representing the corners of the tile.</returns>
        public List<TerrainPoint> GetTileCorners((int x, int z) tile)
        {
            if (tile.x >= TilesPerSide)
                tile.x -= 1;

            if (tile.z >= TilesPerSide)
                tile.z -= 1;

            return new() { new(tile.x, tile.z), new(tile.x + 1, tile.z), new(tile.x, tile.z + 1), new(tile.x + 1, tile.z + 1) };
        }

        /// <summary>
        /// Checks whether all the points at the corners of the given tile are at the same height and whether they are above the water level.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is flat and not underwater, false otherwise.</returns>
        public bool IsTileFlat((int x, int z) tile) => IsTileFlat(GetChunkIndex(tile), tile);

        /// <summary>
        /// Checks whether all the points at the corners of the given tile are at the same height and whether they are above the water level.
        /// </summary>
        /// <param name="chunkIndex">The (x, z) index of a terrain chunk.</param>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is flat and not underwater, false otherwise.</returns>
        public bool IsTileFlat((int x, int z) chunkIndex, (int x, int z) tile) => GetChunkByIndex(chunkIndex).IsTileFlat(tile);

        /// <summary>
        /// Checks whether all the corner points of the given tile are on or below the water level.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is underwater, false otherwise.</returns>
        public bool IsTileUnderwater((int x, int z) tile)
        {
            foreach (TerrainPoint point in GetTileCorners(tile))
                if (point.Y > m_WaterLevel)
                    return false;

            return true;
        }

        /// <summary>
        /// Checks whether there is a structure on the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is occupied by a structure, false otherwise.</returns>
        public bool IsTileOccupied((int x, int z) tile) => IsTileOccupied(GetChunkIndex(tile), tile);

        /// <summary>
        /// Checks whether there is a structure on the given tile.
        /// </summary>
        /// <param name="chunkIndex">The (x, z) index of a terrain chunk.</param>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is occupied by a structure, false otherwise.</returns>
        public bool IsTileOccupied((int x, int z) chunkIndex, (int x, int z) tile) => GetChunkByIndex(chunkIndex).IsTileOccupied(tile);

        public bool HasTileSettlement((int x, int z) tile) => GetStructureOnTile(tile).GetType() == typeof(Settlement);

        /// <summary>
        /// Gets the <c>Structure</c> occupying the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile whose <c>Structure</c> should be returned.</param>
        /// <returns>The <c>Structure</c> occupying the given tile, or <c>null</c> if the tile is unoccupied.</returns>
        public Structure GetStructureOnTile((int x, int z) tile) => GetStructureOnTile(GetChunkIndex(tile), tile);

        /// <summary>
        /// Gets the <c>Structure</c> occupying the given tile.
        /// </summary>
        /// <param name="chunkIndex">The (x, z) index of a terrain chunk.</param>
        /// <param name="tile">The (x, z) coordinates of the tile whose <c>Structure</c> should be returned.</param>
        /// <returns>The <c>Structure</c> occupying the given tile, or <c>null</c> if the tile is unoccupied.</returns>
        public Structure GetStructureOnTile((int x, int z) chunkIndex, (int x, int z) tile) => GetChunkByIndex(chunkIndex).GetStructureOnTile(tile);

        /// <summary>
        /// Sets the given structure to occupy the given tile.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile whose <c>Structure</c> should be returned.</param>
        /// <param name="structure">The <c>Structure</c> that should be placed on the tile.</param>
        public void SetOccupiedTile((int x, int z) tile, Structure structure) => GetChunkByIndex(GetChunkIndex(tile)).SetOccupiedTile(tile, structure);

        /// <summary>
        /// Checks whether when moving from one point of a tile to another, a <c>Structure</c> or water is crossed.
        /// </summary>
        /// <param name="start">A <c>TerrainPoint</c> representing the starting point.</param>
        /// <param name="end">A <c>TerrainPoint</c> representing the end point.</param>
        /// <returns>True if no <c>Structure</c> or water is crossed, false otherwise.</returns>
        public bool CanCrossTile(TerrainPoint start, TerrainPoint end)
        {
            int dx = end.GridX - start.GridX;
            int dz = end.GridZ - start.GridZ;

            // we are not moving diagonally
            if (Mathf.Abs(dx) != Mathf.Abs(dz))
            {
                // we wnat to check if the unit is going into the water or just along the shore
                if (start.Y == 0 && end.Y == 0)
                {
                    // if there is water on both sides of the line the unit is travelling, then it is going into the water
                    (int x, int z)[] diagonals = new (int, int)[2];
                    if (dz == 0) diagonals = new (int, int)[2] { (end.GridX, end.GridZ - 1), (end.GridX, end.GridZ + 1) };
                    if (dx == 0) diagonals = new (int, int)[2] { (end.GridX - 1, end.GridZ), (end.GridX + 1, end.GridZ) };

                    foreach ((int x, int z) diagonal in diagonals)
                    {
                        if (diagonal.x < 0 || diagonal.x > TilesPerSide || diagonal.z < 0 || diagonal.z > TilesPerSide)
                            continue;

                        if (GetPointHeight(diagonal) > 0)
                            return true;
                    }
                    return false;
                }

                // otherwise it's fine
                return true;
            }

            // we'll be crossing water
            if (start.Y == 0 && end.Y == 0)
                return false;

            int x = start.GridX;
            int z = start.GridZ;

            if (dx > 0 && dz < 0)
                z -= 1;
            else if (dx < 0 && dz > 0)
                x -= 1;
            else if (dx < 0 && dz < 0)
            {
                x -= 1;
                z -= 1;
            }

            if (x < 0 || z < 0)
                return false;

            Structure structure = GetStructureOnTile((x, z));
            if (!structure || structure.GetType() == typeof(Field) || structure.GetType() == typeof(Swamp))
                return true;

            return false;
        }

        #endregion



        /// <summary>
        /// Checks whether the level of the water has reached the maximum height of the terrain.
        /// </summary>
        /// <returns>True if the level of the water has reached the maximum height, false otherwise.</returns>
        public bool HasReachedMaxWaterLevel() => m_WaterLevel == MaxHeight;
    }
}