using UnityEngine;

public static class NoiseGenerator
{
    private static int seed;
    private const float SCALE = 2f;
    private const int OCTAVES = 6;              // number of levels of detail
    private const float PERSISTENCE = 0.5f;     // how much each octave contributes to the overall shape (adjusts the amplitude) - in range 0..1
    private const float LACUNARITY = 2;         // how much detail is added or removed at each octave (adjusts frequency) - must be > 1
    private static Vector2[] offsets;


    public static void Initialize(int mapSeed)
    {
        seed = mapSeed;
        offsets = GenerateNoiseOffsets();
    }


    private static Vector2[] GenerateNoiseOffsets()
    {
        // to get different random maps every time, we need to sample from different random coordinates
        // we sample at differnet random coordinates for each octave
        System.Random ranGen = new System.Random(seed);
        Vector2[] offsets = new Vector2[OCTAVES];

        for (int i = 0; i < OCTAVES; ++i)
        {
            float offsetX = ranGen.Next(-1000, 1000);
            float offsetY = ranGen.Next(-1000, 1000);
            offsets[i] = new Vector2(offsetX, offsetY);
        }

        return offsets;
    }


    // returns a value between 0 and 1
    public static float GetPerlinAtPosition(Vector3 position)
    {
        float amplitude = 1;
        float frequency = 1;
        float elevation = 0;
        float amplitudeSum = 0;

        for (int i = 0; i < OCTAVES; ++i)
        {
            // we cannot use the pixel coordinates(x, y) because the perlin noise always generates the same value at whole numbers
            // we also multiply by scale to not get an extremely zoomed in picture
            float x = position.x / Chunk.WIDTH * SCALE + offsets[i].x;
            float z = position.z / Chunk.WIDTH * SCALE + offsets[i].y;

            // increase the noise by the perlin value of each octave
            // the higher the frequency, the further apart the sample points will be, so the elevation will change more rapidly
            elevation += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
            amplitudeSum += amplitude;

            // decreases each octave
            amplitude *= PERSISTENCE;

            //increases each octave
            frequency *= LACUNARITY;
        }

        return (elevation + (1 - GetFalloffAtPosition(position))) / (2 * amplitudeSum);
    }


    public static float GetFalloffAtPosition(Vector3 position)
    {
        //float nx = 2 * position.x / WorldMap.WIDTH - 1;
        //float nz = 2 * position.z / WorldMap.WIDTH - 1;

        //return 1 - (1 - nx * nx) * (1 - nz * nz);
        float distance = Mathf.Sqrt(Mathf.Pow((Chunk.WIDTH - 1 - position.x) / 10, 2) + Mathf.Pow((Chunk.WIDTH - 1 - position.z) / 1.5f, 2));
        return distance / Chunk.WIDTH;
    }
}
