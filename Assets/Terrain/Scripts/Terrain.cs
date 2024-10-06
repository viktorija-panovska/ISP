using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Random = System.Random;


namespace Populous
{
    public struct MapPoint : IEquatable<MapPoint>, INetworkSerializable
    {
        private int m_X;
        private int m_Z;

        /// <summary>
        /// Gets the X coordinate of this <c>MapPoint</c> on the grid.
        /// </summary>
        public readonly int X { get => m_X; }
        /// <summary>
        /// Gets the Z coordinate of this <c>MapPoint</c> on the grid.
        /// </summary>
        public readonly int Z { get => m_Z; }

        /// <summary>
        /// Gets the terrain height at this <c>MapPoint</c>.
        /// </summary>
        public readonly int Y
        {
            get => m_TouchingChunks == null
                ? Terrain.Instance.GetPointHeight((m_X, m_Z))
                : Terrain.Instance.GetPointHeight(m_TouchingChunks[0], (m_X, m_Z));
        }

        private List<(int x, int z)> m_TouchingChunks;
        /// <summary>
        /// Gets the list of chunks which this <c>MapPoint</c> is a part of if it is available, creates it then returns it otherwise.
        /// </summary>
        public List<(int x, int z)> TouchingChunks
        {
            get
            {
                m_TouchingChunks ??= Terrain.Instance.GetAllTouchingChunks((m_X, m_Z));
                return m_TouchingChunks;
            }
        }

        private List<(int x, int z)> m_Neighbors;
        /// <summary>
        /// Gets the list of neighboring points of this <c>MapPoint</c> if it is available, creates it then returns it otherwise.
        /// </summary>
        public List<(int x, int z)> Neighbors
        {
            get
            {
                m_Neighbors ??= Terrain.Instance.GetAllPointNeighbors((m_X, m_Z));
                return m_Neighbors;
            }
        }


        #region Constructors

        /// <summary>
        /// A constructor for <c>MapPoint</c>, to be used when the coordinates of the point on the grid need to be computed from a world position.
        /// </summary>
        /// <param name="x">The x coordinate of a world position.</param>
        /// <param name="z">The y coordinate of a world position.</param>
        public MapPoint(float x, float z)
            : this(Mathf.RoundToInt(x / Terrain.Instance.UnitsPerTileSide), Mathf.RoundToInt(z / Terrain.Instance.UnitsPerTileSide)) { }

        /// <summary>
        /// A constructor for <c>MapPoint</c>, to be used when the coordinates of the point on the grid are already computed.
        /// </summary>
        /// <param name="x">The x coordinate of the point on the grid.</param>
        /// <param name="z">The z coordinate of the point on the grid.</param>
        public MapPoint(int x, int z)
        {
            m_X = x;
            m_Z = z;

            m_TouchingChunks = null;
            m_Neighbors = null;
        }

        #endregion


        #region Conversion

        /// <summary>
        /// Gets the <c>MapPoint</c> coordinates as a <c>Vector3</c>
        /// </summary>
        /// <returns>A <c>Vector3</c> of the coordinates.</returns>
        public readonly Vector3 ToVector3() => new(m_X, Y, m_Z);

        /// <summary>
        /// Gets a string representation of the <c>MapPoint</c>.
        /// </summary>
        /// <returns>A <c>string</c> representation of the <c>MapPoint</c>.</returns>
        public override readonly string ToString() => $"MapPoint -> {ToVector3()}";

        #endregion


        #region Equality

        /// <summary>
        /// Tests whether two <c>MapPoint</c>s are equal to each other.
        /// </summary>
        /// <param name="a">The first <c>MapPoint</c> operand.</param>
        /// <param name="b">The second <c>MapPoint</c> operand.</param>
        /// <returns>True if the two operands are equal to each other, false otherwise.</returns>
        public static bool operator ==(MapPoint a, MapPoint b) => a.Equals(b);

        /// <summary>
        /// Tests whether two <c>MapPoint</c>s are not equal to each other.
        /// </summary>
        /// <param name="a">The first <c>MapPoint</c> operand.</param>
        /// <param name="b">The second <c>MapPoint</c> operand.</param>
        /// <returns>True if the two operands are not equal to each other, false otherwise.</returns>
        public static bool operator !=(MapPoint a, MapPoint b) => !a.Equals(b);

