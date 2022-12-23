using UnityEngine;

public static class NoiseGenerator
{
    public static Vector2[] GenerateNoiseOffsets(int mapSeed, int octaves)
    {
        // to get different random maps every time, we need to sample from different random coordinates
        // we sample at differnet random coordinates for each octave
        System.Random ranGen = new System.Random(mapSeed);
        Vector2[] offsets = new Vector2[octaves];

        for (int i = 0; i < octaves; ++i)
        {
            float offsetX = ranGen.Next(-1000, 1000);
            float offsetY = ranGen.Next(-1000, 1000);
            offsets[i] = new Vector2(offsetX, offsetY);
        }

        return offsets;
    }


    public static float GetPerlinAtPosition(Vector3 position, float scale, Vector2[] offsets,
                                            int octaves, float persistence, float lacunarity)
    {
        float amplitude = 1;
        float frequency = 1;
        float elevation = 0;
        float amplitudeSum = 0;

        for (int i = 0; i < octaves; ++i)
        {
            // we cannot use the pixel coordinates(x, y) because the perlin noise always generates the same value at whole numbers
            // we also multiply by scale to not get an extremely zoomed in picture
            float x = position.x / Chunk.TileNumber * scale + offsets[i].x;
            float y = position.y / Chunk.TileNumber * scale + offsets[i].y;

            // increase the noise by the perlin value of each octave
            // the higher the frequency, the further apart the sample points will be, so the elevation will change more rapidly
            elevation += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            amplitudeSum += amplitude;

            // decreases each octave
            amplitude *= persistence;

            //increases each octave
            frequency *= lacunarity;
        }

        return (elevation / amplitudeSum);
    }




    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, 
                                            int octaves, float persistence, float lacunarity)
    {
        float[,] falloffMap = GenerateFalloffMap(mapWidth, mapHeight);

        float[,] noiseMap = new float[mapHeight, mapWidth];

        // to get different random maps every time, we need to sample from different random coordinates
        // we sample at differnet random coordinates for each octave
        System.Random ranGen = new System.Random(seed);
        Vector2[] offsets = new Vector2[octaves];

        for (int i = 0; i < octaves; ++i)
        {
            float offsetX = ranGen.Next(-1000, 1000);
            float offsetY = ranGen.Next(-1000, 1000);
            offsets[i] = new Vector2(offsetX, offsetY);
        }


        for (int y = 0; y < mapHeight; ++y) 
        {
            for (int x = 0; x < mapWidth; ++x)
            {
                float amplitude = 1;
                float frequency = 1;
                float elevation = 0;
                float amplitudeSum = 0;

                for (int i = 0; i < octaves; ++i)
                {
                    // we cannot use the pixel coordinates(x, y) because the perlin noise always generates the same value at whole numbers
                    // we also multiply by scale to not get an extremely zoomed in picture
                    float xCoordinate = (float)x / mapWidth * scale + offsets[i].x;
                    float yCoordinate = (float)y / mapHeight * scale + offsets[i].y;

                    // increase the noise by the perlin value of each octave
                    // the higher the frequency, the further apart the sample points will be, so the elevation will change more rapidly
                    elevation += Mathf.PerlinNoise(xCoordinate * frequency, yCoordinate * frequency) * amplitude;
                    amplitudeSum += amplitude;

                    // decreases each octave
                    amplitude *= persistence;

                    //increases each octave
                    frequency *= lacunarity;
                }

                noiseMap[y, x] = (elevation / amplitudeSum) - falloffMap[y, x];
            }
        }

        return noiseMap;
    }


    // Makes the map look like a peninsula
    public static float[,] GenerateFalloffMap(int mapWidth, int mapHeight)
    {
        float[,] falloffMap = new float[mapHeight, mapWidth];

        for (int y = 0; y < mapHeight; ++y)
        {
            for (int x = 0; x < mapWidth; ++x)
            {
                float distance = Mathf.Sqrt(Mathf.Pow(((mapWidth - 1) - x) / 10, 2) + Mathf.Pow(((mapHeight - 1) - y) / 1.5f, 2));
                falloffMap[y, x] = distance / mapWidth;
            }
        }

        return falloffMap;
    }
}
