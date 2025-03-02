using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
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
        private static ConnectionManager m_Instance;
        /// <summary>
        /// Gets a singleton instance of this class.
        /// </summary>
        public static ConnectionManager Instance { get => m_Instance; }

        /// <summary>
        /// The maximum number of players in the game.
        /// </summary>
        public const int MAX_PLAYERS = 2;
        /// <summary>
        /// Limits the amount of data that can be sent during the connection of a client to a server, so it provides light protection against DOS attacks.
        /// </summary>
        private const int MAX_CONNECTION_PAYLOAD = 1024;

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
        }

        private void Start()
        {
            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        private void OnDestroy()
        {
            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
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


        #region Starting a Host

        /// <summary>
        /// Sets the values of the game that is being started and its associated lobby, and starts the process of launching the host.
        /// </summary>
        /// <param name="lobbyName">The name of the lobby that should be created.</param>
        /// <param name="lobbyPassword">The optional password of the lobby that should be created.</param>
        /// <param name="gameSeed">The seed the created game should use to randomly generate the terrain and other game elements.</param>
        public void CreateLobby(string lobbyName, string lobbyPassword, string gameSeed)
        {
            Debug.Log("Create Game");

            GameData.Instance.CurrentLobbyInfo = new LobbyInfo(lobbyName, lobbyPassword);
            GameData.Instance.GameSeed = int.TryParse(gameSeed, out int seed) ? seed : new Random().Next(0, int.MaxValue);

            ScreenFader.Instance.OnFadeOutComplete += StartHost;
            ScreenFader.Instance.FadeOut();
        }

        /// <summary>
        /// Launches the host and creates the lobby associated with the started game.
        /// </summary>
        private async void StartHost()
        {
            Debug.Log("Start Host");

            ScreenFader.Instance.OnFadeOutComplete -= StartHost;

            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.ConnectionApprovalCallback += OnConnectionApproval;
            NetworkManager.Singleton.StartHost();

            m_CurrentLobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
        }

        /// <summary>
        /// Called when the creation of the server has completed, and triggers the transfer to the lobby scene.
        /// </summary>
        private void OnServerStarted()
        {
            Debug.Log("OnServerStarted");

            if (!NetworkManager.Singleton.IsHost) return;
            SceneLoader.Instance.SwitchToScene(Scene.LOBBY);
        }

        /// <summary>
        /// Called when the creation of the lobby has completed, and sets the data lobby data.
        /// </summary>
        /// <param name="result">The status of the creation of the lobby.</param>
        /// <param name="lobby">The created lobby.</param>
        private void OnLobbyCreated(Result result, Lobby lobby)
        {
            Debug.Log("OnLobbyCreated");

            if (result != Result.OK)
            {
                Debug.Log("Lobby wasn't created");
                return;
            }

            Debug.Log("Lobby created");

            lobby.SetData("name", GameData.Instance.CurrentLobbyInfo.LobbyName);
            lobby.SetData("password", GameData.Instance.CurrentLobbyInfo.LobbyPassword);
            lobby.SetData("seed", GameData.Instance.GameSeed.ToString());
            lobby.SetData("isPopulous", "true");
            lobby.SetPublic();
            lobby.SetJoinable(true);
        }

        #endregion


        #region Starting a Client

        /// <summary>
        /// Gets all the active lobbies.
        /// </summary>
        /// <returns>A <c>Task</c> containing a list the <c>Lobby</c> instances of all active lobbies.</returns>
        public async Task<Lobby[]> GetActiveLobbies()
            => await SteamMatchmaking.LobbyList.WithMaxResults(10).RequestAsync();

        /// <summary>
        /// Triggers the client to attempt to join the given lobby.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> the client wants to join.</param>
        public void JoinLobby(Lobby lobby) => OnGameLobbyJoinRequested(lobby, lobby.Owner.Id);

        /// <summary>
        /// Attempts to join the given lobby, which is owned by the Steam user with the given ID.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> this client wants to join.</param>
        /// <param name="steamId">The <c>SteamId</c> of the lobby owner.</param>
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

        /// <summary>
        /// Called when the user has entered the given lobby, and starts the process of launching the client.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> that the user has joined.</param>
        private void OnLobbyEntered(Lobby lobby)
        {
            if (NetworkManager.Singleton.IsHost) return;
            Debug.Log("OnLobbyEntered");

            // start the client
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            GetComponent<FacepunchTransport>().targetSteamId = m_CurrentLobby.Value.Owner.Id;
            Debug.Log("Joining room hosted by " + m_CurrentLobby.Value.Owner.Id);

            ScreenFader.Instance.OnFadeOutComplete += StartClient;
            ScreenFader.Instance.FadeOut();
        }

        /// <summary>
        /// Launches the client.
        /// </summary>
        private void StartClient()
        {
            ScreenFader.Instance.OnFadeOutComplete -= StartClient;

            if (NetworkManager.Singleton.StartClient())
                Debug.Log("Client has started");
        }

        /// <summary>
        /// Called when the client's connection to the server is established, and sends the client to the server's scene.
        /// </summary>
        /// <param name="clientId">The ID of the client that was connected.</param>
        private void OnClientConnected(ulong clientId)
        {
            Debug.Log("OnClientConnected");
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            NetworkManager.Singleton.SceneManager.OnSceneEvent += SceneLoader.Instance.HandleSceneEvent;
        }

        #endregion


        #region Connection Control

        /// <summary>
        /// Checks whether the client has entered the correct password for the lobby on the server.
        /// </summary>
        /// <param name="request">The data of the client requesting a connection.</param>
        /// <param name="response">The response from the server, granting or denying the connection.</param>
        private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            Debug.Log("OnConnectionApproval");
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





        #region Disconnect

        private void OnLobbyMemberLeave(Steamworks.Data.Lobby lobby, Friend friend)
        {
            Debug.Log("OnLobbyMemberLeave");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log("OnClientDisconnect");
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
                //NetworkManager.Singleton.SceneManager.OnSceneEvent -= SceneLoader.Instance.HandleSceneEvent;
            }

            NetworkManager.Singleton.Shutdown(true);
            Debug.Log("Disconnected");
        }

        public void KickClient(ulong clientId)
        {

        }


        #endregion


        #region Launch Game

        public void StartGame()
        {
            ScreenFader.Instance.OnFadeOutComplete += OnGameStartReady;
            ScreenFader.Instance.FadeOut();
        }

        private void OnGameStartReady()
        {
            SceneLoader.Instance.SwitchToScene(Scene.GAME_SCENE);
        }

        #endregion
    }
}