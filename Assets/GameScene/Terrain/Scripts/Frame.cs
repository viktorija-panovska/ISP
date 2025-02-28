using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Frame</c> class controls the behavior of the object surrounding the generated terrain.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Frame : MonoBehaviour
    {
        private static Frame m_Instance;
        /// <summary>
        /// Gets a singleton instance of the class.
        /// </summary>
        public static Frame Instance { get => m_Instance; }


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
        /// Sets the size and position of the frame based on the size of the terrain.
        /// </summary>
        public void Create()
        {
            float newSize = Terrain.Instance.UnitsPerSide * 2;
            Vector3 size = GetComponent<Renderer>().bounds.size;

            Vector3 newScale = transform.localScale;
            newScale.x = newSize * newScale.x / size.x;
            newScale.z = newSize * newScale.z / size.z;

            transform.localScale = newScale * 5;
            transform.position = new Vector3(Terrain.Instance.UnitsPerSide / 2, -1, Terrain.Instance.UnitsPerSide / 2);
        }
    }
}