using UnityEngine;


namespace Populous
{
    public class Water : MonoBehaviour
    {
        private static Water m_Instance;
        /// <summary>
        /// Gets an instance of the class.
        /// </summary>
        public static Water Instance { get => m_Instance; }


        #region MonoBehavior

        private void Awake()
        {
            if (m_Instance != null)
                Destroy(gameObject);

            m_Instance = this;
        }

        #endregion


        /// <summary>
        /// Sets the size and position of the water.
        /// </summary>
        public void SetupWater()
        {
            float newSize = Terrain.Instance.UnitsPerSide;
            Vector3 size = GetComponent<Renderer>().bounds.size;

            Vector3 newScale = transform.localScale;
            newScale.x = newSize * newScale.x / size.x;
            newScale.z = newSize * newScale.z / size.z;

            transform.localScale = newScale;
            transform.position = new Vector3(Terrain.Instance.UnitsPerSide / 2, 3, Terrain.Instance.UnitsPerSide / 2);
        }


        public void Raise()
        {
            transform.position += Vector3.up * Terrain.Instance.StepHeight;
        }
    }
}