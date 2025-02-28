using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>MinimapCamera</c> class controls the behavior of the camera capturing the minimap.
    /// </summary>
    public class MinimapCamera : MonoBehaviour
    {
        private static MinimapCamera m_Instance;
        /// <summary>
        /// Gets a singleton instance of this class.
        /// </summary>
        public static MinimapCamera Instance { get => m_Instance; }


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
        /// Places the camera in such a way so that it only captures the minimap.
        /// </summary>
        public void Setup()
        {
            transform.position = new(Terrain.Instance.UnitsPerSide / 2, 300, Terrain.Instance.UnitsPerSide / 2);
            GetComponent<Camera>().orthographicSize = Terrain.Instance.UnitsPerSide / 2;
        }
    }
}