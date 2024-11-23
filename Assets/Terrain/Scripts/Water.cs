using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Water</c> class is a <c>MonoBehavior</c> which handles the plane representing the water level of the terrain.
    /// </summary>
    public class Water : MonoBehaviour
    {
        private static Water m_Instance;
        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        public static Water Instance { get => m_Instance; }


        private void Awake()
        {
            if (m_Instance)
                Destroy(gameObject);

            m_Instance = this;
        }


        /// <summary>
        /// Sets the currentSize and position of the water plane based on the currentSize of the terrain.
        /// </summary>
        public void Create()
        {
            float newSize = Terrain.Instance.UnitsPerSide;
            Vector3 currentSize = GetComponent<Renderer>().bounds.size;

            Vector3 newScale = transform.localScale;
            newScale.x = newSize * newScale.x / currentSize.x;
            newScale.z = newSize * newScale.z / currentSize.z;

            transform.localScale = newScale;
            transform.position = new Vector3(Terrain.Instance.UnitsPerSide / 2, 1, Terrain.Instance.UnitsPerSide / 2);
        }


        /// <summary>
        /// Increases the height of the water plane by one step.
        /// </summary>
        /// <remarks>Used for the Flood power.</remarks>
        public void Raise() => transform.position += Vector3.up * Terrain.Instance.StepHeight;
    }
}