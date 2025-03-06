using UnityEngine;

namespace Populous
{
    /// <summary>
    /// The <c>GameNetworkManager</c> class ensures that the <c>NetworkManager</c> persists through scenes.
    /// </summary>
    public class GameNetworkManager : MonoBehaviour
    {
        private static GameNetworkManager m_Instance;
        /// <summary>
        /// Gets a singleton instance of this class.
        /// </summary>
        public static GameNetworkManager Instance { get => m_Instance; }


        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(m_Instance.gameObject);
                return;
            }

            m_Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Destroys the <c>NetworkManager</c> GameObject.
        /// </summary>
        public void Destroy() => Destroy(gameObject);
    } 
}