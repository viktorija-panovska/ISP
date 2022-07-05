using UnityEngine;


[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}


public class MapGenerator : MonoBehaviour
{
    private readonly int mapWidth = 400;                // map width in pixels
    private readonly int mapHeight = 200;               // map height in pixels
    private readonly float scale = 10f;                 // number of grid points along every direction (zoom, the larger the number the less zoomed in)
    private readonly int octaves = 3;                   // number of levels of detail
    private readonly float persistence = 0.5f;          // how much each octave contributes to the overall shape (adjusts the amplitude) - in range 0..1
    private readonly float lacunarity = 2;              // how much detail is added or removed at each octave (adjusts frequency) - must be > 1
    
    public TerrainType[] regions;


    public void GenerateMap()
    {
        int seed = GenerateSeed();

        // generate noise map
        float[,] noiseMap = Noise.GenerateNoiseMap(mapWidth, mapHeight, seed, scale, octaves, persistence, lacunarity);

        // generate the color map by picking from terrain types
        Color[] colorMap = new Color[mapHeight * mapWidth];
        for (int x = 0; x < mapWidth; ++x)
        {
            for (int y = 0; y < mapHeight; ++y)
            {
                for (int i = 0; i < regions.Length; ++i)
                {
                    if (noiseMap[x, y] <= regions[i].height)
                    {
                        colorMap[y * mapWidth + x] = regions[i].color;
                        break;
                    }
                }
            }
        }

        MapDisplay display = FindObjectOfType<MapDisplay>();
        display.DrawTexture(GenerateTexture(colorMap, mapWidth, mapHeight));
    }


    private int GenerateSeed()
    {
        System.Random ranGen = new System.Random();
        return ranGen.Next();
    }


    private Texture2D GenerateTexture(Color[] colorMap, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(colorMap);
        texture.Apply();

        return texture;
    }
}