using UnityEngine;
using Random = System.Random;


namespace Populous
{
    /// <summary>
    /// The <c>INoiseGenerator</c> interface defines methods necessary for classes which generate a height map.
    /// </summary>
    public interface INoiseGenerator
    {
        /// <summary>
        /// Sets up the initial properties of the generator.
        /// </summary>
        public void Setup();

        /// <summary>
        /// Computes the height at a given position using the Perlin Noise function and a falloff function.
        /// </summary>
        /// <param name="position">A <c>Vector3</c> representing the position on the terrain that the height should be computed for.</param>
        /// <returns>A <c>float</c> between 0 and 1 representing the height of the terrain at the given position.</returns>
        public float GetNoiseAtPosition(Vector3 position);
    }


    /// <summary>
    /// The <c>PerlinNoiseGenerator</c> class generates a height map for the terrain.
    /// </summary>
    public class PerlinNoiseGenerator : MonoBehaviour, INoiseGenerator
    {
        [Tooltip("Higher values create more varied noise, and vice versa.")]
        [SerializeField] private float m_Scale = 100f;
        [Tooltip("Higher values create more complex noise, and vice versa.")]
        [SerializeField] private int m_Octaves = 2;
        [Tooltip("The factor by which the frequency of the noise increases each octave.")]
        [SerializeField] private float m_FrequencyIncreaseFactor = 20f;
        [Tooltip("The factor by which the amplitude of the noise decreases each octave.")]
        [SerializeField] private float m_AmplitudeDecreaseFactor = 20f;
        [Tooltip("Lower factors create landmasses with more land and less water, and vice versa.")]
        [SerializeField] private float m_FalloffScaleFactor = 0.5f;


        /// <summary>
        /// The seed for the random generator used in the noise generation.
        /// </summary>
        private int m_Seed;
        /// <summary>
        /// The offsets to the positions for sampling the noise for each octave.
        /// </summary>
        private Vector2[] m_Offsets;


        /// <inheritdoc />
        public void Setup()
        {
            m_Seed = !GameData.Instance ? 0 : GameData.Instance.GameSeed;
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

                frequency *= m_FrequencyIncreaseFactor;
                amplitude /= m_AmplitudeDecreaseFactor;
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
            float dx = (2 * position.x / Terrain.Instance.UnitsPerSide - 1);
            float dz = (2 * position.z / Terrain.Instance.UnitsPerSide - 1);

            // can be replaced with other falloff function
            float squareBump = 1 - (1 - (dx * dx)) * (1 - (dz * dz));

            return squareBump * m_FalloffScaleFactor;
        }
    }
}