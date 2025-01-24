using UnityEngine;
using Random = System.Random;

namespace Populous
{
    /// <summary>
    /// The <c>INoiseGenerator</c> interface defines methods necessary for classes which generate a height map.
    /// </summary>
    public interface INoiseGenerator
    {
        public void Setup();

        /// <summary>
        /// Computes the height at a given position using the Perlin Noise function and a falloff function.
        /// </summary>
        /// <param name="position">A <c>Vector3</c> representing the position on the terrain that the height should be computed for.</param>
        /// <returns>A <c>float</c> between 0 and 1 representing the height of the terrain at the given position.</returns>
        public float GetNoiseAtPosition(Vector3 position);
    }


    /// <summary>
    /// The <c>NoiseGenerator</c> class generates a height map for the terrain.
    /// </summary>
    public class NoiseGenerator : MonoBehaviour, INoiseGenerator
    {
        [SerializeField] private float m_Scale = 2f;
        [SerializeField] private int m_Octaves = 2;

        [Header("Falloff")]
        [SerializeField] private Vector2 m_LandmassCenter = Vector2.zero;
        [Tooltip("Lower factors create landmasses with more land and less water, and vice versa.")]
        [SerializeField] private float m_FalloffScaleFactor = 0.5f;

        private int m_Seed;
        private Vector2[] m_Offsets;


        public void Setup()
        {
            m_Seed = !GameData.Instance ? 0 : GameData.Instance.MapSeed;
            m_Offsets = GenerateNoiseOffsets();
        }


        /// <summary>
        /// Creates a noise offset for each octave based on the height map seed.
        /// </summary>
        /// <returns>A <c>Vector2</c> array of offsets.</returns>
        private Vector2[] GenerateNoiseOffsets()
        {
            // to get different random maps every octave, we need to sample from different random coordinates
            // we sample at differnet random coordinates for each octave
            Random ranGen = new(m_Seed);
            Vector2[] offsets = new Vector2[m_Octaves];

            for (int i = 0; i < m_Octaves; ++i)
            {
                float offsetX = ranGen.Next(-1000, 1000);
                float offsetY = ranGen.Next(-1000, 1000);
                offsets[i] = new Vector2(offsetX, offsetY);
            }

            return offsets;
        }


        /// <inheritdoc />
        public float GetNoiseAtPosition(Vector3 position)
        {
            float amplitude = 1;
            float frequency = 1;
            float elevation = 0;
            float amplitudeSum = 0;

            for (int i = 0; i < m_Octaves; ++i)
            {
                // we cannot use the pixel coordinates(x, y) because the perlin noise always generates the same value at whole numbers
                // we also multiply by scale to not get an extremely zoomed in picture
                float x = position.x / Terrain.Instance.UnitsPerChunkSide;
                float z = position.z / Terrain.Instance.UnitsPerChunkSide;

                // increase the noise by the perlin value of each octave
                // the higher the frequency, the further apart the sample points will be, so the elevation will change more rapidly
                elevation += Mathf.PerlinNoise(x * frequency * m_Scale + m_Offsets[i].x, z * frequency * m_Scale + m_Offsets[i].y) * amplitude;
                amplitudeSum += amplitude;

                frequency *= 2;
                amplitude /= 2;
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
            float dx = (2 * position.x / Terrain.Instance.UnitsPerSide - 1) - m_LandmassCenter.x;
            float dz = (2 * position.z / Terrain.Instance.UnitsPerSide - 1) - m_LandmassCenter.y;

            // can be replaced with other falloff function
            float squareBump = 1 - (1 - (dx * dx)) * (1 - (dz * dz));

            return squareBump * m_FalloffScaleFactor;
        }
    }
}