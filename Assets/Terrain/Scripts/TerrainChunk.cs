using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

namespace Populous
{
    public class TerrainChunk
    {
        private readonly struct MeshData
        {
            private readonly Vector3[] m_Vertices;
            /// <summary>
            /// Contains the positions of the vertices of the mesh.
            /// </summary>
            public readonly Vector3[] Vertices { get => m_Vertices; }

            private readonly int[] m_Triangles;
            /// <summary>
            /// Contains the indices of the vertices included in each triangle.
            /// </summary>
            /// <remarks>Each triple of indices represents one triangle. The vertices are entered in order left-center-right.</remarks>
            public readonly int[] Triangles { get => m_Triangles; }

            /// <summary>
            /// A constructor for <c>MeshData</c>
            /// </summary>
            /// <param name="sideLength">Number of vertices on one side of the mesh.</param>
            public MeshData(int sideLength)
            {
                m_Vertices = new Vector3[sideLength * sideLength];
                m_Triangles = new int[(sideLength - 1) * (sideLength - 1) * 6];
            }

            /// <summary>
            /// Adds a new vertex at the given index in the vertices array.
            /// </summary>
            /// <param name="index">The index in the vertices array at which the new vertex should be added.</param>
            /// <param name="vertex">A <c>Vector3</c> representing the vertex.</param>
            public readonly void AddVertex(int index, Vector3 vertex)
            {
                m_Vertices[index] = vertex;
            }

            /// <summary>
            /// Adds a new triangle starting at the given index in the triangles array.
            /// </summary>
            /// <param name="index">The index in the triangles array at which the first vertex index should be added.</param>
            /// <param name="a">The index of the first vertex in the triangle.</param>
            /// <param name="b">The index of the second vertex in the triangle.</param>
            /// <param name="c">The index of the third vertex in the triangle.</param>
            public readonly void AddTriangle(int index, int a, int b, int c)
            {
                m_Triangles[index] = a;
                m_Triangles[index + 1] = b;
                m_Triangles[index + 2] = c;
            }
        }

        private readonly GameObject m_ChunkObject;
        private readonly MeshData m_MeshData;
        private readonly (int x, int z) m_ChunkIndex;

        /// <summary>
        /// Gets the global position of the chunk.
        /// </summary>
        public Vector3 ChunkPosition { get => m_ChunkObject.transform.position; }
        /// <summary>
        /// Gets the index of the chunk in the chunk map array.
        /// </summary>
        public (int X, int Z) ChunkIndex { get => m_ChunkIndex; }

        private readonly (float x, float z)[] m_VertexOffsets = new (float x, float z)[]
        {
        (0, 0), (1, 0), (1, 1), (0, 1), (0.5f, 0.5f)
        };
        private readonly int[] m_TriangleIndices = new int[]
        {
        0, 4, 1,    // bottom left, center, bottom right
        1, 4, 2,    // bottom right, center, top right
        2, 4, 3,    // top right, center, top left
        3, 4, 0     // top left, center, bottom left
        };
        private readonly List<int>[] m_SharedVertexOffsets = new List<int>[5]
        {
        new() { 0, 11 },        // bottom left
        new() { 2, 3  },        // bottom right
        new() { 8, 9 },         // top left
        new() { 5, 6 },         // top right
        new() { 1, 4, 7, 10 }   // center
        };

        private Structure[,] m_IsTileOccupied = new Structure[Terrain.Instance.TilesPerChunkSide, Terrain.Instance.TilesPerChunkSide];


