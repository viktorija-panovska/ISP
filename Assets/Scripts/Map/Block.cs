using System;
using UnityEngine;


public enum BlockType
{
    None,
    Air,
    Water,
    Grass
}


public struct BlockProperties
{
    public bool IsSolid { get; }
    public int TopTextureId { get; }
    public int SideTextureId { get; }

    public BlockProperties(bool isSolid, int topTextureId, int sideTextureId)
    {
        IsSolid = isSolid;
        TopTextureId = topTextureId;
        SideTextureId = sideTextureId;
    }
}


public struct BlockData
{
    public const int Width = 1;
    public const int Height = 1;
    public const int Vertices = 8;
    public const int Faces = 6;
    public const int VerticesPerFace = 4;

    public static readonly Vector3[] VertexOffsets = new Vector3[Vertices]
    {
        new Vector3(0,     0,      0),       // front lower left
        new Vector3(Width, 0,      0),       // front lower right
        new Vector3(Width, Height, 0),       // front upper right
        new Vector3(0,     Height, 0),       // front upper left
        new Vector3(0,     0,      Width),   // back lower left
        new Vector3(Width, 0,      Width),   // back lower right
        new Vector3(Width, Height, Width),   // back upper right
        new Vector3(0,     Height, Width),   // back upper left
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
        new Vector2(WorldMap.Instance.NormalizedTextureBlockSize, 0),
        new Vector2(WorldMap.Instance.NormalizedTextureBlockSize, WorldMap.Instance.NormalizedTextureBlockSize),
        new Vector2(0, WorldMap.Instance.NormalizedTextureBlockSize)
    };

    // This is the vector that points us to the face of the neighboring voxel that touches
    // the chosen face of this block
    public static readonly Vector3[] NeighborBlockFace = new Vector3[Faces]
    {
        new Vector3(0, 0, -1),    // front face
        new Vector3(0, 0, 1),     // back face
        new Vector3(0, 1, 0),     // top face
        new Vector3(0, -1, 0),    // bottom face
        new Vector3(-1, 0, 0),    // left face
        new Vector3(1, 0, 0)      // right face
    };

    public static readonly bool[] IsSideFace = new bool[]
    {
        true,
        true,
        false,
        false,
        true,
        true
    };

    public static readonly bool[] IsTopVertex = new bool[]
    {
        false,
        false,
        true,
        true,
        false,
        false,
        true,
        true
    };
}


public class Block
{
    public BlockType Type { get; set; }

    // front right, front left
    // back right,  back left
    private Vector3[,] topVertices;
    private Vector3[,] bottomVertices;

    public Block(BlockType type)
    {
        Type = type;
        topVertices = new Vector3[,] { { BlockData.VertexOffsets[2], BlockData.VertexOffsets[3] }, 
                                       { BlockData.VertexOffsets[6], BlockData.VertexOffsets[7]} };
        bottomVertices = new Vector3[,] { { BlockData.VertexOffsets[1], BlockData.VertexOffsets[0] },
                                          { BlockData.VertexOffsets[5], BlockData.VertexOffsets[4]} };
    }

    private (int x, int y) GetIndex(int i)
    {
        switch (i)
        {
            case 2: return (0, 0);
            case 3: return (0, 1);
            case 6: return (1, 0);
            case 7: return (1, 1);
            default: throw new Exception("Invalid vertex index");
        }
    }

    public Vector3 GetVertex(int i)
    {
        (int x, int y) = GetIndex(i);
        return topVertices[x, y];
    }

    public Vector3 GetDiagonalVertex(int i)
    {
        (int x, int y) = GetIndex(i);
        return topVertices[(x + 1) % 2, (y + 1) % 2];
    }

    public void LowerVertex(int i)
    {
        (int x, int y) = GetIndex(i);

        // vertex clicked on goes down
        topVertices[x, y].y = 0;

        (int x, int y) diagonal = ((x + 1) % 2, (y + 1) % 2);

        // neighboring vertices go to the diagonal one to form a half-pyramid shape
        topVertices[(x + 1) % 2, y] = topVertices[diagonal.x, diagonal.y];
        topVertices[x, (y + 1) % 2] = topVertices[diagonal.x, diagonal.y];
    }
}