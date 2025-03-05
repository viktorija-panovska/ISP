using Unity.Netcode;

namespace Populous
{
    public class ClientDisconnector : NetworkBehaviour
    {
        private static ClientDisconnector m_Instance;
        public static ClientDisconnector Instance { get => m_Instance; }

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

        [ClientRpc]
        public void Disconnect_ClientRpc(ClientRpcParams clientRpcParams = default)
            => ConnectionManager.Instance.Disconnect();
    }
}