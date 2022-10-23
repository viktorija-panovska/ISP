using UnityEngine;

public class ChunkMeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    public ChunkMeshData(int width, int height)
    {
        vertices = new Vector3[width * height];
        uvs = new Vector2[width * height];
        triangles = new int[(width - 1) * (height - 1) * (BlockData.Faces * 6)];
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

