using UnityEngine;


[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
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


public class MapGenerator : MonoBehaviour
{
    private readonly int mapWidth = 200;                // map width in pixels
    private readonly int mapHeight = 200;               // map height in pixels
    private readonly float scale = 10f;                 // number of grid points along every direction (zoom, the larger the number the less zoomed in)
    private readonly int octaves = 3;                   // number of levels of detail
    private readonly float persistence = 0.5f;          // how much each octave contributes to the overall shape (adjusts the amplitude) - in range 0..1
    private readonly float lacunarity = 2;              // how much detail is added or removed at each octave (adjusts frequency) - must be > 1
    
    public TerrainType[] regions;

    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;

    float heightMultiplier = 50f;
    public AnimationCurve heightCurve;



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
        Color[] colorMap = new Color[mapHeight * mapWidth];
        for (int x = 0; x < mapWidth; ++x)
        {
            for (int y = 0; y < mapHeight; ++y)
            {
                for (int i = 0; i < regions.Length; ++i)
                {
                    if (heightMap[x, y] <= regions[i].height)
                    {
                        colorMap[y * mapWidth + x] = regions[i].color;
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
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        float topLeftX = (width - 1) / -2f;
        float topLeftZ = (height - 1) / 2f;

        MeshData meshData = new MeshData(width, height);
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                meshData.AddVertex(vertexIndex, new Vector3(topLeftX + x, heightCurve.Evaluate(heightMap[x, y]) * heightMultiplier, topLeftZ - y));
                meshData.AddUV(vertexIndex, new Vector2(x / (float)width, y / (float)height));

                if (x < width - 1 && y < height - 1)
                {
                    meshData.AddTriangle(triangleIndex, vertexIndex, vertexIndex + width + 1, vertexIndex + width);
                    triangleIndex += 3;
                    meshData.AddTriangle(triangleIndex, vertexIndex + width + 1, vertexIndex, vertexIndex + 1);
                    triangleIndex += 3;
                }

                vertexIndex++;
            }
        }

        return meshData;

    }

    private void DrawMesh(MeshData meshData, Texture2D texture)
    {
        Mesh mesh = new Mesh();
        mesh.vertices = meshData.vertices;
        mesh.triangles = meshData.triangles;
        mesh.uv = meshData.uvs;
        mesh.RecalculateNormals();

        meshFilter.sharedMesh = mesh;
        meshRenderer.sharedMaterial.mainTexture = texture;
    }
}