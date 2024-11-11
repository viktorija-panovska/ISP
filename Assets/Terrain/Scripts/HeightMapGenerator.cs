using UnityEngine;
using Random = System.Random;

namespace Populous
{
    /// <summary>
    /// The <c>IHeightMapGenerator</c> interface defines methods necessary for classes which generate a height map.
    /// </summary>
    public interface IHeightMapGenerator
    {
        /// <summary>
        /// Computes the height at a given position using the Perlin Noise function and a falloff function.
        /// </summary>
        /// <param name="position">A <c>Vector3</c> representing the position on the terrain that the height should be computed for.</param>
        /// <returns>A <c>float</c> between 0 and 1 representing the height of the terrain at the given position.</returns>
        public float GetHeightAtPosition(Vector3 position);
    }


    /// <summary>
    /// The <c>HeightMapGenerator</c> class is a static class which contains properties and methods 
    /// which are used to generate a height map for the terrain.
    /// </summary>
    public class HeightMapGenerator : IHeightMapGenerator
    {
        private const float SCALE = 2f;
        private const int OCTAVES = 6;              // number of levels of detail
        private const float PERSISTENCE = 0.5f;     // how much each octave contributes to the overall shape (adjusts the amplitude) - in range 0..1
        private const float LACUNARITY = 2;         // how much detail is added or removed at each octave (adjusts frequency) - must be > 1

        private int m_Seed;
        private Vector2[] m_Offsets;


        /// <summary>
        /// Constructor for the <c>HeightMapGenerator</c> class.
        /// </summary>
        /// <param name="mapSeed">The random seed for the height map.</param>
        public HeightMapGenerator(int mapSeed)
        {
            m_Seed = mapSeed;
            m_Offsets = GenerateNoiseOffsets();
        }


        /// <summary>
        /// Creates a noise offset for each octave based on the height map seed.
        /// </summary>
        /// <returns>A <c>Vector2</c> array of offsets.</returns>
        private Vector2[] GenerateNoiseOffsets()
        {
            // to get different random maps every time, we need to sample from different random coordinates
            // we sample at differnet random coordinates for each octave
            Random ranGen = new(m_Seed);
            Vector2[] offsets = new Vector2[OCTAVES];

            for (int i = 0; i < OCTAVES; ++i)
            {
                float offsetX = ranGen.Next(-1000, 1000);
                float offsetY = ranGen.Next(-1000, 1000);
                offsets[i] = new Vector2(offsetX, offsetY);
            }

            return offsets;
        }


        /// <inheritdoc />
        public float GetHeightAtPosition(Vector3 position)
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


        /// <summary>
        /// Computes the terrain falloff at a given position, based on the distance from the center.
        /// </summary>
        /// <remarks>Used to generate a terrain in the form of an island.</remarks>
        /// <param name="position">A <c>Vector3</c> representing the position on the terrain that the height should be computed for.</param>
        /// <returns>A <c>float</c> between 0 and 1 representing the falloff at the given position.</returns>
        private float GetFalloffAtPosition(Vector3 position)
        {
            float dx = 2 * position.x / Terrain.Instance.UnitsPerSide - 1;
            float dz = 2 * position.z / Terrain.Instance.UnitsPerSide - 1;

            return (1 - (1 - (dx * dx)) * (1 - (dz * dz)));
        }
    }
}