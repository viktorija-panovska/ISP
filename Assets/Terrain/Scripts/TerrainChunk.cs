using System.Collections.Generic;
using UnityEngine;


public class TerrainChunk
{
    private readonly struct MeshData
    {
        private readonly Vector3[] m_Vertices;
        public readonly Vector3[] Vertices { get =>  m_Vertices; }

        private readonly int[] m_Triangles;
        public readonly int[] Triangles { get => m_Triangles; }


        public MeshData(int width, int height)
        {
            m_Vertices = new Vector3[width * height];
            m_Triangles = new int[(width - 1) * (height - 1) * 6];
        }

        public readonly void AddVertex(int index, Vector3 vertex)
        {
            m_Vertices[index] = vertex;
        }

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

    public Vector3 ChunkPosition { get => m_ChunkObject.transform.position; }
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


    public TerrainChunk(int mapPositionX, int mapPositionZ, Transform parentTransform)
    {
        m_ChunkObject = new GameObject();
        m_ChunkObject.AddComponent<MeshFilter>();
        m_ChunkObject.AddComponent<MeshRenderer>();
        m_ChunkObject.AddComponent<MeshCollider>();

        m_ChunkIndex = (mapPositionX, mapPositionZ);

        m_ChunkObject.transform.position = new Vector3(
            ChunkIndex.X * Terrain.Instance.UnitsPerChunk, 
            0,
            ChunkIndex.Z * Terrain.Instance.UnitsPerChunk
        );
        m_ChunkObject.transform.SetParent(parentTransform);
        m_ChunkObject.name = "Chunk " + ChunkIndex.X + " " + ChunkIndex.Z;

        SetVisibility(false);
        m_MeshData = GenerateMeshData();
        SetMesh();
    }


    #region Generate Terrain Chunk

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
        MeshData meshData = new(Terrain.Instance.UnitsPerChunk, Terrain.Instance.UnitsPerChunk);
        int vertexIndex = 0;
        int triangleIndex = 0;

        // Generating the mesh tile by tile
        for (int z = 0; z < Terrain.Instance.TilesPerChunk; ++z)
        {
            for (int x = 0; x < Terrain.Instance.TilesPerChunk; ++x)
            {
                for (int i = 0; i < m_TriangleIndices.Length; ++i)
                {
                    int index = m_TriangleIndices[i];

                    Vector3 vertex = new(
                        (x + m_VertexOffsets[index].x) * Terrain.Instance.UnitsPerTile,
                        0,
                        (z + m_VertexOffsets[index].z) * Terrain.Instance.UnitsPerTile
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

    private int CalculateTileCenterHeight(int x, int z)
    {
        int[] cornerHeights = new int[4];
        int i = 0;
        for (int zOffset = 0; zOffset <= 1; ++zOffset)
            for (int xOffset = 0; xOffset <= 1; ++xOffset)
                cornerHeights[i++] = GetVertexHeight((x + xOffset, z + zOffset));

        if (cornerHeights[0] == cornerHeights[3] && cornerHeights[1] == cornerHeights[2])
            return Mathf.Max(cornerHeights);
        else
            return Mathf.Min(cornerHeights) + (Terrain.Instance.StepHeight / 2);

    }

    #endregion


    #region Get Mesh Info

    public (int x, int z) GetPointInChunk((int x, int z) pointInMap)
        => (pointInMap.x - m_ChunkIndex.x * Terrain.Instance.TilesPerChunk,
            pointInMap.z - m_ChunkIndex.z * Terrain.Instance.TilesPerChunk);

    public int GetVertexHeight(MapPoint point)
        => GetVertexHeight(GetPointInChunk(point.PointInMap));

    private int GetVertexHeight((int x, int z) point)
        => (int)m_MeshData.Vertices[GetVertexIndex(point)].y;

    private int GetVertexIndex((int x, int z) point)
    {
        if (point.x == Terrain.Instance.TilesPerChunk && point.z != point.x)
            return (point.z * Terrain.Instance.TilesPerChunk + (point.x - 1)) * m_TriangleIndices.Length + 2;
        else if (point.z == Terrain.Instance.TilesPerChunk && point.x != point.z)
            return ((point.z - 1) * Terrain.Instance.TilesPerChunk + point.x) * m_TriangleIndices.Length + 8;
        else if (point.x == point.z && point.z == Terrain.Instance.TilesPerChunk)
            return ((point.z - 1) * Terrain.Instance.TilesPerChunk + (point.x - 1)) * m_TriangleIndices.Length + 5;
        else
            return (point.z * Terrain.Instance.TilesPerChunk + point.x) * m_TriangleIndices.Length;
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
                    point.z + z >= Terrain.Instance.TilesPerChunk || point.x + x >= Terrain.Instance.TilesPerChunk)
                {
                    vertexIndex++;
                    continue;
                }

                int tileIndex = (point.z + z) * Terrain.Instance.TilesPerChunk + (point.x + x);

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

    private int GetTileCenterHeight(int x, int z) => (int)m_MeshData.Vertices[GetTileCenterIndex(x, z)].y;

    private int GetTileCenterIndex(int x, int z) => GetVertexIndex((x, z)) + 1;

    private List<int> GetAllTileCenterIncides(int x, int z)
    {
        int tileIndex = GetVertexIndex((x, z));

        List<int> vertices = new();
        foreach (var index in m_SharedVertexOffsets[^1])
            vertices.Add(tileIndex + index);

        return vertices;
    }

    private bool IsPointInChunk(int x, int z)
        => x >= 0 && z >= 0 && x <= Terrain.Instance.TilesPerChunk && z <= Terrain.Instance.TilesPerChunk;

    #endregion


    #region Modify Terrain Chunk

    public void SetVisibility(bool isVisible)
    {
        m_ChunkObject.GetComponent<MeshRenderer>().enabled = isVisible;
    }


    public void ChangePointHeight(MapPoint point, bool lower)
    {
        (int x, int z) pointInChunk = GetPointInChunk(point.PointInMap);

        List<int> vertices = GetAllVertexIndicesAtPoint(pointInChunk);

        foreach (int vertex in vertices)
            m_MeshData.Vertices[vertex].y = Mathf.Clamp(
                m_MeshData.Vertices[vertex].y + (lower ? 1 : -1) * Terrain.Instance.StepHeight,
                0,
                Terrain.Instance.MaxHeight
            );

        // Update neighboring vertices
        for (int zOffset = -1; zOffset <= 1; ++zOffset)
        {
            for (int xOffset = -1; xOffset <= 1; ++xOffset)
            {
                if ((xOffset, zOffset) == (0, 0) || !Terrain.Instance.IsIndexInBounds((point.PointInMap.X + xOffset, point.PointInMap.Z + zOffset)))
                    continue;

                MapPoint neighbor = new(point.PointInMap.X + xOffset, point.PointInMap.Z + zOffset);

                if (Mathf.Abs(point.Y - neighbor.Y) > Terrain.Instance.StepHeight && IsPointInChunk(pointInChunk.x + xOffset, pointInChunk.z + zOffset))
                    Terrain.Instance.ChangePointHeight(neighbor, lower);
            }
        }

        RecomputeCenters(pointInChunk);
    }

    private void RecomputeCenters((int x, int z) point)
    {
        for (int z = -1; z <= 0; ++z)
        {
            for (int x = -1; x <= 0; ++x)
            {
                (int x, int z) tile = (point.x + x, point.z + z);

                if (tile.x >= 0 && tile.z >= 0 && tile.x < Terrain.Instance.TilesPerChunk && tile.z < Terrain.Instance.TilesPerChunk)
                {
                    int prevHeight = GetTileCenterHeight(tile.x, tile.z);
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
}