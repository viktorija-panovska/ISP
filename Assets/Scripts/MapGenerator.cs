using UnityEngine;


[System.Serializable]
public struct Terrain
{
    public float Height;
    public Color Color;
}


public class MeshData
{
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    public MeshData(int meshWidth, int meshHeight)
    {
        vertices = new Vector3[meshWidth * meshHeight];
        uvs = new Vector2[meshWidth * meshHeight];
        triangles = new int[(meshWidth - 1) * (meshHeight - 1) * 6];
    }

    public void AddVertex(int index, Vector3 vertex)
    {
        vertices[index] = vertex;
    }

    public void AddTriangle(int index, int a, int b, int c)
    {
        triangles[index] = a;
        triangles[index + 1] = b;
        triangles[index + 2] = c;
    }

    public void AddUV(int index, Vector2 uv)
    {
        uvs[index] = uv;
    }
}


[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class MapGenerator : MonoBehaviour
{
    // the maximum number of vertices a Unity mesh can have is 255 x 255
    private const int mapWidth = 241;           // map width in pixels
    private const int mapHeight = 241;          // map height in pixels
    private const float scale = 10f;            // number of grid points along every direction (zoom, the larger the number the less zoomed in)
    private const int octaves = 1;              // number of levels of detail
    private const float persistence = 0.5f;     // how much each octave contributes to the overall shape (adjusts the amplitude) - in range 0..1
    private const float lacunarity = 2;         // how much detail is added or removed at each octave (adjusts frequency) - must be > 1

    private const int heightMultiplier = 12;
    public int TileSize = 10;

    // Initialized in the editor
    public Terrain[] Regions;
    public AnimationCurve HeightCurve;


    private void Start()
    {
        float[,] heightMap = GenerateHeightMap(GenerateSeed());
        Texture2D texture = GenerateTexture(heightMap);
        DrawMesh(GenerateMeshData(heightMap), texture);
    }


    private int GenerateSeed()
    {
        System.Random ranGen = new System.Random();
        return ranGen.Next();
    }

    private float[,] GenerateHeightMap(int seed)
    {
        return NoiseGenerator.GenerateNoiseMap(mapWidth, mapHeight, seed, scale, octaves, persistence, lacunarity);
    }

    private Color[] GenerateColorMap(float[,] heightMap)
    {
        // generate the color map by picking from terrain types
        Color[] colorMap = new Color[mapWidth * mapHeight];
        for (int y = 0; y < mapHeight; ++y)
        {
            for (int x = 0; x < mapWidth; ++x)
            {
                for (int i = 0; i < Regions.Length; ++i)
                {
                    if (heightMap[y, x] <= Regions[i].Height)
                    {
                        colorMap[x * mapHeight + y] = Regions[i].Color;
                        break;
                    }
                }
            }
        }

        return colorMap;
    }

    private Texture2D GenerateTexture(float[,] heightMap)
    {
        Color[] colorMap = GenerateColorMap(heightMap);

        Texture2D texture = new Texture2D(mapWidth, mapHeight);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colorMap);
        texture.Apply();

        return texture;
    }


    private MeshData GenerateMeshData(float[,] heightMap)
    {
        float topLeftX = (mapWidth - 1) / -2f;
        float topLeftZ = (mapHeight - 1) / 2f;

        int vertexWidth = (mapWidth - 1) / TileSize + 1;     // number of vertices per row
        int vertexHeight = (mapHeight - 1) / TileSize + 1;   // number of vertices per column

        MeshData meshData = new MeshData(vertexWidth, vertexHeight);
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int y = 0; y < mapHeight; y += TileSize)
        {
            for (int x = 0; x < mapWidth; x += TileSize)
            {
                meshData.AddVertex(vertexIndex, new Vector3(topLeftX + x, 
                    Mathf.Round(heightMap[x, y] * heightMultiplier) / heightMultiplier, 
                    topLeftZ - y));
                meshData.AddUV(vertexIndex, new Vector2(x / (float)mapWidth, y / (float)mapHeight));

                if (x < mapWidth - 1 && y < mapHeight - 1)
                {
                    meshData.AddTriangle(triangleIndex, vertexIndex, vertexIndex + vertexWidth + 1, vertexIndex + vertexWidth);
                    triangleIndex += 3;
                    meshData.AddTriangle(triangleIndex, vertexIndex + vertexWidth + 1, vertexIndex, vertexIndex + 1);
                    triangleIndex += 3;
                }

                vertexIndex++;
            }
        }

        return meshData;

    }

    private void DrawMesh(MeshData meshData, Texture2D texture)
    {
        Mesh mesh = new Mesh()
        {
            name = "MapMesh",
            vertices = meshData.vertices,
            triangles = meshData.triangles,
            uv = meshData.uvs
        };

        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterial.mainTexture = texture;
    }
}