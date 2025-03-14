// TODO: Delete script before final

using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Populous
{
    public struct LobbyPlayerState : INetworkSerializable, IEquatable<LobbyPlayerState>
    {
        public ulong ClientId;
        public FixedString32Bytes PlayerName;
        public bool IsReady;

        public LobbyPlayerState(ulong clientId, FixedString32Bytes playerName, bool isReady)
        {
            ClientId = clientId;
            PlayerName = playerName;
            IsReady = isReady;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsReady);
        }

        public bool Equals(LobbyPlayerState other)
        {
            return ClientId == other.ClientId && PlayerName.Equals(other.PlayerName) && IsReady == other.IsReady;
        }
    }

    public struct PlayerData
    {
        public string PlayerName { get; private set; }
        public ulong PlayerId { get; private set; }

        public PlayerData(string playerName, ulong clientId)
        {
            PlayerName = playerName;
            PlayerId = clientId;
        }
    }

    [Serializable]
    public class ConnectionPayload
    {
        public string playerId;
        public int clientScene = -1;
        public string playerName;
        public string password;
    }


    public class LocalConnectionManager : MonoBehaviour, IConnectionManager
    {
        private static LocalConnectionManager m_Instance;
        public static LocalConnectionManager Instance { get => m_Instance; }

        private bool gameInProgress;

        private Dictionary<string, PlayerData> clientData;
        private Dictionary<ulong, string> clientIdToPlayerId;
        private Dictionary<ulong, int> clientSceneMap;          // which client is in which scene


        #region Event Functions

        private void Awake()
        {
            if (m_Instance && m_Instance != this)
                Destroy(m_Instance.gameObject);

            m_Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnServerStarted += OnServerStart;

            clientData = new Dictionary<string, PlayerData>();
            clientIdToPlayerId = new Dictionary<ulong, string>();
            clientSceneMap = new Dictionary<ulong, int>();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton == null)
                return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnServerStarted -= OnServerStart;
        }

        public void OnApplicationQuit()
        {
            Disconnect();
        }

        #endregion


        #region Hosting a Game

        public void CreateLobby(string lobbyName, int gameSeed)
        {
            NetworkManager.Singleton.StartHost();
        }

        private void OnServerStart()
        {
            if (!NetworkManager.Singleton.IsHost)
                return;

            string clientGuid = Guid.NewGuid().ToString();
            string playerName = PlayerPrefs.GetString("PlayerName", "Missing Name");

            clientData.Add(clientGuid, new PlayerData(playerName, NetworkManager.Singleton.LocalClientId));
            clientIdToPlayerId.Add(NetworkManager.Singleton.LocalClientId, clientGuid);
            SceneLoader.Instance.SwitchToScene_Network(Scene.LOBBY);
        }

        private void OnHostDisconnectRequest()
        {
            foreach (var pair in clientSceneMap)
                if (NetworkManager.Singleton.LocalClientId != pair.Key)
                    KickClient();

            NetworkManager.Singleton.Shutdown();
            ClearAllClientData();

            SceneLoader.Instance.SwitchToScene_Network(Scene.MAIN_MENU);
            SceneManager.LoadScene("MainMenu");
        }

        private void ClearAllClientData()
        {
            clientData.Clear();
            clientIdToPlayerId.Clear();
            clientSceneMap.Clear();

            gameInProgress = false;
        }

        private void ClearClientData(ulong clientId)
        {
            if (clientIdToPlayerId.TryGetValue(clientId, out string guid))
            {
                clientIdToPlayerId.Remove(clientId);

                if (clientData[guid].PlayerId == clientId)
                    clientData.Remove(guid);
            }
        }

        #endregion


        #region Joining a Game

        public Task<Lobby[]> GetActiveLobbies() => null;

        public void JoinGame(Lobby lobby)
        {
            Debug.Log("Join Game");
            string payload = JsonUtility.ToJson(new ConnectionPayload()
            {
                playerId = Guid.NewGuid().ToString(),
                clientScene = SceneManager.GetActiveScene().buildIndex,
                playerName = PlayerPrefs.GetString("PlayerName", "Missing Name"),
                password = "test"
            });

            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
            NetworkManager.Singleton.StartClient();
        }

        private void OnClientConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
                return;

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnSceneEvent += SceneLoader.Instance.HandleSceneEvent;
        }

        private void OnClientDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                clientSceneMap.Remove(clientId);

                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
                    ClearAllClientData();
                }
                else
                {
                    ClearClientData(clientId);

                    if (gameInProgress)
                    {
                        SceneLoader.Instance.SwitchToScene_Network(Scene.LOBBY);
                        gameInProgress = false;
                    }
                }

                return;
            }

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            GoToMainMenu();
        }

        private void OnClientDisconnectRequest()
        {
            NetworkManager.Singleton.Shutdown();
            GoToMainMenu();
        }

        #endregion


        #region Scene Management

        private void GoToMainMenu()
        {
            if (SceneManager.GetActiveScene().name == "MAIN_MENU")
                return;

            SceneManager.LoadScene("MAIN_MENU");
        }

        public void StartGame()
        {
            gameInProgress = true;
            SceneLoader.Instance.SwitchToScene_Network(Scene.GAMEPLAY);
        }

        #endregion


        public void Disconnect()
        {
            if (NetworkManager.Singleton == null)
                return;

            if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= SceneLoader.Instance.HandleSceneEvent;

            if (NetworkManager.Singleton.IsHost)
                OnHostDisconnectRequest();

            else if (NetworkManager.Singleton.IsClient)
                OnClientDisconnectRequest();
        }

        public void KickClient()
        {
            throw new NotImplementedException();
        }
    }
}