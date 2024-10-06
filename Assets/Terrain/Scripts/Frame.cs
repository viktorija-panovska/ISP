using UnityEngine;

namespace Populous
{
    public class Frame : MonoBehaviour
    {
        private static Frame m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static Frame Instance { get => m_Instance; }


        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        #endregion


        /// <summary>
        /// Sets the size and position of the frame.
        /// </summary>
        public void SetupFrame()
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
    }
}