        /// <summary>
        /// Tests whether an object is equal to this <c>MapPoint</c>.
        /// </summary>
        /// <param name="obj">The object which is being compared against this <c>MapPoint</c>.</param>
        /// <returns>True if the object is a <c>MapPoint</c> and is equal to this <c>MapPoint</c>, false otherwise.</returns>
        public override readonly bool Equals(object obj) => obj.GetType() == typeof(MapPoint) && Equals((MapPoint)obj);

        #endregion


        #region Interface members

        /// <summary>
        /// Tests whether a <c>MapPoint</c> is equal to this <c>MapPoint</c>.
        /// </summary>
        /// <remarks>Interface member of <c>IEquitable</c>.</remarks>
        /// <param name="other">The <c>MapPoint</c> instance which is being compared against this <c>MapPoint</c>.</param>
        /// <returns>True if the coordinates of the <c>MapPoint</c>s are equal, false otherwise.</returns>
        public readonly bool Equals(MapPoint other) => m_X == other.X && m_Z == other.Z;

        /// <summary>
        /// Gets the hash code for the current <c>MapPoint</c>.
        /// </summary>
        /// <remarks>Interface member of <c>IEquitable</c>.</remarks>
        /// <returns>The integer hash code of the current <c>MapPoint</c>.</returns>
        public override readonly int GetHashCode() => base.GetHashCode();

        /// <summary>
        /// Serializes the <c>MapPoint</c> coordinates.
        /// </summary>
        /// <remarks>Interface member of <c>INetworkSerializable</c>.</remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer">The <c>BufferSerializer</c> to be used in the serialization.</param>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref m_X);
            serializer.SerializeValue(ref m_Z);
        }