        /// <summary>
        /// A constructor for the <c>TerrainChunk</c>.
        /// </summary>
        /// <param name="mapPositionX">The x index of the chunk in the chunk map array.</param>
        /// <param name="mapPositionZ">The z index of the chunk in the chunk map array.</param>
        /// <param name="parentTransform">The transform of the Terrain object.</param>
        public TerrainChunk(int mapPositionX, int mapPositionZ, Transform parentTransform)
        {
            m_ChunkObject = new GameObject();
            m_ChunkObject.AddComponent<MeshFilter>();
            m_ChunkObject.AddComponent<MeshRenderer>();
            m_ChunkObject.AddComponent<MeshCollider>();

            m_ChunkIndex = (mapPositionX, mapPositionZ);

            m_ChunkObject.transform.position = new Vector3(
                ChunkIndex.X * Terrain.Instance.UnitsPerChunkSide,
                0,
                ChunkIndex.Z * Terrain.Instance.UnitsPerChunkSide
            );
            m_ChunkObject.transform.SetParent(parentTransform);
            m_ChunkObject.name = "Chunk " + ChunkIndex.X + " " + ChunkIndex.Z;

            SetVisibility(false);
            m_MeshData = GenerateMeshData();
            //SetVertexHeights();
            SetMesh();
        }


        #region Generate Terrain Chunk

        /// <summary>
        /// Creates a new mesh from the <c>MeshData</c> and applies it.
        /// </summary>
        public void SetMesh()
        {
            Mesh mesh = new()
            {
                vertices = m_MeshData.Vertices,
                triangles = m_MeshData.Triangles,
            };

            mesh.RecalculateNormals();
            m_ChunkObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            m_ChunkObject.GetComponent<MeshRenderer>().sharedMaterial = Terrain.Instance.TerrainMaterial;
            m_ChunkObject.GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        private MeshData GenerateMeshData()
        {
            MeshData meshData = new(Terrain.Instance.UnitsPerChunkSide);
            int vertexIndex = 0;
            int triangleIndex = 0;

            // Generating the mesh tile by tile
            for (int z = 0; z < Terrain.Instance.TilesPerChunkSide; ++z)
            {
                for (int x = 0; x < Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    for (int i = 0; i < m_TriangleIndices.Length; ++i)
                    {
                        int index = m_TriangleIndices[i];

                        Vector3 vertex = new(
                            (x + m_VertexOffsets[index].x) * Terrain.Instance.UnitsPerTileSide,
                            60,
                            (z + m_VertexOffsets[index].z) * Terrain.Instance.UnitsPerTileSide
                        );
                        meshData.AddVertex(vertexIndex + i, vertex);

                        // After every third vertex, add a triangle
                        if ((i + 1) % 3 == 0)
                        {
                            meshData.AddTriangle(triangleIndex, vertexIndex + i - 2, vertexIndex + i - 1, vertexIndex + i);
                            triangleIndex += 3;
                        }
                    }
                    vertexIndex += m_TriangleIndices.Length;
                }
            }

            return meshData;
        }

        private void SetVertexHeights()
        {
            if (ChunkIndex.X > 0)
            {
                for (int z = 0; z <= Terrain.Instance.TilesPerChunkSide; ++z)
                {
                    int height = Terrain.Instance.GetPointHeight(
                        (ChunkIndex.X - 1, ChunkIndex.Z),
                        (ChunkIndex.X * Terrain.Instance.TilesPerChunkSide, z + ChunkIndex.Z * Terrain.Instance.TilesPerChunkSide)
                    );

                    foreach (int index in GetAllVertexIndicesAtPoint((0, z)))
                        m_MeshData.Vertices[index].y = height;
                }
            }

            if (ChunkIndex.Z > 0)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    int height = Terrain.Instance.GetPointHeight(
                        (ChunkIndex.X, ChunkIndex.Z - 1),
                        (x + ChunkIndex.X * Terrain.Instance.TilesPerChunkSide, ChunkIndex.Z * Terrain.Instance.TilesPerChunkSide)
                    );

                    foreach (int index in GetAllVertexIndicesAtPoint((x, 0)))
                        m_MeshData.Vertices[index].y = height;
                }
            }

            for (int z = 0; z <= Terrain.Instance.TilesPerChunkSide; ++z)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    if ((x == 0 && ChunkIndex.X > 0) || (z == 0 && ChunkIndex.Z > 0)) continue;

                    int height = CalculateVertexHeight(x, z, GetVertexIndex((x, z)));

