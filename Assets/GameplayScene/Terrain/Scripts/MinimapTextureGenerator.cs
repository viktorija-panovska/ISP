using UnityEngine;
using UnityEngine.UI;

namespace Populous
{
    /// <summary>
    /// The <c>Minimap</c> class manages the texture of the minimap.
    /// </summary>
    public class MinimapTextureGenerator : MonoBehaviour
    {
        [Tooltip("The image that the minimap should be projected on.")]
        [SerializeField] private RawImage m_Target;
        [Tooltip("The color of the water on the minimap.")]
        [SerializeField] private Color m_WaterColor;
        [Tooltip("The colors of the terrain, one for each height step from lowest to highest.")]
        [SerializeField] private Color[] m_LandColors;

        private static MinimapTextureGenerator m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static MinimapTextureGenerator Instance { get => m_Instance; }

        /// <summary>
        /// The texture of the minimap, created in code.
        /// </summary>
        private Texture2D m_MinimapTexture;


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
        }

        /// <summary>
        /// Creates the texture for the minimap based on the heights of the terrain.
        /// </summary>
        public void Create()
        {
            // as many pixels as there are points on the terrain
            m_MinimapTexture = new(Terrain.Instance.TilesPerSide + 1, Terrain.Instance.TilesPerSide + 1);
            m_MinimapTexture.filterMode = FilterMode.Point;
            m_MinimapTexture.wrapMode = TextureWrapMode.Clamp;
            SetTexture();

            m_Target.texture = m_MinimapTexture;
        }

        /// <summary>
        /// Gets colors based on the heights of the terrain and applies them to the texture.
        /// </summary>
        public void SetTexture()
        {
            Color32[] colors = new Color32[m_MinimapTexture.width * m_MinimapTexture.height];

            Debug.LogWarning(Terrain.Instance.StepHeight);

            for (int z = 0; z <= Terrain.Instance.TilesPerSide; ++z)
            {
                for (int x = 0; x <= Terrain.Instance.TilesPerSide; ++x)
                {
                    int height = new TerrainPoint(x, z).GetHeight();
                    colors[z * m_MinimapTexture.width + x] = height > Terrain.Instance.WaterLevel
                        ? m_LandColors[height / Terrain.Instance.StepHeight]
                        : m_WaterColor
                    ;
                }
            }

            m_MinimapTexture.SetPixels32(colors);
            m_MinimapTexture.Apply();
        }

        /// <summary>
        /// Updates the colors in a section of the texture between the given points.
        /// </summary>
        /// <param name="bottomLeft">The bottom-left corner of a rectangular area containing all modified terrain points.</param>
        /// <param name="topRight">The top-right corner of a rectangular area containing all modified terrain points.</param>
        public void UpdateTextureInArea(TerrainPoint bottomLeft, TerrainPoint topRight)
        {
            Color32[] colors = m_MinimapTexture.GetPixels32();

            for (int z = bottomLeft.Z; z <= topRight.Z; ++z)
            {
                for (int x = bottomLeft.X; x <= topRight.X; ++x)
                {
                    int height = new TerrainPoint(x, z).GetHeight();
                    colors[z * m_MinimapTexture.width + x] = height > Terrain.Instance.WaterLevel
                        ? m_LandColors[height / Terrain.Instance.StepHeight]
                        : m_WaterColor
                    ;
                }
            }

            m_MinimapTexture.SetPixels32(colors);
            m_MinimapTexture.Apply();
        }
    }
}