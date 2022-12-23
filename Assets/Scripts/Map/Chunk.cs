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

    public Vector3 Position { get => gameObject.transform.position; }
    public (int x, int z) Index { get; private set; }

    public const int TileWidth = 50;
    public const int TileNumber = 5;
    public int Width { get => TileNumber * TileWidth; }     // number of pixels on each side of the chunk

    private readonly Vector2[] tileVertexOffsets =
    {
        new Vector2(0, 0),
        new Vector2(TileWidth, 0),
        new Vector2(TileWidth, TileWidth),
        new Vector2(TileWidth, TileWidth),
        new Vector2(0, TileWidth),
        new Vector2(0, 0)
    };
    private readonly int[,][] vertices = new int[TileNumber + 1, TileNumber + 1][];
    private readonly MeshData meshData;


    public Chunk((int x, int z) locationInMap)
    {
        gameObject = new GameObject();
        gameObject.AddComponent<MeshFilter>();
        gameObject.AddComponent<MeshRenderer>();
        gameObject.AddComponent<MeshCollider>();
        gameObject.GetComponent<MeshRenderer>().material = WorldMap.WorldMaterial;
        gameObject.transform.SetParent(WorldMap.Transform);

        Index = locationInMap;
        gameObject.transform.position = new Vector3(Index.x * Width, 0f, Index.z * Width);
        gameObject.name = "Chunk " + Index.x + " " + Index.z;

        meshData = GenerateMeshData();
        DrawMesh();
    }


    private MeshData GenerateMeshData()
    {
        MeshData meshData = new MeshData(Width, Width);
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int z = 0; z < Width; z += TileWidth)
        {
            for (int x = 0; x < Width; x += TileWidth)
            {
                for (int i = 0; i < tileVertexOffsets.Length; ++i)
                {
                    Vector3 vertex = new Vector3(x + tileVertexOffsets[i].x, 0, z + tileVertexOffsets[i].y);
                    meshData.AddVertex(vertexIndex + i, vertex);
                    meshData.AddUV(vertexIndex + i, new Vector2(vertex.x / Width, vertex.z / Width));

                    (int x_i, int z_i) = ((int)((z + tileVertexOffsets[i].x) / TileWidth), (int)((x + tileVertexOffsets[i].y) / TileWidth));
                    if (vertices[x_i, z_i] == null)
                        vertices[x_i, z_i] = new int[9];    // index 0 stores the index of the next free slot in the array

                    vertices[x_i, z_i][++vertices[x_i, z_i][0]] = vertexIndex + i;
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


    public void UpdateHeightAtVertex(int x, int z, int height)
    {
        foreach (int i in vertices[x, z])
            meshData.vertices[i].y = height;

        DrawMesh();
    }
}
