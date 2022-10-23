using UnityEngine;


[System.Serializable]
public struct Terrain
{
    public float Height;
    public Color Color;
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

    private const int heightMultiplier = 40;
    private const int tileSize = 10;

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

    public Texture2D GenerateTexture(Mesh mesh)
    {
        Color[] colorMap = GenerateColorMap(mesh.vertices);

        Texture2D texture = new Texture2D(mapWidth, mapHeight);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colorMap);
        texture.Apply();

        return texture;
    }

    private Color[] GenerateColorMap(Vector3[] vertices)
    {
        // generate the color map by picking from terrain types
        Color[] colorMap = new Color[mapWidth * mapHeight];

        for (int y = 0; y < mapHeight; ++y)
        {
            for (int x = 0; x < mapWidth; ++x)
            {
                for (int i = 0; i < Regions.Length; ++i)
                {
                    if (vertices[x * mapHeight + y].y <= Regions[i].Height)
                    {
                        colorMap[x * mapHeight + y] = Regions[i].Color;
                        break;
                    }
                }
            }
        }

        return colorMap;
    }

    private ChunkMeshData GenerateMeshData(float[,] heightMap)
    {
        float topLeftX = (mapWidth - 1) / -2f;
        float topLeftZ = (mapHeight - 1) / 2f;

        int vertexWidth = (mapWidth - 1) / tileSize + 1;     // number of vertices per row
        int vertexHeight = (mapHeight - 1) / tileSize + 1;   // number of vertices per column

        ChunkMeshData meshData = new ChunkMeshData(vertexWidth, vertexHeight);
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int y = 0; y < mapHeight; y += tileSize)
        {
            for (int x = 0; x < mapWidth; x += tileSize)
            {
                meshData.AddVertex(vertexIndex, new Vector3(topLeftX + x,
                    HeightCurve.Evaluate(heightMap[x, y]) * heightMultiplier,
                    topLeftZ - y));
                meshData.AddUV(vertexIndex, new Vector2(x / (float)mapWidth, y / (float)mapHeight));

                if (x < mapWidth - 1 && y < mapHeight - 1)
                {
                    meshData.AddTriangles(triangleIndex, vertexIndex, vertexIndex + vertexWidth + 1, vertexIndex + vertexWidth, vertexIndex + vertexWidth + 1, vertexIndex, vertexIndex + 1);
                    triangleIndex += 6;
                }

                vertexIndex++;
            }
        }

        return meshData;
    }

    private void DrawMesh(ChunkMeshData meshData, Texture2D texture)
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