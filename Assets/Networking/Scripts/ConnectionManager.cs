using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

using Random = System.Random;

namespace Populous
{
    /// <summary>
    /// The <c>ConnectionManager</c> class handles the connection and disconnection of the players over the network.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        [Tooltip("Set to true for testing purposes.")]
        [SerializeField] private bool m_LocalConnection;

        private static ConnectionManager m_Instance;
        /// <summary>
        /// Gets a singleton instance of this class.
        /// </summary>
        public static ConnectionManager Instance { get => m_Instance; }

        /// <summary>
        /// The Steam identifier of this application.
        /// </summary>
        private const uint APP_ID = 480;     // SpaceWar id
        /// <summary>
        /// The maximum number of players in the game.
        /// </summary>
        public const int MAX_PLAYERS = 2;
        /// <summary>
        /// 
        /// </summary>
        private const int MAX_CONNECTION_PAYLOAD = 1024;

        /// <summary>
        /// True if the connection is being made locally, false if the connection is over Steam.
        /// </summary>
        /// <remarks>For testing purposes only.</remarks>
        public bool LocalConnection { get => m_LocalConnection; }

        /// <summary>
        /// The lobby this client is currently in.
        /// </summary>
        private Lobby? m_CurrentLobby;


        #region Event Functions

        private void Awake()
        {
            if (m_Instance && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
            DontDestroyOnLoad(gameObject);

            // attempt to open Steam client, won't work if Steam isn't running on the machine.
            try
            {
                SteamClient.Init(APP_ID, true);
                Debug.Log("Steam is up and running.");
            }
            catch (Exception e)
            {
                Debug.Log("Steam is not running.");
            }
        }

        private void Start()
        {
            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite += OnLobbyInvite;
            SteamMatchmaking.OnLobbyGameCreated += OnLobbyGameCreated;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        private void OnDestroy()
        {
            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
            SteamMatchmaking.OnLobbyInvite -= OnLobbyInvite;
            SteamMatchmaking.OnLobbyGameCreated -= OnLobbyGameCreated;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;

            if (NetworkManager.Singleton == null)
                return;

            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApproval;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnApplicationQuit() => Disconnect();

        #endregion


        #region Hosting a Game

        public void CreateGame(string lobbyName, string lobbyPassword, string gameSeed)
        {
            Debug.Log("Create Game");

            GameData.Instance.CurrentLobbyInfo = new LobbyInfo(lobbyName, lobbyPassword);
            GameData.Instance.GameSeed = gameSeed.Length > 0 ? int.Parse(gameSeed) : new Random().Next(0, int.MaxValue);

            ScreenFader.Instance.OnFadeOutComplete += StartHost;
            ScreenFader.Instance.FadeOut();
        }

        private async void StartHost()
        {
            Debug.Log("Start Host");
            ScreenFader.Instance.OnFadeOutComplete -= StartHost;

            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApproval;
            NetworkManager.Singleton.StartHost();

            m_CurrentLobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
        }

        private void OnServerStarted()
        {
            Debug.Log("OnServerStarted");

            if (!NetworkManager.Singleton.IsHost) return;
            SceneLoader.Instance.SwitchToScene(Scene.LOBBY);
        }

        private void OnLobbyCreated(Result result, Lobby lobby)
        {
            Debug.Log("OnLobbyCreated");

            if (result != Result.OK)
            {
                Debug.Log("Lobby wasn't created");
                return;
            }

            lobby.SetData("name", GameData.Instance.CurrentLobbyInfo.LobbyName);
            lobby.SetData("password", GameData.Instance.CurrentLobbyInfo.LobbyPassword);
            lobby.SetData("seed", GameData.Instance.GameSeed.ToString());
            lobby.SetData("isISP", "true");
            lobby.SetPublic();
            lobby.SetJoinable(true);
            lobby.SetGameServer(lobby.Owner.Id);    // set game server associated with the lobby

            Debug.Log("Lobby created");
        }

        /// <summary>
        /// A game server has been associated with the lobby. Populates when the host is started.
        /// </summary>
        /// <param name="lobby"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="steamId"></param>
        private void OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId steamId)
        {
            Debug.Log("OnLobbyGameCreated");
        }

        #endregion




        #region Joining a lobby

        public async Task<Lobby[]> GetActiveLobbies()
            => await SteamMatchmaking.LobbyList.WithMaxResults(10).RequestAsync();

        public void JoinGame(SteamId steamId)
        {
            Debug.Log("Join Game");

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            GetComponent<FacepunchTransport>().targetSteamId = steamId;
            Debug.Log("Joining room hosted by " + steamId);

            ScreenFader.Instance.OnFadeOutComplete += StartClient;
            ScreenFader.Instance.FadeOut();
        }

        private void StartClient()
        {
            ScreenFader.Instance.OnFadeOutComplete -= StartClient;

            //if (m_LocalConnection)
            //{
            //    LocalConnectionManager.Instance.StartClient("test");
            //    return;
            //}

            if (NetworkManager.Singleton.StartClient())
                Debug.Log("Client has started");
        }

        private void OnLobbyEntered(Lobby lobby)
        {
            if (NetworkManager.Singleton.IsHost) return;
            Debug.Log("OnLobbyEntered");

            // how the players connect
            //JoinGame(m_CurrentLobby.Value.Owner.Id);
        }

        private void OnLobbyMemberJoined(Lobby lobby, Friend friend)
        {
            Debug.Log("OnLobbyMemberJoined");
        }

        private void OnLobbyMemberLeave(Lobby lobby, Friend friend)
        {
            Debug.Log("OnLobbyMemberLeave");
        }

        /// <summary>
        /// Called when the given friend invited this client to the given lobby.
        /// </summary>
        /// <param name="friend"></param>
        /// <param name="lobby"></param>
        private void OnLobbyInvite(Friend friend, Lobby lobby)
        {
            Debug.Log("OnLobbyInvite");
        }



        /// <summary>
        /// Called when a user tries to join the lobby hosted by this client through their friends list.
        /// </summary>
        /// <param name="lobby"></param>
        /// <param name="steamId"></param>
        private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
        {
            Debug.Log("OnGameLobbyJoinRequested");

            RoomEnter joinLobby = await lobby.Join();

            if (joinLobby != RoomEnter.Success)
            {
                Debug.Log("Failed to enter lobby");
                return;
            }

            m_CurrentLobby = lobby;
            Debug.Log("Joined lobby");
        }

        #endregion


        #region Unity Netcode


        private void OnClientDisconnected(ulong obj)
        {
            throw new NotImplementedException();
        }

        private void OnClientConnected(ulong obj)
        {
            throw new NotImplementedException();
        }


        public void Disconnect()
        {
            m_CurrentLobby?.Leave();
            if (NetworkManager.Singleton == null)
                return;

            if (NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
                NetworkManager.Singleton.ConnectionApprovalCallback -= OnConnectionApproval;
            }
            else
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            NetworkManager.Singleton.Shutdown(true);
            Debug.Log("Disconnected");
        }

        #endregion










        public void StartGame()
        {
            ScreenFader.Instance.OnFadeOutComplete += OnGameStartReady;
            ScreenFader.Instance.FadeOut();
        }

        private void OnGameStartReady()
        {
            SceneLoader.Instance.SwitchToScene(Scene.GAME_SCENE);
        }


        #region Hosting








        private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            ulong clientId = request.ClientNetworkId;

            // If there is no password, there is no need to check anything. Host can enter automatically
            if (clientId == NetworkManager.ServerClientId || string.IsNullOrEmpty(GameData.Instance.CurrentLobbyInfo.LobbyPassword))
            {
                response.Approved = true;
                return;
            }

            byte[] connectionData = request.Payload;

            if (connectionData.Length > MAX_CONNECTION_PAYLOAD)
            {
                response.Approved = false;
                return;
            }

            string payload = Encoding.UTF8.GetString(connectionData);
            var connectionPayload = JsonUtility.FromJson<ConnectionPayload>(payload);

            if (connectionPayload.password != GameData.Instance.CurrentLobbyInfo.LobbyPassword)
            {
                response.Approved = false;
                return;
            }

            if (m_CurrentLobby.Value.MemberCount < MAX_PLAYERS)
            {
                response.Approved = true;
                return;
            }

            NetworkManager.Singleton.DisconnectClient(clientId);
        }

        #endregion


        #region Joining





        private void OnClientConnectedCallback(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            NetworkManager.Singleton.SceneManager.OnSceneEvent += SceneLoader.Instance.HandleSceneEvent;
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {

        }

        #endregion


        #region Leaving

        //public void Disconnect()
        //{
        //    if (Local)
        //    {
        //        LocalConnectionManager.Instance.Disconnect();
        //    }

        //    //if (NetworkManager.Singleton == null)
        //    //    return;

        //    //if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.SceneManager != null)
        //    //    NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;

        //    //if (NetworkManager.Singleton.IsHost)
        //    //    OnHostDisconnectRequest();

        //    //else if (NetworkManager.Singleton.IsClient)
        //    //    OnClientDisconnectRequest();
        //}

        public void KickClient()
        {

        }

        #endregion

    }
}