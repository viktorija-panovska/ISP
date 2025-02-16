using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The <c>Terrain</c> class handles the generation and modification of the terrain chunks.
    /// </summary>
    [RequireComponent(typeof(INoiseGenerator))]
    public class Terrain : MonoBehaviour
    {
        #region Inspector Fields

        [Tooltip("The prefab from which the terrain chunks are spawned.")]
        [SerializeField] private GameObject m_ChunkPrefab;

        [Tooltip("A material that uses the Terrain shader.")]
        [SerializeField] private Material m_TerrainMaterial;


        [Header("Terrain Properties")]

        [Tooltip("The number of terrain chunks on each side of the terrain.")]
        [SerializeField] private int m_ChunksPerSide = 7;

        [Tooltip("The number of tiles on each side of a terrain chunk.")]
        [SerializeField] private int m_TilesPerChunkSide = 8;

        [Tooltip("The length of each side of a terrain tile, in Unity units.")]
        [SerializeField] private int m_UnitsPerTileSide = 50;

        [Tooltip("The distance of one height step, in Unity units (i.e. the only possible height difference between two neighboring points that are not on the same height).")]
        [SerializeField] private int m_StepHeight = 50;

        [Tooltip("The maximum number of height steps above the initial water level that a terrain point can sit at.")]
        [SerializeField] private int m_MaxHeightSteps = 7;

        #endregion


        #region Class Fields

        private static Terrain m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static Terrain Instance { get => m_Instance; }

        /// <summary>
        /// Gets the <c>INoiseGenerator</c> component attached to the gameObject.
        /// </summary>
        public INoiseGenerator NoiseGenerator { get => GetComponent<INoiseGenerator>(); }

        private TerrainPoint m_TerrainCenter;
        /// <summary>
        /// Gets the terrain point at (or closest to) the center of the terrain.
        /// </summary>
        public TerrainPoint TerrainCenter { get => m_TerrainCenter; }

        /// <summary>
        /// A 2D array where each terrain chunk is stored in the cell corresponding to its index in the terrain chunk grid.
        /// </summary>
        private TerrainChunk[,] m_ChunkMap;

        /// <summary>
        /// Gets the number of chunks on each side of the terrain.
        /// </summary>
        public int ChunksPerSide { get => m_ChunksPerSide; }
        /// <summary>
        /// Gets the number of tiles on each side of the terrain.
        /// </summary>
        public int TilesPerSide { get => m_TilesPerChunkSide * m_ChunksPerSide; }
        /// <summary>
        /// Gets the length of each side of the terrain, in Unity units.
        /// </summary>
        public int UnitsPerSide { get => TilesPerSide * m_UnitsPerTileSide; }
        /// <summary>
        /// Gets the number of tiles on each side of a terrain chunk.
        /// </summary>
        public int TilesPerChunkSide { get => m_TilesPerChunkSide; }
        /// <summary>
        /// Gets the length of each side of a terrain chunk, in Unity units.
        /// </summary>
        public int UnitsPerChunkSide { get => m_UnitsPerTileSide * m_TilesPerChunkSide; }
        /// <summary>
        /// Gets the length of each side of a terrain tile, in Unity units.
        /// </summary>
        public int UnitsPerTileSide { get => m_UnitsPerTileSide; }

        /// <summary>
        /// Gets the distance of one height step, in Unity units.
        /// </summary>
        public int StepHeight { get => m_StepHeight; }
        /// <summary>
        /// Gets the maximum possible height of a point on the terrain, in Unity units.
        /// </summary>
        public int MaxHeight { get => m_MaxHeightSteps * m_StepHeight; }

        private int m_WaterLevel;
        /// <summary>
        /// Gets the current height of the water.
        /// </summary>
        public int WaterLevel { get => m_WaterLevel; }

        /// <summary>
        /// Gets the terrain material.
        /// </summary>
        public Material TerrainMaterial { get => m_TerrainMaterial; }

        /// <summary>
        /// A set containing all the chunks that have points whose heights have been altered.
        /// </summary>
        private HashSet<(int, int)> m_ModifiedChunks = new();
        /// <summary>
        /// The coordinates of the bottom-left point and the top-right point of a rectangular 
        /// area containing all points whose heights were changed a terrain modification.
        /// </summary>
        (int bottomX, int bottomZ, int topX, int topZ) m_ModifiedAreaCorners;

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

            m_ChunkMap = new TerrainChunk[m_ChunksPerSide, m_ChunksPerSide];
            m_TerrainCenter = new(UnitsPerSide / 2, UnitsPerSide / 2, getClosestPoint: true);

            m_ModifiedAreaCorners.bottomX = m_ModifiedAreaCorners.bottomZ = TilesPerSide;
            m_ModifiedAreaCorners.topX = m_ModifiedAreaCorners.topZ = 0;
        }

        #endregion


        #region Terrain Generation

        /// <summary>
        /// Sets up the terrain, triggering the generation of its shape.
        /// </summary>
        public void Create()
        {
            NoiseGenerator.Setup();
            SetupChunks();
            SetupTerrainShader();
        }

        /// <summary>
        /// Instantiates all terrain chunks in the grid.
        /// </summary>
        private void SetupChunks()
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
        /// Sets up the variables in the Terrain shader of the terrain material.
        /// </summary>
        private void SetupTerrainShader()
            => m_TerrainMaterial.SetInteger("waterLevel", m_WaterLevel);

        #endregion



        #region Terrain Modificiation

        /// <summary>
        /// Modifies the terrain 
        /// </summary>
        /// <param name="point">The point that the player has clicked.</param>
        /// <param name="lower">True if the point heights should be decreased, false if they should be increased.</param>
        public (TerrainPoint, TerrainPoint) ModifyTerrain(TerrainPoint point, bool lower)
        {
            ChangePointHeight(point, lower);

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
                GetChunkByIndex(chunkIndex).SetMesh();

            // add one point to the left to also affect the tile the bottom left point is the bottom right corner of
            (TerrainPoint bottomLeft, TerrainPoint topRight) modifiedAreaEdges = (
                new(m_ModifiedAreaCorners.bottomX, m_ModifiedAreaCorners.bottomZ),
                new(m_ModifiedAreaCorners.topX, m_ModifiedAreaCorners.topZ)
            );

            m_ModifiedChunks = new();
            m_ModifiedAreaCorners.bottomX = m_ModifiedAreaCorners.bottomZ = TilesPerSide;
            m_ModifiedAreaCorners.topX = m_ModifiedAreaCorners.topZ = 0;

            return modifiedAreaEdges;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <param name="lower"></param>
        /// <param name="height"></param>
        public void ChangePointHeight(TerrainPoint point, bool lower, int? height = null)
        {
            foreach ((int, int) chunkIndex in GetAllTouchingChunks(point))
            {
                if (!m_ModifiedChunks.Contains(chunkIndex))
                    m_ModifiedChunks.Add(chunkIndex);

                if (height.HasValue)
                    GetChunkByIndex(chunkIndex).SetVertexHeight(point, height.Value);
                else
                    GetChunkByIndex(chunkIndex).ChangeHeight(point, lower);
            }

            m_ModifiedAreaCorners.bottomX = Mathf.Min(point.X, m_ModifiedAreaCorners.bottomX);
            m_ModifiedAreaCorners.bottomZ = Mathf.Min(point.Z, m_ModifiedAreaCorners.bottomZ);
            m_ModifiedAreaCorners.topX = Mathf.Max(point.X, m_ModifiedAreaCorners.topX);
            m_ModifiedAreaCorners.topZ = Mathf.Max(point.Z, m_ModifiedAreaCorners.topZ);
        }

        /// <summary>
        /// Lowers the terrain within the given number of tiles from the given center point. nearly to the water level.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center.</param>
        /// <param name="radius">The number of tiles in each direction that the lowering should affect.</param>
        /// <param name="randomizerSeed">The seed for the randomizer used when choosing random lowering steps.</param>
        public (TerrainPoint, TerrainPoint) CauseEarthquake(TerrainPoint center, int radius, int randomizerSeed)
        {
            Random random = new(randomizerSeed);
            Dictionary<(int, int), int> newHeights = new();

            for (int z = -radius; z <= radius; ++z)
            {
                for (int x = -radius; x <= radius; ++x)
                {
                    if (center.X + x < 0 || center.X + x > TilesPerSide ||
                        center.Z + z < 0 || center.Z + z > TilesPerSide)
                        continue;

                    TerrainPoint point = new(center.X + x, center.Z + z);
                    int randomStep = random.Next(0, 2);
                    ChangePointHeight(point, false, m_WaterLevel + (randomStep * m_StepHeight));

                    newHeights.Add((point.X, point.Z), m_WaterLevel);
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
                        if (Mathf.Abs(neighbor.Height - point.Height) > m_StepHeight)
                        {
                            int x = point.X, z = point.Z;

                            if (point.X == center.X - distance)
                                x++;

                            if (point.X == center.X + distance)
                                x--;

                            if (point.Z == center.Z - distance)
                                z++;

                            if (point.Z == center.Z + distance)
                                z--;

                            if ((x, z) == (point.X, point.Z))
                                continue;

                            ChangePointHeight(point, false, newHeights[(x, z)] + m_StepHeight);
                            changedHeightsInIter++;
                            break;
                        }
                    }
                    newHeights.Add((point.X, point.Z), point.Height);
                }
            }

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
            {
                TerrainChunk chunk = GetChunkByIndex(chunkIndex);
                chunk.RecalculateAllTileCenterHeights();
                chunk.SetMesh();
            }

            // add one point to the left to also affect the tile the bottom left point is the bottom right corner of
            (TerrainPoint bottomLeft, TerrainPoint topRight) modifiedAreaEdges = (
                new(Mathf.Clamp(m_ModifiedAreaCorners.bottomX - 1, 0, m_ModifiedAreaCorners.bottomX),
                    Mathf.Clamp(m_ModifiedAreaCorners.bottomZ - 1, 0, m_ModifiedAreaCorners.bottomZ)),
                new(m_ModifiedAreaCorners.topX, m_ModifiedAreaCorners.topZ)
            );

            m_ModifiedChunks = new();
            m_ModifiedAreaCorners.bottomX = m_ModifiedAreaCorners.bottomZ = TilesPerSide;
            m_ModifiedAreaCorners.topX = m_ModifiedAreaCorners.topZ = 0;

            return modifiedAreaEdges;
        }

        /// <summary>
        /// Elevates the terrain within the given number of tiles of the given center point to form a steep mountain.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the center of the elevation.</param>
        /// <param name="radius">The number of tiles in each direction that the elevation should affect.</param>
        public (TerrainPoint, TerrainPoint) CauseVolcano(TerrainPoint center, int radius)
        {
            int maxHeightOnEdge = -1;

            foreach (TerrainPoint point in GetPointsAtDistance(center, radius))
                if (point.Height > maxHeightOnEdge)
                    maxHeightOnEdge = point.Height;

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
                        if (Mathf.Abs(neighbor.Height - point.Height) > m_StepHeight)
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
                chunk.RecalculateAllTileCenterHeights();
                chunk.SetMesh();
            }

            // add one point to the left to also affect the tile the bottom left point is the bottom right corner of
            (TerrainPoint bottomLeft, TerrainPoint topRight) modifiedAreaEdges = (
                new(Mathf.Clamp(m_ModifiedAreaCorners.bottomX - 1, 0, m_ModifiedAreaCorners.bottomX),
                    Mathf.Clamp(m_ModifiedAreaCorners.bottomZ - 1, 0, m_ModifiedAreaCorners.bottomZ)),
                new(m_ModifiedAreaCorners.topX, m_ModifiedAreaCorners.topZ)
            );

            m_ModifiedChunks = new();
            m_ModifiedAreaCorners.bottomX = m_ModifiedAreaCorners.bottomZ = TilesPerSide;
            m_ModifiedAreaCorners.topX = m_ModifiedAreaCorners.topZ = 0;

            return modifiedAreaEdges;
        }




        /// <summary>
        /// Increases the water level by one step.
        /// </summary>
        public void RaiseWaterLevel()
        {
            m_WaterLevel += StepHeight;
            m_TerrainMaterial.SetInteger("waterLevel", m_WaterLevel);
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
                int targetZ = center.Z + z;
                if (targetZ < 0 || targetZ > TilesPerSide)
                    continue;

                int[] xs;

                if (z == -distance || z == distance)
                    xs = Enumerable.Range(-distance, 2 * distance + 1).ToArray();
                else
                    xs = new[] { -distance, distance };

                foreach (int x in xs)
                {
                    int targetX = center.X + x;
                    if (targetX < 0 || targetX > TilesPerSide)
                        continue;

                    yield return new TerrainPoint(targetX, targetZ);
                }
            }
        }



        #endregion


        /// <summary>
        /// Checks whether all the points at the corners of the given tile are at the same height and whether they are above the water level.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is flat and not underwater, false otherwise.</returns>
        public bool IsTileFlat((int x, int z) tile)
        {
            return true;
            //(int x, int z) inChunk = GetPointInChunk(tile);

            //int height = GetMeshHeightAtPoint(inChunk);

            //for (int z = 0; z <= 1; ++z)
            //    for (int x = 0; x <= 1; ++x)
            //        if (height != GetMeshHeightAtPoint((inChunk.x + x, inChunk.z + z)))
            //            return false;

            //return height != Terrain.Instance.WaterLevel;
        }










        #region Chunk Info

        /// <summary>
        /// Checks whether the given chunk index is within the bounds of the terrain.
        /// </summary>
        /// <param name="chunkIndex">The index of the chunk which is to be tested.</param>
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
        /// <returns>A tuple representing the index of the chunk</returns>
        public (int x, int z) GetChunkIndex(Vector3 position)
            => (position.x == UnitsPerSide ? ChunksPerSide - 1 : (int)position.x / UnitsPerChunkSide,
                position.z == UnitsPerSide ? ChunksPerSide - 1 : (int)position.z / UnitsPerChunkSide);

        /// <summary>
        /// Gets the index of the chunk that the current point is contained in.
        /// </summary>
        /// <param name="point">A tuple representing the index of the current point in the terrain mesh.</param>
        /// <returns>A tuple representing the (x, z) index of the chunk.</returns>
        public (int x, int z) GetChunkIndex((int x, int z) point)
            => (point.x == TilesPerSide ? ChunksPerSide - 1 : point.x / TilesPerChunkSide,
                point.z == TilesPerSide ? ChunksPerSide - 1 : point.z / TilesPerChunkSide);


        /// <summary>
        /// Gets all the chunks which share the given point.
        /// </summary>
        /// <returns>A list of (x, z) indices of the chunks that contain this point.</returns>
        private List<(int x, int z)> GetAllTouchingChunks(TerrainPoint point)
        {
            (int x, int z) mainChunk = GetChunkIndex((point.X, point.Z));

            List<(int x, int z)> chunks = new() { mainChunk };

            (int x, int z) pointInChunk = GetChunkByIndex(mainChunk).GetPointChunkCoordinates(point);

            // bottom left
            if (pointInChunk.x == 0 && mainChunk.x > 0 && pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x - 1, mainChunk.z - 1));

            // bottom right
            if (pointInChunk.x == m_TilesPerChunkSide && mainChunk.x < m_ChunksPerSide - 1 && pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x + 1, mainChunk.z - 1));

            // top left
            if (pointInChunk.x == 0 && mainChunk.x > 0 && pointInChunk.z == m_TilesPerChunkSide && mainChunk.z < m_ChunksPerSide - 1)
                chunks.Add((mainChunk.x - 1, mainChunk.z + 1));

            if (pointInChunk.x == m_TilesPerChunkSide && mainChunk.x < m_ChunksPerSide - 1 &&
                pointInChunk.z == m_TilesPerChunkSide && mainChunk.z < m_ChunksPerSide - 1)
                chunks.Add((mainChunk.x + 1, mainChunk.z + 1));

            // left
            if (pointInChunk.x == 0 && mainChunk.x > 0)
                chunks.Add((mainChunk.x - 1, mainChunk.z));

            // right
            if (pointInChunk.x == m_TilesPerChunkSide && mainChunk.x < m_ChunksPerSide - 1)
                chunks.Add((mainChunk.x + 1, mainChunk.z));

            // bottom
            if (pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x, mainChunk.z - 1));

            // top
            if (pointInChunk.z == m_TilesPerChunkSide && mainChunk.z < m_ChunksPerSide - 1)
                chunks.Add((mainChunk.x, mainChunk.z + 1));

            return chunks;
        }


        #endregion



        #region Point Info


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
        public int GetPointHeight((int x, int z) chunkIndex, (int x, int z) point) => GetChunkByIndex(chunkIndex).GetPointHeight(new(point.x, point.z));

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
        public int GetTileCenterHeight((int x, int z) chunkIndex, (int x, int z) tile) => GetChunkByIndex(chunkIndex).GetTileCenterHeight(new(tile.x, tile.z));



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
        /// Checks whether all the corner points of the given tile are on or below the water level.
        /// </summary>
        /// <param name="tile">The (x, z) coordinates of the tile which should be checked.</param>
        /// <returns>True if the tile is underwater, false otherwise.</returns>
        public bool IsTileUnderwater((int x, int z) tile)
        {
            // check the center


            foreach (TerrainPoint point in GetTileCorners(tile))
                if (point.Height > m_WaterLevel)
                    return false;

            return true;
        }

        /// <summary>
        /// Checks whether when moving from one point of a tile to another, a <c>Structure</c> or water is crossed.
        /// </summary>
        /// <param name="start">A <c>TerrainPoint</c> representing the starting point.</param>
        /// <param name="end">A <c>TerrainPoint</c> representing the end point.</param>
        /// <returns>True if no <c>Structure</c> or water is crossed, false otherwise.</returns>
        public bool IsTileCornerReachable(TerrainPoint start, TerrainPoint end)
        {
            int dx = end.X - start.X;
            int dz = end.Z - start.Z;

            // we are not moving diagonally
            if (Mathf.Abs(dx) != Mathf.Abs(dz))
            {
                // we wnat to check if the unit is going into the water or just along the shore
                if (start.Height == 0 && end.Height == 0)
                {
                    // if there is water on both sides of the line the unit is travelling, then it is going into the water
                    (int x, int z)[] diagonals = new (int, int)[2];
                    if (dz == 0) diagonals = new (int, int)[2] { (end.X, end.Z - 1), (end.X, end.Z + 1) };
                    if (dx == 0) diagonals = new (int, int)[2] { (end.X - 1, end.Z), (end.X + 1, end.Z) };

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
            if (start.Height == 0 && end.Height == 0)
                return false;

            int x = start.X;
            int z = start.Z;

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

            Structure structure = StructureManager.Instance.GetStructureOnTile((x, z));
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