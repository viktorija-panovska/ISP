using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Water</c> class handles the behavior of the water plane.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class Water : MonoBehaviour
    {
        private static Water m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static Water Instance { get => m_Instance; }


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
        /// Sets the size and position of the water plane based on the size of the terrain.
        /// </summary>
        public void Create()
        {
            float newSize = Terrain.Instance.UnitsPerSide;
            Vector3 currentSize = GetComponent<Renderer>().bounds.size;

            Vector3 newScale = transform.localScale;
            newScale.x = newSize * newScale.x / currentSize.x;
            newScale.z = newSize * newScale.z / currentSize.z;

            transform.localScale = newScale;
            transform.position = new Vector3(Terrain.Instance.UnitsPerSide / 2, transform.position.y, Terrain.Instance.UnitsPerSide / 2);
        }

        /// <summary>
        /// Increases the height of the water plane by one step.
        /// </summary>
        /// <remarks>Used for the Flood Divine Intervention.</remarks>
        public void Raise() => transform.position += Vector3.up * Terrain.Instance.StepHeight;
    }
}