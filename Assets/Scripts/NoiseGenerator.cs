using UnityEngine;

public class NoiseGenerator
{
    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, int octaves, float persistance, float lacunarity)
    {
        float[,] noiseMap = new float[mapWidth, mapHeight];

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


        for (int x = 0; x < mapWidth; ++x) 
        {
            for (int y = 0; y < mapHeight; ++y)
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
                    amplitude *= persistance;

                    //increases each octave
                    frequency *= lacunarity;
                }

                noiseMap[x, y] = elevation / amplitudeSum;
            }
        }

        return noiseMap;
    }
}
