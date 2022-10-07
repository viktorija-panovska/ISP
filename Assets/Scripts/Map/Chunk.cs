using UnityEngine;


[System.Serializable]
public struct VoxelType
{
    public string BlockName;
    public bool IsSolid;
    public int TextureID;
}


public struct Voxel
{
    public const int Vertices = 8;
    public const int Faces = 6;
    public const int VerticesPerFace = 4;

    public static readonly Vector3[] VertexOffsets = new Vector3[Vertices]
    {
        new Vector3(0, 0, 0),   // front lower left
        new Vector3(1, 0, 0),   // front lower right
        new Vector3(1, 1, 0),   // front upper right
        new Vector3(0, 1, 0),   // front upper left
        new Vector3(0, 0, 1),   // back lower left
        new Vector3(1, 0, 1),   // back lower right
        new Vector3(1, 1, 1),   // back upper right
        new Vector3(0, 1, 1),   // back upper left
    };

    // These are the indeces of the vertices on each face in the VertexOffsets array
    public static readonly int[,] FaceVertices = new int[Faces, VerticesPerFace]
    {
        { 0, 1, 2, 3 },   // front face
        { 5, 4, 7, 6 },   // back face
        { 3, 2, 6, 7 },   // top face
        { 0, 1, 5, 4 },   // bottom face
        { 4, 0, 3, 7 },   // left face
        { 1, 5, 6, 2 },   // right face
    };

    public static readonly Vector2[] UvOffsets = new Vector2[VerticesPerFace]
    {
        new Vector2(0, 0),
        new Vector2(0, WorldMap.NormalizedTextureBlockSize),
        new Vector2(WorldMap.NormalizedTextureBlockSize, 0),
        new Vector2(WorldMap.NormalizedTextureBlockSize, WorldMap.NormalizedTextureBlockSize)
    };

    // This is the vector that points us to the face of the neighboring voxel that touches
    // the chosen face of this voxel
    public static readonly Vector3[] NeighborVoxelFace = new Vector3[Faces]
    {
        new Vector3(0, 0, -1),    // front face
        new Vector3(0, 0, 1),     // back face
        new Vector3(0, 1, 0),     // top face
        new Vector3(0, -1, 0),    // bottom face
        new Vector3(-1, 0, 0),    // left face
        new Vector3(1, 0, 0)      // right face
    };
}


public class Chunk
{
    public (int x, int z) ChunkWorldLocation;

    private GameObject chunkObject;
    public Vector3 ChunkPosition { get => chunkObject.transform.position; }

    public static readonly int Width = 5;
    public static readonly int Height = 15;

    private byte[,,] voxelIDs = new byte[Width, Height, Width];

    private int vertexIndex = 0;
    private int triangleIndex = 0;

    private WorldMap worldMap;


    public Chunk(WorldMap worldMap, (int, int) chunkLoc)
    {
        ChunkWorldLocation = chunkLoc;

        chunkObject = new GameObject();
        chunkObject.AddComponent<MeshFilter>();
        chunkObject.AddComponent<MeshRenderer>();
        chunkObject.AddComponent<MeshCollider>();
        
        this.worldMap = worldMap;
        chunkObject.GetComponent<MeshRenderer>().material = worldMap.WorldMaterial;
        chunkObject.transform.SetParent(worldMap.transform);
        chunkObject.transform.position = new Vector3(ChunkWorldLocation.x * Width, 0f, ChunkWorldLocation.z * Width);
        chunkObject.name = "Chunk " + ChunkWorldLocation.x + " " + ChunkWorldLocation.z;

        FillChunkMap();
        DrawMesh(GenerateMeshData());
    }

    private void FillChunkMap()
    {
        for (int y = 0; y < Height; ++y)
            for (int x = 0; x < Width; ++x)
                for (int z = 0; z < Width; ++z)
                    voxelIDs[x, y, z] = worldMap.GetVoxel(new Vector3(x, y, z) + ChunkPosition);

    }

    private ChunkData GenerateMeshData()
    {
        ChunkData chunkData = new ChunkData(Width, Height);

        for (int y = 0; y < Height; ++y)
            for (int x = 0; x < Width; ++x)
                for (int z = 0; z < Width; ++z)
                    AddBlockToChunk(chunkData, new Vector3(x, y, z));

        return chunkData;
    }


    private void AddBlockToChunk(ChunkData chunkData, Vector3 voxelPosition)
    {
        // get coordinates for texture
        int textureID = worldMap.VoxelTypes[voxelIDs[(int)voxelPosition.x, (int)voxelPosition.y, (int)voxelPosition.z]].TextureID;
        float y = (textureID / WorldMap.TextureAtlasBlocks) * WorldMap.NormalizedTextureBlockSize;
        float x = (textureID - (y * WorldMap.TextureAtlasBlocks)) * WorldMap.NormalizedTextureBlockSize;
        Vector2 textureCoords = new Vector2(x, y);

        for (int face = 0; face < Voxel.Faces; ++face)
        {
            // if the face we're drawing has a neighboring face, it shouldn't be drawn
            if (!IsVoxelSolid(voxelPosition + Voxel.NeighborVoxelFace[face]))
            {
                for (int i = 0; i < Voxel.VerticesPerFace; ++i)
                {
                    chunkData.AddVertex(vertexIndex + i, voxelPosition + Voxel.VertexOffsets[Voxel.FaceVertices[face, i]]);
                    chunkData.AddUV(vertexIndex + i, textureCoords + Voxel.UvOffsets[i]);
                }

                chunkData.AddTriangles(triangleIndex, vertexIndex, vertexIndex + 2, vertexIndex + 1, vertexIndex + 2, vertexIndex, vertexIndex + 3);

                vertexIndex += 4;
                triangleIndex += 6;
            }
        }
    }


    private bool IsVoxelInChunk(Vector3 pos)
    {
        if (pos.x < 0 || pos.x > Width - 1 ||
            pos.y < 0 || pos.y > Height - 1 ||
            pos.z < 0 || pos.z > Width - 1)
            return false;

        return true;
    }


    private bool IsVoxelSolid(Vector3 pos)
    {
        if (!IsVoxelInChunk(pos))
            return worldMap.VoxelTypes[worldMap.GetVoxel(pos + ChunkPosition)].IsSolid;

        return worldMap.VoxelTypes[voxelIDs[(int)pos.x, (int)pos.y, (int)pos.z]].IsSolid;
    }


    private void DrawMesh(ChunkData meshData)
    {
        Mesh mesh = new Mesh()
        {
            name = "Chunk Mesh",
            vertices = meshData.vertices,
            triangles = meshData.triangles,
            uv = meshData.uvs
        };

        mesh.RecalculateNormals();

        chunkObject.GetComponent<MeshFilter>().mesh = mesh;
        chunkObject.GetComponent<MeshCollider>().sharedMesh = mesh;
    }
}