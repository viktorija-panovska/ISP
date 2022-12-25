using System.Collections.Generic;
using UnityEngine;

public class MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    public MeshData(int width, int height)
    {
        vertices = new Vector3[width * height];
        uvs = new Vector2[width * height];
        triangles = new int[(width - 1) * (height - 1) * 6];
    }

    public void AddVertex(int index, Vector3 vertex)
    {
        vertices[index] = vertex;
    }

    public void AddTriangles(int index, int a1, int b1, int c1, int a2, int b2, int c2)
    {
        triangles[index] = a1;
        triangles[index + 1] = b1;
        triangles[index + 2] = c1;

        triangles[index + 3] = a2;
        triangles[index + 4] = b2;
        triangles[index + 5] = c2;
    }

    public void AddUV(int index, Vector2 uv)
    {
        uvs[index] = uv;
    }
}


public class Chunk
{
    private readonly GameObject gameObject;
    private readonly MeshData meshData;

    public Vector3 ChunkPosition { get => gameObject.transform.position; }
    public (int x, int z) ChunkIndex { get; private set; }

    public const int TileWidth = 50;
    public const int TileNumber = 5;
    public const int WidthInPixels = TileNumber * TileWidth;
    public const int WidthInVertices = TileNumber + 1;
    public const int MaxVertexIndex = WidthInVertices - 1;

    public const int StepHeight = 20;
    public const int MaxSteps = 7;
    public const int MaxHeight = StepHeight * MaxSteps;

    private readonly Vector2[] tileVertexOffsets =
    {
        new Vector2(0, 0),
        new Vector2(TileWidth, 0),
        new Vector2(TileWidth, TileWidth),
        new Vector2(TileWidth, TileWidth),
        new Vector2(0, TileWidth),
        new Vector2(0, 0)
    };
    private readonly List<int>[,] vertices = new List<int>[WidthInVertices, WidthInVertices];
    private readonly (int, int)[] vertexNeighbors =
    {
        (0, 1),  (0, -1),
        (1, 0),  (-1, 0),
        (1, 1),  (-1, -1),
        (1, -1), (-1, 1)
    };


    public Chunk((int x, int z) locationInMap)
    {
        gameObject = new GameObject();
        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<MeshRenderer>();
        gameObject.AddComponent<MeshCollider>();
        gameObject.GetComponent<MeshRenderer>().material = WorldMap.WorldMaterial;
        gameObject.transform.SetParent(WorldMap.Transform);

        ChunkIndex = locationInMap;
        gameObject.transform.position = new Vector3(ChunkIndex.x * WidthInPixels, 0f, ChunkIndex.z * WidthInPixels);
        gameObject.name = "Chunk " + ChunkIndex.x + " " + ChunkIndex.z;

        meshData = GenerateMeshData();
        SetVertexHeights();
        DrawMesh();
    }


    public float GetVertexHeight(int x, int z) => meshData.vertices[vertices[x, z][0]].y;


    // Building Mesh

    private MeshData GenerateMeshData()
    {
        MeshData meshData = new MeshData(WidthInPixels, WidthInPixels);
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int z = 0; z < WidthInPixels; z += TileWidth)
        {
            for (int x = 0; x < WidthInPixels; x += TileWidth)
            {
                for (int i = 0; i < tileVertexOffsets.Length; ++i)
                {
                    Vector3 vertex = new Vector3(x + tileVertexOffsets[i].x, 0, z + tileVertexOffsets[i].y);
                    meshData.AddVertex(vertexIndex + i, vertex);
                    meshData.AddUV(vertexIndex + i, new Vector2(vertex.x / WidthInPixels, vertex.z / WidthInPixels));

                    (int x_i, int z_i) = ((int)((x + tileVertexOffsets[i].x) / TileWidth), (int)((z + tileVertexOffsets[i].y) / TileWidth));
                    if (vertices[x_i, z_i] == null)
                        vertices[x_i, z_i] = new List<int>();

                    vertices[x_i, z_i].Add(vertexIndex + i);
                }

                meshData.AddTriangles(triangleIndex,
                    vertexIndex, vertexIndex + 2, vertexIndex + 1,
                    vertexIndex + 3, vertexIndex + 5, vertexIndex + 4);
                 
                triangleIndex += 6;
                vertexIndex += 6;
            }
        }
        return meshData;
    }

    private void SetVertexHeights()
    {
        for (int z = 0; z < WidthInVertices; ++z)
        {
            for (int x = 0; x < WidthInVertices; ++x)
            {
                List<int> currentVertices = vertices[x, z];
                float height;

                if (x == 0 && ChunkIndex.x > 0)
                    height = WorldMap.GetVertexHeight((ChunkIndex.x - 1, ChunkIndex.z), (MaxVertexIndex - x, z));
                else if (z == 0 && ChunkIndex.z > 0)
                    height = WorldMap.GetVertexHeight((ChunkIndex.x, ChunkIndex.z - 1), (x, MaxVertexIndex - z));
                else
                    height = Mathf.FloorToInt(NoiseGenerator.GetPerlinAtPosition(ChunkPosition + meshData.vertices[currentVertices[0]]) * MaxSteps) * StepHeight;

                foreach (int v in currentVertices)
                    meshData.vertices[v].y = height;
            }
        }
    }

    private void DrawMesh()
    {
        Mesh mesh = new Mesh()
        {
            name = "Chunk Mesh",
            vertices = meshData.vertices,
            triangles = meshData.triangles,
            uv = meshData.uvs
        };

        mesh.RecalculateNormals();

        gameObject.GetComponent<MeshFilter>().mesh = mesh;
        gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }



    // Modifying Mesh

    public void UpdateChunk(int x, int z, bool decrease)
    {
        Debug.Log("Clicked Chunk: " + gameObject.name + " and Vertex: " + x + " " + z);

        UpdateHeightAtPoint(x, z, decrease);
        DrawMesh();
    }

    private void UpdateHeightAtPoint(int x, int z, bool decrease)
    {
        // Get all the vertices that share the clicked point
        List<int> currentVertices = vertices[x, z];

        foreach (int v in currentVertices)
        {
            if (decrease && meshData.vertices[v].y > 0)
                meshData.vertices[v].y -= StepHeight;

            if (!decrease && meshData.vertices[v].y < MaxHeight)
                meshData.vertices[v].y += StepHeight;
        }

        // Update neighboring vertices
        //foreach ((int dx, int dz) in vertexNeighbors)
        //{
        //    // Check if we're out of bounds
        //    if (x + dx >= 0 && x + dx < vertices.Length &&
        //        z + dz >= 0 && z + dz < vertices.Length)
        //    {
        //        // Since they should all have the same height, we can just check for one
        //        if (meshData.vertices[vertices[x + dx, z + dz][0]].y / StepHeight < meshData.vertices[currentVertices[0]].y / StepHeight - 1 ||
        //            meshData.vertices[vertices[x + dx, z + dz][0]].y / StepHeight > meshData.vertices[currentVertices[0]].y / StepHeight + 1)
        //            UpdateHeightAtPoint(x + dx, z + dz, decrease);
        //    }
        //}
    }
}