        #endregion
    }


    public class Terrain : MonoBehaviour
    {
        [Header("Terrain Properties")]
        [SerializeField] private int m_ChunksPerSide = 2;
        [SerializeField] private int m_TilesPerChunkSide = 5;
        [SerializeField] private int m_UnitsPerTileSide = 50;
        [SerializeField] private int m_MaxHeightSteps = 7;
        [SerializeField] private int m_StepHeight = 20;

        [Header("Terrain Material")]
        [SerializeField] private Material m_TerrainMaterial;

        [Header("Trees and Rocks Properties")]
        [SerializeField, Range(0, 1)] private float m_TreeDensity;
        [SerializeField, Range(0, 1)] private float m_WhiteRockDensity;
        [SerializeField, Range(0, 1)] private float m_BlackRockDensity;
        [SerializeField] private GameObject m_TreePrefab;
        [SerializeField] private GameObject m_WhiteRockPrefab;
        [SerializeField] private GameObject m_BlackRockPrefab;


        private static Terrain m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static Terrain Instance { get => m_Instance; }

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

        private int m_WaterLevel;
        /// <summary>
        /// Gets the current height of the water.
        /// </summary>
        public int WaterLevel { get => m_WaterLevel; }

        private TerrainChunk[,] m_ChunkMap;
        private HashSet<(int, int)> m_ModifiedChunks;



        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        private void Start()
        {
            m_ChunkMap = new TerrainChunk[m_ChunksPerSide, m_ChunksPerSide];

            HeightMapGenerator.Initialize(GameData.Instance == null ? 0 : GameData.Instance.MapSeed);
            GenerateTerrain();
            GenerateTexture();

            Frame.Instance.SetupFrame();
            Water.Instance.SetupWater();
            CameraController.Instance.UpdateVisibleTerrainChunks();

            //PlaceTreesAndRocks(m_TreeDensity, m_WhiteRockDensity, m_BlackRockDensity);
        }

        #endregion



        #region Terrain Generation

        private void GenerateTerrain()
        {
            for (int z = 0; z < m_ChunksPerSide; ++z)
                for (int x = 0; x < m_ChunksPerSide; ++x)
                    m_ChunkMap[z, x] = new(x, z, gameObject.transform);
        }

        private void GenerateTexture()
        {
            m_TerrainMaterial.SetFloat("minHeight", 0);
            m_TerrainMaterial.SetFloat("maxHeight", MaxHeight);
            m_TerrainMaterial.SetInt("waterLevel", m_WaterLevel);
            m_TerrainMaterial.SetInt("stepHeight", m_StepHeight);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="treeDensity"></param>
        /// <param name="whiteRockDensity"></param>
        /// <param name="blackRockDensity"></param>
        public void PlaceTreesAndRocks(float treeDensity, float whiteRockDensity, float blackRockDensity)
        {
            if (!GameController.Instance.IsPlayerHosting) return;

            Random random = new(GameData.Instance == null ? 0 : GameData.Instance.MapSeed);

            for (int z = 0; z < TilesPerSide; ++z)
            {
                for (int x = 0; x < TilesPerSide; ++x)
                {
                    if (IsTileOccupied((x, z)) || IsTileUnderwater((x, z)))
                        continue;

                    List<MapPoint> occupiedPoints = GetTilePoints((x, z));

                    double randomValue = random.NextDouble();

                    if (randomValue < whiteRockDensity)
                        GameController.Instance.SpawnStructure(m_WhiteRockPrefab, (x, z), occupiedPoints);
                    else if (randomValue < blackRockDensity)
                        GameController.Instance.SpawnStructure(m_BlackRockPrefab, (x, z), occupiedPoints);
                    else if (randomValue < treeDensity)
                        GameController.Instance.SpawnStructure(m_TreePrefab, (x, z), occupiedPoints);
                }
            }
        }

        #endregion



        #region Terrain Modificiation

        /// <summary>
        /// Calls the <see cref="ChangePointHeight(MapPoint, bool)"/> to modify the terrain, 
        /// then resets the meshes of all the chunks that have been modified..
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> which should be modified.</param>
        /// <param name="lower">Whether the point should be lowered or elevated.</param>
        public void ModifyTerrain(MapPoint point, bool lower)
        {
            m_ModifiedChunks = new();
            ChangePointHeight(point, lower);

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
                GetChunkByIndex(chunkIndex).SetMesh();

            m_ModifiedChunks = new();
        }

        /// <summary>
        /// Elevates or lowers the given point on the terrain in all the chunks the point belongs to.
        /// </summary>
        /// <param name="point">The <c>MapPoint</c> which should be modified.</param>
        /// <param name="lower">Whether the point should be lowered or elevated.</param>
        public void ChangePointHeight(MapPoint point, bool lower)
        {
            foreach ((int, int) chunkIndex in point.TouchingChunks)
            {
                if (!m_ModifiedChunks.Contains(chunkIndex))
                    m_ModifiedChunks.Add(chunkIndex);

                GetChunkByIndex(chunkIndex).ChangeHeights(point, lower);
            }
        }

        /// <summary>
        /// Increases the water level by one step and updates the shader.
        /// </summary>
        public void RaiseWaterLevel()
        {
            m_WaterLevel += StepHeight;
            m_TerrainMaterial.SetInt("waterLevel", m_WaterLevel);
        }


        private IEnumerable<MapPoint> GetPointsAtDistance(MapPoint center, int distance)
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

                    yield return new MapPoint(targetX, targetZ);
                }
            }
        }


        public void CauseEarthquake(MapPoint center, int radius, int randomizerSeed)
        {
            m_ModifiedChunks = new();
            Random random = new(randomizerSeed);

            foreach (MapPoint point in GetPointsAtDistance(center, radius))
            {
                int randomStep = random.Next(0, 2);
                int steps = Mathf.Abs((randomStep * m_StepHeight) - point.Y) / m_StepHeight;
                for (int i = 0; i < steps; ++i)
                    ChangePointHeight(point, lower: point.Y - (randomStep * m_StepHeight) > 0);
            }

            for (int distance = radius - 1; distance >= 0; --distance)
                foreach (MapPoint point in GetPointsAtDistance(center, distance))
                    SetVertexHeight(point, m_WaterLevel);

            for (int distance = radius - 1; distance >= 0; --distance)
                foreach (MapPoint point in GetPointsAtDistance(center, distance))
                    if (random.Next(0, 2) == 1)
                        ChangePointHeight(point, lower: false);

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
            {
                TerrainChunk chunk = GetChunkByIndex(chunkIndex);
                chunk.RecomputeAllCenters();
                chunk.SetMesh();
            }

            GameController.Instance.OnFlood?.Invoke();
            m_ModifiedChunks = new();
        }

        public void CauseVolcano(MapPoint center, int radius)
        {
            m_ModifiedChunks = new();

            int maxHeightOnEdge = -1;

            foreach (MapPoint point in GetPointsAtDistance(center, radius))
                if (point.Y > maxHeightOnEdge)
                    maxHeightOnEdge = point.Y;

            maxHeightOnEdge += m_StepHeight;

            foreach (MapPoint point in GetPointsAtDistance(center, radius))
            {
                int steps = Mathf.Abs(maxHeightOnEdge - point.Y) / m_StepHeight;
                for (int i = 0; i < steps; ++i)
                    ChangePointHeight(point, lower: false);
            }

            for (int distance = radius - 1; distance >= 0; --distance)
                foreach (MapPoint point in GetPointsAtDistance(center, distance))
                    SetVertexHeight(point, maxHeightOnEdge + (radius - distance) * m_StepHeight);

            foreach ((int, int) chunkIndex in m_ModifiedChunks)
            {
                TerrainChunk chunk = GetChunkByIndex(chunkIndex);
                chunk.RecomputeAllCenters();
                chunk.SetMesh();
            }

            GameController.Instance.OnFlood?.Invoke();
            m_ModifiedChunks = new();
        }

        #endregion



        #region Getters

        /// <summary>
        /// Gets the <c>TerrainChunk</c> at the given index in the chunk map.
        /// </summary>
        /// <param name="chunkIndex">An (x,z) index in the chunk map.</param>
        /// <returns>The <c>TerrainChunk</c> at the given index.</returns>
        public TerrainChunk GetChunkByIndex((int x, int z) chunkIndex)
            => m_ChunkMap[chunkIndex.z, chunkIndex.x];

        /// <summary>
        /// Gets the index of the chunk that the current position is contained in.
        /// </summary>
        /// <param name="position">A <c>Vector3</c> representing the current position.</param>
        /// <returns>A tuple representing the (x,z) index of the chunk</returns>
        public (int x, int z) GetChunkIndex(Vector3 position)
            => (position.x == UnitsPerSide ? ChunksPerSide - 1 : (int)position.x / UnitsPerChunkSide,
                position.z == UnitsPerSide ? ChunksPerSide - 1 : (int)position.z / UnitsPerChunkSide);

        /// <summary>
        /// Gets the index of the chunk that the current point is contained in.
        /// </summary>
        /// <param name="point">A tuple representing the (x,z) index of the current point in the terrain mesh.</param>
        /// <returns>A tuple representing the (x,z) index of the chunk.</returns>
        public (int x, int z) GetChunkIndex((int x, int z) point)
            => (point.x == TilesPerSide ? ChunksPerSide - 1 : point.x / TilesPerChunkSide,
                point.z == TilesPerSide ? ChunksPerSide - 1 : point.z / TilesPerChunkSide);

        /// <summary>
        /// The height of the given point.
        /// </summary>
        /// <param name="point">The (x,z) index of the given point in the terrain mesh.</param>
        /// <returns>The height of the terrain at the given point.</returns>
        public int GetPointHeight((int x, int z) point)
            => GetPointHeight(GetChunkIndex(point), point);

        /// <summary>
        /// The height of the given point in the given chunk.
        /// </summary>
        /// <param name="chunkIndex">The (x,z) index of the given chunk.</param>
        /// <param name="point">The (x,z) index of the given point in the terrain mesh.</param>
        /// <returns>The height of the terrain at the given point.</returns>
        public int GetPointHeight((int x, int z) chunkIndex, (int x, int z) point)
            => GetChunkByIndex(chunkIndex).GetVertexHeight(point);

        /// <summary>
        /// The height of the center of the given tile.
        /// </summary>
        /// <param name="tile">The (x,z) index of the given tile in the terrain mesh.</param>
        /// <returns>The height of the terrain at the center of the given tile.</returns>
        public int GetTileCenterHeight((int x, int z) tile)
            => GetTileCenterHeight(GetChunkIndex(tile), tile);

        /// <summary>
        /// The height of the center of the given tile in the given chunk.
        /// </summary>
        /// <param name="chunkIndex">The (x,z) index of the given chunk.</param>
        /// <param name="tile">The (x,z) index of the given tile in the terrain mesh.</param>
        /// <returns>The height of the terrain at the given point.</returns>
        public int GetTileCenterHeight((int x, int z) chunkIndex, (int x, int z) tile)
            => GetChunkByIndex(chunkIndex).GetTileCenterHeight(tile);


        /// <summary>
        /// Gets all the chunks that the given point belongs to.
        /// </summary>
        /// <param name="point">The (x, z) index of a point in the terrain mesh.</param>
        /// <returns>A list of (x,z) indices of chunks that contain the given point.</returns>
        public List<(int x, int z)> GetAllTouchingChunks((int x, int z) point)
        {
            (int x, int z) mainChunk = GetChunkIndex(point);

            List<(int x, int z)> chunks = new() { mainChunk };

            (int x, int z) pointInChunk = GetChunkByIndex(mainChunk).GetPointInChunk(point);

            // bottom left
            if (pointInChunk.x == 0 && mainChunk.x > 0 && pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x - 1, mainChunk.z - 1));

            // bottom right
            if (pointInChunk.x == TilesPerChunkSide && mainChunk.x < ChunksPerSide - 1 && pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x + 1, mainChunk.z - 1));

            // top left
            if (pointInChunk.x == 0 && mainChunk.x > 0 && pointInChunk.z == TilesPerChunkSide && mainChunk.z < ChunksPerSide - 1)
                chunks.Add((mainChunk.x - 1, mainChunk.z + 1));

            if (pointInChunk.x == TilesPerChunkSide && mainChunk.x < ChunksPerSide - 1 &&
                pointInChunk.z == TilesPerChunkSide && mainChunk.z < ChunksPerSide - 1)
                chunks.Add((mainChunk.x + 1, mainChunk.z + 1));

            // left
            if (pointInChunk.x == 0 && mainChunk.x > 0)
                chunks.Add((mainChunk.x - 1, mainChunk.z));

            // right
            if (pointInChunk.x == TilesPerChunkSide && mainChunk.x < ChunksPerSide - 1)
                chunks.Add((mainChunk.x + 1, mainChunk.z));

            // bottom
            if (pointInChunk.z == 0 && mainChunk.z > 0)
                chunks.Add((mainChunk.x, mainChunk.z - 1));

            // top
            if (pointInChunk.z == Terrain.Instance.TilesPerChunkSide && mainChunk.z < Terrain.Instance.ChunksPerSide - 1)
                chunks.Add((mainChunk.x, mainChunk.z + 1));

            return chunks;
        }

        /// <summary>
        /// Gets all the point which neighbor the given point.
        /// </summary>
        /// <param name="point">The (x, z) index of a point in the terrain mesh.</param>
        /// <returns>A list of (x,z) indices of points that neighbor the given point.</returns>
        public List<(int x, int z)> GetAllPointNeighbors((int x, int z) point)
        {
            List<(int x, int z)> neighbors = new();

            for (int z = -1; z <= 1; ++z)
            {
                for (int x = -1; x <= 1; ++x)
                {
                    if ((x, z) == (0, 0)) continue;

                    if (IsPointInBounds((point.x + x, point.z + z)))
                        neighbors.Add((point.x + x, point.z + z));
                }
            }

            return neighbors;
        }

        public List<MapPoint> GetTilePoints((int x, int z) tile)
            => new() { new(tile.x, tile.z), new(tile.x + 1, tile.z), new(tile.x, tile.z + 1), new(tile.x + 1, tile.z + 1) };


        public Structure GetStructureOccupyingTile((int x, int z) tile)
            => GetChunkByIndex(GetChunkIndex(tile)).GetStructureOccupyingTile(tile.x, tile.z);

        public Structure GetStructureOccupyingTile((int x, int z) chunk, (int x, int z) tile)
            => GetChunkByIndex(chunk).GetStructureOccupyingTile(tile.x, tile.z);

        #endregion


        #region Setters

        /// <summary>
        /// Sets the status of a given tile to being occupied or not occupied by some structure.
        /// </summary>
        /// <param name="tile">The coordinates of the tile.</param>
        /// <param name="structure"></param>
        public void SetOccupiedTile((int x, int z) tile, Structure structure)
        {
            TerrainChunk chunk = GetChunkByIndex(GetChunkIndex(tile));
            chunk.SetOccupiedTile(tile.x, tile.z, structure);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="point"></param>
        /// <param name="height"></param>
        public void SetVertexHeight(MapPoint point, int height)
        {
            foreach ((int, int) chunkIndex in point.TouchingChunks)
            {
                if (!m_ModifiedChunks.Contains(chunkIndex))
                    m_ModifiedChunks.Add(chunkIndex);

                GetChunkByIndex(chunkIndex).SetVertexHeight(point, height);
            }
        }

        #endregion


        #region Checkers

        /// <summary>
        /// Tests whether the given chunk index corresponds to an existing chunk.
        /// </summary>
        /// <param name="chunkIndex">The index that is to be tested.</param>
        /// <returns>True if the chunk index is in bounds, false otherwise</returns>
        public bool IsChunkInBounds((int x, int z) chunkIndex)
            => chunkIndex.x >= 0 && chunkIndex.z >= 0 && chunkIndex.x < ChunksPerSide && chunkIndex.z < ChunksPerSide;

        /// <summary>
        /// Tests whether the given point is inside the terrain.
        /// </summary>
        /// <param name="point">The point coordinates to be tested.</param>
        /// <returns>True if the point is in bounds, false otherwise</returns>
        public bool IsPointInBounds((int x, int z) point)
            => point.x >= 0 && point.x <= TilesPerSide && point.z >= 0 && point.z <= TilesPerSide;

        /// <summary>
        /// Tests whether the given tile is occupied by some structure or not.
        /// </summary>
        /// <param name="tile">The coordinates of the tile.</param>
        /// <returns>True if there is a structure occupying the tile, false otherwise.</returns>
        public bool IsTileOccupied((int x, int z) tile)
            => GetChunkByIndex(GetChunkIndex(tile)).IsTileOccupied(tile.x, tile.z);

        /// <summary>
        /// Tests whether the given tile is occupied by some structure or not.
        /// </summary>
        /// <param name="chunk">The index of the chunk the tile is in.</param>
        /// <param name="tile">The coordinates of the tile.</param>
        /// <returns>True if there is a structure occupying the tile, false otherwise.</returns>
        public bool IsTileOccupied((int x, int z) chunk, (int x, int z) tile)
            => GetChunkByIndex(chunk).IsTileOccupied(tile.x, tile.z);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        public bool IsTileUnderwater((int x, int z) tile)
        {
            foreach (MapPoint point in GetTilePoints(tile))
                if (point.Y > m_WaterLevel)
                    return false;

            return true;
        }

        /// <summary>
        /// Tests whether the given tile is flat or not.
        /// </summary>
        /// <param name="tile">The coordinates of the tile.</param>
        /// <returns>True if all the points on the corners of the given tile are at the same height, false otherwise.</returns>
        public bool IsTileFlat((int x, int z) tile)
            => GetChunkByIndex(GetChunkIndex(tile)).IsTileFlat(tile);

        /// <summary>
        /// Tests whether the given tile is flat or not.
        /// </summary>
        /// <param name="chunk">The index of the chunk the tile is in.</param>
        /// <param name="tile">The coordinates of the tile.</param>
        /// <returns>True if all the points on the corners of the given tile are at the same height, false otherwise.</returns>
        public bool IsTileFlat((int x, int z) chunk, (int x, int z) tile)
            => GetChunkByIndex(chunk).IsTileFlat(tile);

        #endregion
    }
}