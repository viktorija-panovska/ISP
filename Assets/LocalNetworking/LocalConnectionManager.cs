using Steamworks;
using System;
using System.Collections.Generic;
using System.Text;
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


    public class LocalConnectionManager : MonoBehaviour
    {
        public static LocalConnectionManager Instance;

        private const int MAX_PLAYERS = 2;
        private const int MAX_CONNECTION_PAYLOAD = 1024;

        private string serverPassword;
        private bool gameInProgress;

        private Dictionary<string, PlayerData> clientData;
        private Dictionary<ulong, string> clientIdToPlayerId;
        private Dictionary<ulong, int> clientSceneMap;          // which client is in which scene



        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.ConnectionApprovalCallback += ApprovalCheck;
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
            NetworkManager.Singleton.ConnectionApprovalCallback -= ApprovalCheck;
            NetworkManager.Singleton.OnServerStarted -= OnServerStart;
        }

        public void OnApplicationQuit()
        {
            Disconnect();
        }

        public void Disconnect()
        {
            if (NetworkManager.Singleton == null)
                return;

            //if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.SceneManager != null)
            //    NetworkManager.Singleton.SceneManager.OnSceneEvent -= SceneLoader.Instance.HandleSceneEvent;

            if (NetworkManager.Singleton.IsHost)
                OnHostDisconnectRequest();

            else if (NetworkManager.Singleton.IsClient)
                OnClientDisconnectRequest();
        }


        #region Getters

        public PlayerData? GetPlayerData(ulong clientId)
        {
            if (clientIdToPlayerId.TryGetValue(clientId, out string clientGuid))
                if (clientData.TryGetValue(clientGuid, out PlayerData playerData))
                    return playerData;

            return null;
        }

        public string GetServerPassword() => serverPassword;

        #endregion



        #region Scene Management

        private void GoToMainMenu()
        {
            if (SceneManager.GetActiveScene().name == "MainMenu")
                return;

            SceneManager.LoadScene("MainMenu");
        }

        public void StartGame()
        {
            gameInProgress = true;

            SceneLoader.Instance.SwitchToScene(Scene.GAMEPLAY_SCENE);
        }

        #endregion



        #region Host

        public void StartHost(string password)
        {
            serverPassword = password;
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

            //GameData.Instance.AddPlayerInfo(new PlayerInfo(NetworkManager.Singleton.LocalClientId, SteamClient.SteamId.Value, Faction.RED));

            SceneLoader.Instance.SwitchToScene(Scene.LOBBY);
        }

        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            byte[] connectionData = request.Payload;
            ulong clientId = request.ClientNetworkId;

            if (connectionData.Length > MAX_CONNECTION_PAYLOAD)
            {
                response.Approved = false;
                return;
            }

            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                response.Approved = true;
                return;
            }

            string payload = Encoding.UTF8.GetString(connectionData);
            var connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload);

            if (connectionPayload.password != serverPassword)
            {
                response.Approved = false;
                return;
            }

            if (!gameInProgress && clientData.Count < MAX_PLAYERS)
            {
                clientIdToPlayerId[clientId] = connectionPayload.playerId;
                clientData[connectionPayload.playerId] = new PlayerData(connectionPayload.playerName, clientId);

                clientSceneMap[clientId] = connectionPayload.clientScene;

                response.Approved = true;
                return;
            }

            KickClient(clientId);
        }

        public void OnHostDisconnectRequest()
        {
            foreach (var pair in clientSceneMap)
                if (NetworkManager.Singleton.LocalClientId != pair.Key)
                    KickClient(pair.Key);

            NetworkManager.Singleton.Shutdown();
            ClearAllClientData();

            SceneLoader.Instance.SwitchToScene(Scene.MAIN_MENU);
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

        public void KickClient(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsHost)
                return;

            NetworkManager.Singleton.DisconnectClient(clientId);
        }

        #endregion



        #region Client

        public void StartClient(string password)
        {
            string payload = JsonUtility.ToJson(new ConnectionPayload()
            {
                playerId = Guid.NewGuid().ToString(),
                clientScene = SceneManager.GetActiveScene().buildIndex,
                playerName = PlayerPrefs.GetString("PlayerName", "Missing Name"),
                password = password
            });

            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            NetworkManager.Singleton.NetworkConfig.ConnectionData = payloadBytes;
            NetworkManager.Singleton.StartClient();
        }

        public void OnClientConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId)
                return;

            if (!NetworkManager.Singleton.IsHost)
                GameData.Instance.AddPlayerInfo_ServerRpc(NetworkManager.Singleton.LocalClientId, SteamClient.SteamId, Faction.BLUE);

            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            NetworkManager.Singleton.SceneManager.OnSceneEvent += SceneLoader.Instance.HandleSceneEvent;
        }


        private void OnClientDisconnect(ulong clientId)
        {
            if (NetworkManager.Singleton.IsHost)
            {
                //GameData.Instance.RemovePlayerInfo(clientId);   
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
                        SceneLoader.Instance.SwitchToScene(Scene.LOBBY);
                        gameInProgress = false;
                    }
                }

                return;
            }

            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            GoToMainMenu();
        }


        public void OnClientDisconnectRequest()
        {
            NetworkManager.Singleton.Shutdown();
            GoToMainMenu();
        }

        #endregion
    }
}