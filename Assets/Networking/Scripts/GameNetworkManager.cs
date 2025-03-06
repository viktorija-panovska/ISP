using UnityEngine;

namespace Populous
{
    public class GameNetworkManager : MonoBehaviour
    {
        private static GameNetworkManager m_Instance;
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
    } 
}