using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Minimap</c> class manages the texture of the minimap.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Minimap : MonoBehaviour
    {
        private static Minimap m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static Minimap Instance { get => m_Instance; }

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

            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial.mainTexture = m_MinimapTexture;
            meshRenderer.transform.position = new Vector3(Terrain.Instance.UnitsPerSide / 2, 0, Terrain.Instance.UnitsPerSide / 2);
            GameUtils.ResizeGameObject(meshRenderer.gameObject, Terrain.Instance.UnitsPerSide);
        }


        /// <summary>
        /// Gets colors based on the heights of the terrain and applies them to the texture.
        /// </summary>
        public void SetTexture()
        {
            Color32[] colors = new Color32[m_MinimapTexture.width * m_MinimapTexture.height];

            for (int z = 0; z <= Terrain.Instance.TilesPerSide; ++z)
                for (int x = 0; x <= Terrain.Instance.TilesPerSide; ++x)
                    colors[z * m_MinimapTexture.width + x] =
                        Terrain.Instance.GetPointHeight(new(x, z)) > Terrain.Instance.WaterLevel ? Color.green : Color.blue;

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
                for (int x = bottomLeft.X; x <= topRight.X; ++x)
                    colors[z * m_MinimapTexture.width + x] =
                        Terrain.Instance.GetPointHeight(new(x, z)) > Terrain.Instance.WaterLevel ? Color.green : Color.blue;

            m_MinimapTexture.SetPixels32(colors);
            m_MinimapTexture.Apply();
        }
    }
}