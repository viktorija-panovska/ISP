using System.Collections.Generic;
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
        private TerrainChunk[,] m_ChunkGrid;

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

            m_ChunkGrid = new TerrainChunk[m_ChunksPerSide, m_ChunksPerSide];
            m_TerrainCenter = new(new Vector3((float)UnitsPerSide / 2, 0, (float)UnitsPerSide / 2));

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
                for (int x = 0; x < m_ChunksPerSide; ++x)
                    m_ChunkGrid[z, x] = new TerrainChunk(x, z, transform);
        }

        /// <summary>
        /// Sets up the variables in the Terrain shader of the terrain material.
        /// </summary>
        private void SetupTerrainShader()
        {
            m_TerrainMaterial.SetInteger("stepHeight", m_StepHeight);
            m_TerrainMaterial.SetInteger("waterLevel", m_WaterLevel);
        }

        #endregion


        #region Terrain Modificiation

        /// <summary>
        /// Modifies the terrain mesh at the given point, lowering or elevating it.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> that the player has selected.</param>
        /// <param name="lower">True if the point heights should be decreased, false if they should be increased.</param>
        /// <returns></returns> A pair of <c>TerrainPoint</c>s representing the bottom left and the top right corner of an
        /// area that contains all points that were modified during this action.
        public (TerrainPoint bottomLeft, TerrainPoint topRight) ModifyTerrain(TerrainPoint point, bool lower)
        {
            ChangePointHeight(point, lower);

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
                GetChunkByIndex(chunkIndex).SetMesh();

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
        /// Changes the height of the given point either by increasing it or decreasing it, or by setting it to the given value.
        /// </summary>
        /// <param name="point">The <c>TerrainPoint</c> whose height should be altered.</param>
        /// <param name="lower">True if the point heights should be decreased, false if they should be increased.</param>
        /// <param name="height">If it has a value, the height of the point should be set to that value.</param>
        public void ChangePointHeight(TerrainPoint point, bool lower, int? height = null)
        {
            if (height.HasValue && (height.Value < m_WaterLevel || height.Value > MaxHeight))
                return;

            int currentHeight = point.GetHeight();

            if (!height.HasValue && ((lower && currentHeight <= m_WaterLevel) || (!lower && currentHeight == MaxHeight)))
                return;

            foreach ((int, int) chunkIndex in point.GetAllChunkIndices())
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
        /// <returns></returns> A pair of <c>TerrainPoint</c>s representing the bottom left and the top right corner of an
        /// area that contains all points that were modified during this action.
        public (TerrainPoint bottomLeft, TerrainPoint topRight) CauseEarthquake(TerrainPoint center, int radius, int randomizerSeed)
        {
            Random random = new(randomizerSeed);
            Dictionary<TerrainPoint, int> newHeights = new();

            // randomly decrease the points in the area
            for (int z = -radius; z <= radius; ++z)
            {
                for (int x = -radius; x <= radius; ++x)
                {
                    TerrainPoint point = new(center.X + x, center.Z + z);

                    if (!point.IsInBounds()) continue;

                    int randomStep = random.Next(0, 2);
                    ChangePointHeight(point, false, m_WaterLevel + (randomStep * m_StepHeight));
                    newHeights.Add(point, m_WaterLevel);
                }
            }

            // updating the points around the affected area in case they break the height property
            int distance = radius;
            bool changedHeightsInIter = true;
            while (changedHeightsInIter)
            {
                changedHeightsInIter = false;
                distance++;

                foreach (TerrainPoint point in center.GetAllPointsAtDistance(distance))
                {
                    foreach (TerrainPoint neighbor in point.GetAllNeighbors())
                    {
                        // the point breaks the property, so it needs an update
                        if (Mathf.Abs(neighbor.GetHeight() - point.GetHeight()) > m_StepHeight)
                        {
                            // finding a point in the previous circle to get the height from
                            int x = point.X, z = point.Z;

                            // to the left of the center
                            if (point.X <= center.X) x++;
                            // to the right of the center
                            if (point.X >= center.X) x--;
                            // below the center
                            if (point.Z <= center.Z) z++;
                            // above the center
                            if (point.Z >= center.Z) z--;

                            if ((x, z) == (point.X, point.Z)) continue;

                            ChangePointHeight(point, false, Mathf.Clamp(newHeights[new(x, z)] + m_StepHeight, m_WaterLevel, MaxHeight));
                            changedHeightsInIter = true;
                            break;
                        }
                    }
                    newHeights.Add(point, point.GetHeight());
                }
            }

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
            {
                TerrainChunk chunk = GetChunkByIndex(chunkIndex);
                chunk.RecalculateAllTileCenterHeights();
                chunk.SetMesh();
            }

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
        /// Elevates the terrain within the given number of tiles of the given center point to form a steep mountain.
        /// </summary>
        /// <param name="center">The <c>TerrainPoint</c> at the peak of the elevation.</param>
        /// <param name="radius">The number of tiles in each direction that the elevation should affect.</param>
        /// <returns></returns> A pair of <c>TerrainPoint</c>s representing the bottom left and the top right corner of an
        /// area that contains all points that were modified during this action.
        public (TerrainPoint bottomLeft, TerrainPoint topRight) CauseVolcano(TerrainPoint center, int radius)
        {
            int maxHeightOnEdge = -1;

            foreach (TerrainPoint point in center.GetAllPointsAtDistance(radius))
                if (point.GetHeight() > maxHeightOnEdge)
                    maxHeightOnEdge = point.GetHeight();

            maxHeightOnEdge += m_StepHeight;

            ChangePointHeight(center, false, Mathf.Clamp(maxHeightOnEdge + radius * m_StepHeight, m_WaterLevel, MaxHeight));

            int distance;
            for (distance = 1; distance <= radius; ++distance)
                foreach (TerrainPoint point in center.GetAllPointsAtDistance(distance))
                    ChangePointHeight(point, false, Mathf.Clamp(maxHeightOnEdge + (radius - distance) * m_StepHeight, m_WaterLevel, MaxHeight));

            // updating the points around the affected area in case they break the height property
            distance = radius;
            bool changedHeightsInIter = true;
            while (changedHeightsInIter)
            {
                changedHeightsInIter = false;
                distance++;

                foreach (TerrainPoint point in center.GetAllPointsAtDistance(distance))
                {
                    foreach (TerrainPoint neighbor in point.GetAllNeighbors())
                    {
                        if (Mathf.Abs(neighbor.GetHeight() - point.GetHeight()) > m_StepHeight)
                        {
                            ChangePointHeight(point, false, Mathf.Clamp(maxHeightOnEdge + (radius - distance) * m_StepHeight, m_WaterLevel, MaxHeight));
                            changedHeightsInIter = true;
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
        /// Increases the water level by one height step.
        /// </summary>
        public void RaiseWaterLevel()
        {
            m_WaterLevel += StepHeight;
            m_TerrainMaterial.SetInteger("waterLevel", m_WaterLevel);

            for (int z = 0; z < m_ChunksPerSide; ++z)
            {
                for (int x = 0; x < m_ChunksPerSide; ++x)
                {
                    TerrainChunk chunk = GetChunkByIndex((x, z));
                    chunk.UpdatePointsUnderwater();
                    chunk.SetMesh();
                }
            }
        }

        #endregion


        /// <summary>
        /// Gets the <c>TerrainChunk</c> at the given index of the chunk map.
        /// </summary>
        /// <param name="chunkIndex">The index of a chunk in the chunk map.</param>
        /// <returns>The <c>TerrainChunk</c> at the given index.</returns>
        public TerrainChunk GetChunkByIndex((int x, int z) chunkIndex) => m_ChunkGrid[chunkIndex.z, chunkIndex.x];

        /// <summary>
        /// Checks whether the level of the water has reached the maximum height of the terrain.
        /// </summary>
        /// <returns>True if the level of the water has reached the maximum height, false otherwise.</returns>
        public bool HasReachedMaxWaterLevel() => m_WaterLevel == MaxHeight;
    }
}