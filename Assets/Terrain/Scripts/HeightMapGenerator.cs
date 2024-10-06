
using UnityEngine;

namespace Populous
{
    public static class HeightMapGenerator
    {
        private static int m_Seed;
        private const float SCALE = 2f;
        private const int OCTAVES = 6;              // number of levels of detail
        private const float PERSISTENCE = 0.5f;     // how much each octave contributes to the overall shape (adjusts the amplitude) - in range 0..1
        private const float LACUNARITY = 2;         // how much detail is added or removed at each octave (adjusts frequency) - must be > 1
        private static Vector2[] m_Offsets;


        public static void Initialize(int mapSeed)
        {
            m_Seed = mapSeed;
            m_Offsets = GenerateNoiseOffsets();
        }


        private static Vector2[] GenerateNoiseOffsets()
        {
            // to get different random maps every time, we need to sample from different random coordinates
            // we sample at differnet random coordinates for each octave
            System.Random ranGen = new(m_Seed);
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
                float x = position.x / Terrain.Instance.UnitsPerChunkSide * SCALE + m_Offsets[i].x;
                float z = position.z / Terrain.Instance.UnitsPerChunkSide * SCALE + m_Offsets[i].y;

                // increase the noise by the perlin value of each octave
                // the higher the frequency, the further apart the sample points will be, so the elevation will change more rapidly
                elevation += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
                amplitudeSum += amplitude;

                // decreases each octave
                amplitude *= PERSISTENCE;

                //increases each octave
                frequency *= LACUNARITY;
            }

            return Mathf.Clamp01((elevation / amplitudeSum) - GetFalloffAtPosition(position));
        }


        public static float GetFalloffAtPosition(Vector3 position)
        {
            float dx = 2 * position.x / Terrain.Instance.UnitsPerSide - 1;
            float dz = 2 * position.z / Terrain.Instance.UnitsPerSide - 1;

            return (1 - (1 - (dx * dx)) * (1 - (dz * dz)));
        }
    }
}