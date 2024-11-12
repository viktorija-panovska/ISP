using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>Frame</c> class is a <c>MonoBehavior</c> which represents the frame surrounding the generated terrain.
    /// </summary>
    public class Frame : MonoBehaviour
    {
        private static Frame m_Instance;
        /// <summary>
        /// Gets the singleton instance of the class.
        /// </summary>
        public static Frame Instance { get => m_Instance; }


        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

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

            GetComponent<BoxCollider>().center = transform.position;
            GetComponent<BoxCollider>().size = new Vector3(newSize, 0.1f, newSize);
        }

        /// <summary>
        /// Increases the height of the frame by one step.
        /// </summary>
        /// <remarks>Used for the Flood power.</remarks>
        public void Raise() => transform.position += Vector3.up * Terrain.Instance.StepHeight;
    }
}