                    foreach (int index in GetAllVertexIndicesAtPoint((x, z)))
                        m_MeshData.Vertices[index].y = height;
                }
            }

            RecomputeAllCenters();
        }

        private int CalculateVertexHeight(int x, int z, int vertexIndex)
        {
            int height = Mathf.FloorToInt(
                HeightMapGenerator.GetPerlinAtPosition(ChunkPosition + m_MeshData.Vertices[vertexIndex]) * Terrain.Instance.MaxHeightSteps
            ) * Terrain.Instance.StepHeight;

            // Vertex (0, 0) in chunk (0, 0)
            if (x == 0 && z == 0)
                return height;

            // first row in chunk (0, 0)
            if (z == 0 && ChunkIndex.X == 0)
            {
                int lastHeight = GetMeshHeightAtPoint((x - 1, z));
                height = Mathf.Clamp(height, lastHeight - Terrain.Instance.StepHeight, lastHeight + Terrain.Instance.StepHeight);
            }
            else
            {
                List<(int, int)> directions = new(new (int, int)[] { (-1, -1), (-1, 0), (0, -1), (1, -1) });

                if (x == 1 && ChunkIndex.X > 0)
                    directions.Add((-1, 1));

                List<int> neighborHeights = new();

                foreach ((int neighborX, int neighborZ) in directions)
                    if (x + neighborX >= 0 && x + neighborX <= Terrain.Instance.TilesPerChunkSide &&
                        z + neighborZ >= 0 && z + neighborZ <= Terrain.Instance.TilesPerChunkSide)
                        neighborHeights.Add(GetMeshHeightAtPoint((x + neighborX, z + neighborZ)));

                if (neighborHeights.TrueForAll(EqualToFirst))
                    height = Mathf.Clamp(
                        height,
                        Mathf.Max(0, neighborHeights[0] - Terrain.Instance.StepHeight),
                        Mathf.Min(neighborHeights[0] + Terrain.Instance.StepHeight, Terrain.Instance.MaxHeight));
                else
                {
                    int[] neighborHeightsArray = neighborHeights.ToArray();
                    int minHeight = Mathf.Min(neighborHeightsArray);
                    int maxHeight = Mathf.Max(neighborHeightsArray);

                    if (Mathf.Abs(minHeight - maxHeight) > Terrain.Instance.StepHeight)
                        height = minHeight + Terrain.Instance.StepHeight;
                    else
                        height = Mathf.Clamp(height, minHeight, maxHeight);
                }
                bool EqualToFirst(int x) => x == neighborHeights[0];
            }

            return height;
        }

        private int CalculateTileCenterHeight(int x, int z)
        {
            int[] cornerHeights = new int[4];
            int i = 0;
            for (int zOffset = 0; zOffset <= 1; ++zOffset)
                for (int xOffset = 0; xOffset <= 1; ++xOffset)
                    cornerHeights[i++] = GetMeshHeightAtPoint((x + xOffset, z + zOffset));

            if (cornerHeights[0] == cornerHeights[3] && cornerHeights[1] == cornerHeights[2])
                return Mathf.Max(cornerHeights);
            else
                return Mathf.Min(cornerHeights) + (Terrain.Instance.StepHeight / 2);

        }

        #endregion


        #region Modify Terrain Chunk

        /// <summary>
        /// Sets the visibility of the chunk.
        /// </summary>
        /// <param name="isVisible">True if the chunk should be visible, false otherwise.</param>
        public void SetVisibility(bool isVisible)
            => m_ChunkObject.GetComponent<MeshRenderer>().enabled = isVisible;

        /// <summary>
        /// Changes the height of the given point and propagates the changes if needed 
        /// so that the step height between two points is maintained.
        /// </summary>
        /// <param name="point">The initial <c>MapPoint</c> of the height change.</param>
        /// <param name="lower"></param>
        /// <param name="steps"></param>
        public void ChangeHeights(MapPoint point, bool lower, int steps = 1)
        {
            (int x, int z) pointInChunk = GetPointInChunk((point.X, point.Z));

            List<int> vertices = GetAllVertexIndicesAtPoint(pointInChunk);

            foreach (int vertex in vertices)
                m_MeshData.Vertices[vertex].y = Mathf.Clamp(
                    m_MeshData.Vertices[vertex].y + (lower ? -steps : steps) * Terrain.Instance.StepHeight,
                    0,
                    Terrain.Instance.MaxHeight
                );

            for (int z = 0; z >= -1; --z)
            {
                for (int x = 0; x >= -1; --x)
                {
                    (int x, int z) tile = (pointInChunk.x + x, pointInChunk.z + z);

                    if (tile.x < 0 || tile.x >= Terrain.Instance.TilesPerChunkSide ||
                        tile.z < 0 || tile.z >= Terrain.Instance.TilesPerChunkSide)
                        continue;

                    Structure structure = GetStructureOccupyingTile(point.X + x, point.Z + z);
                    if (structure != null)
                        structure.ReactToTerrainChange();
                }
            }

            // Update neighboring vertices
            for (int zOffset = -1; zOffset <= 1; ++zOffset)
            {
                for (int xOffset = -1; xOffset <= 1; ++xOffset)
                {
                    if ((xOffset, zOffset) == (0, 0) || !Terrain.Instance.IsPointInBounds((point.X + xOffset, point.Z + zOffset)))
                        continue;

                    MapPoint neighbor = new(point.X + xOffset, point.Z + zOffset);

                    if (Mathf.Abs(point.Y - neighbor.Y) > Terrain.Instance.StepHeight && IsPointInChunk(pointInChunk.x + xOffset, pointInChunk.z + zOffset))
                        Terrain.Instance.ChangePointHeight(neighbor, lower);
                }
            }

            RecomputeCenters(pointInChunk);
        }


        public void SetVertexHeight(MapPoint point, int height)
        {
            (int x, int z) pointInChunk = GetPointInChunk((point.X, point.Z));

            List<int> vertices = GetAllVertexIndicesAtPoint(pointInChunk);

            foreach (int vertex in vertices)
                m_MeshData.Vertices[vertex].y = height;
        }

        public void RecomputeAllCenters()
        {
            for (int z = 0; z < Terrain.Instance.TilesPerChunkSide; ++z)
            {
                for (int x = 0; x < Terrain.Instance.TilesPerChunkSide; ++x)
                {
                    int height = CalculateTileCenterHeight(x, z);

                    foreach (int index in GetAllTileCenterIncides(x, z))
                        m_MeshData.Vertices[index].y = height;
                }
            }
        }

        private void RecomputeCenters((int x, int z) point)
        {
            for (int z = -1; z <= 0; ++z)
            {
                for (int x = -1; x <= 0; ++x)
                {
                    (int x, int z) tile = (point.x + x, point.z + z);

                    if (tile.x >= 0 && tile.z >= 0 && tile.x < Terrain.Instance.TilesPerChunkSide && tile.z < Terrain.Instance.TilesPerChunkSide)
                    {
                        int prevHeight = GetCenterHeight(tile);
                        int newHeight = CalculateTileCenterHeight(tile.x, tile.z);

                        if (prevHeight == newHeight)
                            continue;

                        foreach (int index in GetAllTileCenterIncides(tile.x, tile.z))
                            m_MeshData.Vertices[index].y = newHeight;
                    }
                }
            }
        }

        #endregion


        #region Get Mesh Info

        public (int x, int z) GetPointInChunk((int x, int z) point)
            => (point.x - m_ChunkIndex.x * Terrain.Instance.TilesPerChunkSide, point.z - m_ChunkIndex.z * Terrain.Instance.TilesPerChunkSide);

        public int GetVertexHeight((int x, int z) point)
            => GetMeshHeightAtPoint(GetPointInChunk(point));

        private int GetMeshHeightAtPoint((int x, int z) point)
            => (int)m_MeshData.Vertices[GetVertexIndex(point)].y;

        private int GetVertexIndex((int x, int z) point)
        {
            if (point.x == Terrain.Instance.TilesPerChunkSide && point.z != point.x)
                return (point.z * Terrain.Instance.TilesPerChunkSide + (point.x - 1)) * m_TriangleIndices.Length + 2;
            else if (point.z == Terrain.Instance.TilesPerChunkSide && point.x != point.z)
                return ((point.z - 1) * Terrain.Instance.TilesPerChunkSide + point.x) * m_TriangleIndices.Length + 8;
            else if (point.x == point.z && point.z == Terrain.Instance.TilesPerChunkSide)
                return ((point.z - 1) * Terrain.Instance.TilesPerChunkSide + (point.x - 1)) * m_TriangleIndices.Length + 5;
            else
                return (point.z * Terrain.Instance.TilesPerChunkSide + point.x) * m_TriangleIndices.Length;
        }

        private List<int> GetAllVertexIndicesAtPoint((int x, int z) point)
        {
            List<int> vertices = new();

            int vertexIndex = 0;
            for (int z = 0; z >= -1; --z)
            {
                for (int x = 0; x >= -1; --x)
                {
                    if (point.z + z < 0 || point.x + x < 0 ||
                        point.z + z >= Terrain.Instance.TilesPerChunkSide || point.x + x >= Terrain.Instance.TilesPerChunkSide)
                    {
                        vertexIndex++;
                        continue;
                    }

                    int tileIndex = (point.z + z) * Terrain.Instance.TilesPerChunkSide + (point.x + x);

                    for (int i = 0; i < m_SharedVertexOffsets[vertexIndex].Count; ++i)
                    {
                        int index = tileIndex * m_TriangleIndices.Length + m_SharedVertexOffsets[vertexIndex][i];
                        if (index >= 0) vertices.Add(index);
                    }

                    vertexIndex++;
                }
            }

            return vertices;
        }

        public int GetTileCenterHeight((int x, int z) globalTile)
        {
            (int x, int z) tileInChunk = GetPointInChunk(globalTile);
            return GetCenterHeight(tileInChunk);
        }

        private int GetCenterHeight((int x, int z) tile) => (int)m_MeshData.Vertices[GetCenterHeight(tile.x, tile.z)].y;

        private int GetCenterHeight(int x, int z) => GetVertexIndex((x, z)) + 1;

        private List<int> GetAllTileCenterIncides(int x, int z)
        {
            int tileIndex = GetVertexIndex((x, z));

            List<int> vertices = new();
            foreach (var index in m_SharedVertexOffsets[^1])
                vertices.Add(tileIndex + index);

            return vertices;
        }

        private bool IsPointInChunk(int x, int z)
            => x >= 0 && z >= 0 && x <= Terrain.Instance.TilesPerChunkSide && z <= Terrain.Instance.TilesPerChunkSide;

        #endregion


        public void SetOccupiedTile(int x, int z, Structure structure)
        {
            (int x, int z) inChunk = GetPointInChunk((x, z));
            m_IsTileOccupied[inChunk.z, inChunk.x] = structure;
        }

        public bool IsTileOccupied(int x, int z)
        {
            (int x, int z) inChunk = GetPointInChunk((x, z));
            return m_IsTileOccupied[inChunk.z, inChunk.x];
        }

        public Structure GetStructureOccupyingTile(int x, int z)
        {
            (int x, int z) inChunk = GetPointInChunk((x, z));
            return m_IsTileOccupied[inChunk.z, inChunk.x];
        }

        public bool IsTileFlat((int x, int z) tile)
        {
            (int x, int z) inChunk = GetPointInChunk(tile);

            int height = GetMeshHeightAtPoint(inChunk);

            for (int z = 0; z <= 1; ++z)
                for (int x = 0; x <= 1; ++x)
                    if (height != GetMeshHeightAtPoint((inChunk.x + x, inChunk.z + z)))
                        return false;

            return height != Terrain.Instance.WaterLevel;
        }

    }
}