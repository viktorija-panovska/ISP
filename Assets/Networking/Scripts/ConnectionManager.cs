using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;


namespace Populous
{
    /// <summary>
    /// The <c>IConnectionManager</c> interface defines the methods necessary for a class that provides network connection between a host and client.
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>
        /// Starts the process of launching a lobby with the given values.
        /// </summary>
        /// <param name="lobbyName">The name of the lobby that should be created.</param>
        /// <param name="gameSeed">The seed the created game should use to randomly generate the terrain and other game elements.</param>
        public void CreateLobby(string lobbyName, int gameSeed);

        /// <summary>
        /// Gets all the active lobbies.
        /// </summary>
        /// <returns>A <c>Task</c> containing a list the <c>Lobby</c> instances of all active lobbies.</returns>
        public Task<Lobby[]> GetActiveLobbies();

        /// <summary>
        /// Triggers the client to attempt to join the given lobby.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> the client wants to join.</param>
        public void JoinGame(Lobby lobby);

        /// <summary>
        /// Starts the game.
        /// </summary>
        public void StartGame();

        /// <summary>
        /// Disconnects the current client from the network.
        /// </summary>
        public void Disconnect();

        /// <summary>
        /// Allows the host to disconnect the client.
        /// </summary>
        public void KickClient();
    }


    /// <summary>
    /// The <c>ConnectionManager</c> class handles the connection and disconnection of the players over the network.
    /// </summary>
    public class ConnectionManager : NetworkBehaviour, IConnectionManager
    {
        [Tooltip("A reference to the Facepunch Transport component, found in the NetworkManager object.")]
        [SerializeField] private FacepunchTransport m_FacepunchTransport;

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
        /// The key in the lobby data for the name of the lobby.
        /// </summary>
        public const string LOBBY_NAME_KEY = "name";
        /// <summary>
        /// The key in the lobby data for the game seed.
        /// </summary>
        public const string LOBBY_SEED_KEY = "seed";
        /// <summary>
        /// The key in the lobby data for the Steam name of the owner of the lobby.
        /// </summary>
        public const string LOBBY_OWNER_KEY = "owner";
        /// <summary>
        /// The key in the lobby data that serves as an identifier for lobbies of this project.
        /// </summary>
        public const string LOBBY_PROJECT_CHECK_KEY = "isPopulousPlus";

        /// <summary>
        /// The lobby this client is currently in.
        /// </summary>
        private Lobby? m_CurrentLobby;

        /// <summary>
        /// The game data that has been entered by a user that wants to create a game.
        /// </summary>
        private (string name, int seed) m_EnteredGameData;


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
            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        public override void OnDestroy()
        {
            SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;

            if (NetworkManager.Singleton == null)
                return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;

            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= SceneLoader.Instance.HandleSceneEvent;

            base.OnDestroy();
        }

        private void OnApplicationQuit() => Disconnect();

        #endregion


        #region Starting a Host

        /// <inheritdoc />
        public async void CreateLobby(string lobbyName, int gameSeed)
        {
            m_EnteredGameData = (lobbyName, gameSeed);

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("Failed to start host.");
                m_EnteredGameData = ("", 0);
                return;
            }

            m_CurrentLobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
        }

        /// <summary>
        /// Called when the creation of the lobby has completed, and sets the data lobby data.
        /// </summary>
        /// <param name="result">The status of the creation of the lobby.</param>
        /// <param name="lobby">The created lobby.</param>
        private void OnLobbyCreated(Result result, Lobby lobby)
        {
            if (result != Result.OK)
            {
                Debug.LogError("Failed to create lobby.");
                Disconnect();
                return;
            }

            lobby.SetData(LOBBY_NAME_KEY, m_EnteredGameData.name);
            lobby.SetData(LOBBY_SEED_KEY, m_EnteredGameData.seed.ToString());
            lobby.SetData(LOBBY_OWNER_KEY, SteamClient.Name);
            lobby.SetData(LOBBY_PROJECT_CHECK_KEY, "true");
            lobby.SetPublic();
            lobby.SetJoinable(true);

            GameData.Instance.Setup(lobby, m_EnteredGameData.seed);
            SceneLoader.Instance.OnFadeOutComplete += GoToLobby;
            SceneLoader.Instance.FadeOut();
        }

        /// <summary>
        /// Switches the scene to the Lobby scene.
        /// </summary>
        private void GoToLobby()
        {
            SceneLoader.Instance.OnFadeOutComplete -= GoToLobby;

            NetworkManager.Singleton.SceneManager.OnSceneEvent += SceneLoader.Instance.HandleSceneEvent;
            SceneLoader.Instance.SwitchToScene_Network(Scene.LOBBY);
        }

        #endregion


        #region Starting a Client

        /// <inheritdoc />
        public async Task<Lobby[]> GetActiveLobbies()
            => await SteamMatchmaking.LobbyList.WithKeyValue(LOBBY_PROJECT_CHECK_KEY, "true").RequestAsync();

        /// <inheritdoc />
        public void JoinGame(Lobby lobby) => OnGameLobbyJoinRequested(lobby, lobby.Owner.Id);

        /// <summary>
        /// Attempts to join the given lobby, which is owned by the Steam user with the given ID.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> this client wants to join.</param>
        /// <param name="steamId">The <c>SteamId</c> of the lobby owner.</param>
        private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId steamId)
        {
            RoomEnter joinLobby = await lobby.Join();

            if (joinLobby != RoomEnter.Success)
            {
                Debug.LogError("Failed to enter lobby.");
                return;
            }

            m_CurrentLobby = lobby;
        }

        /// <summary>
        /// Called when the user has entered the given lobby, and starts the process of launching the client.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> that the user has joined.</param>
        private void OnLobbyEntered(Lobby lobby)
        {
            if (NetworkManager.Singleton.IsHost) return;

            // start the client
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

            m_FacepunchTransport.targetSteamId = m_CurrentLobby.Value.Owner.Id;

            SceneLoader.Instance.OnFadeOutComplete += StartClient;
            SceneLoader.Instance.FadeOut();
        }

        /// <summary>
        /// Launches the client.
        /// </summary>
        private void StartClient()
        {
            SceneLoader.Instance.OnFadeOutComplete -= StartClient;

            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError("Failed to start client.");
                Disconnect();
                SceneLoader.Instance.FadeIn();
                return;
            }
        }

        /// <summary>
        /// Called when the client's connection to the server is established, and sends the client to the server's scene.
        /// </summary>
        /// <param name="clientId">The ID of the client that was connected.</param>
        private void OnClientConnected(ulong clientId)
        {
            if (clientId != NetworkManager.Singleton.LocalClientId) return;
            NetworkManager.Singleton.SceneManager.OnSceneEvent += SceneLoader.Instance.HandleSceneEvent;
        }

        #endregion


        #region Launch Game

        /// <inheritdoc />
        public void StartGame() => StartGame_ClientRpc();

        /// <summary>
        /// Fades out each client in preparation for starting the game.
        /// </summary>
        [ClientRpc]
        private void StartGame_ClientRpc()
        {
            SceneLoader.Instance.OnFadeOutComplete += OnGameStartReady;
            SceneLoader.Instance.FadeOut();
        }

        /// <summary>
        /// Called when the game is prepared to start, transports the players to the game scene.
        /// </summary>
        private void OnGameStartReady()
        {
            SceneLoader.Instance.OnFadeOutComplete -= OnGameStartReady;

            if (!IsHost) return;
            SceneLoader.Instance.SwitchToScene_Network(Scene.GAMEPLAY);
        }

        #endregion


        #region Disconnect

        /// <inheritdoc />
        public void Disconnect()
        {
            m_CurrentLobby?.Leave();

            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;

            if (NetworkManager.Singleton.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnSceneEvent -= SceneLoader.Instance.HandleSceneEvent;

            NetworkManager.Singleton.Shutdown(true);
            GameNetworkManager.Instance.Destroy();

            SceneLoader.Instance.SwitchToScene_Local(Scene.MAIN_MENU);
        }

        /// <summary>
        /// Disconnects a player on client-side.
        /// </summary>
        [ClientRpc]
        private void Disconnect_ClientRpc(ClientRpcParams clientRpcParams = default) => Disconnect();

        /// <inheritdoc />
        public void KickClient()
        {
            if (!IsHost) return;

            PlayerInfo? clientInfo = GameData.Instance.GetClientPlayerInfo();
            if (!clientInfo.HasValue) return;
            Disconnect_ClientRpc(GameUtils.GetClientParams(clientInfo.Value.NetworkId));
        }

        /// <summary>
        /// Called on the host when the client disconnects and on the client when the host forcefully disconnects it.
        /// </summary>
        /// <param name="networkId">The network ID of the user that invoked this method: the network ID of the disconnected client
        /// on the host and the network ID of the host on the client that is being forcefully disconnected.</param>
        private void OnClientDisconnect(ulong networkId)
        {
            // The host is being informed that the client has disconnected.
            if (IsHost && networkId != NetworkManager.Singleton.LocalClientId)
            {
                GameData.Instance.RemoveClientInfo();

                if (SceneLoader.Instance.GetHostScene() == Scene.GAMEPLAY)
                    SceneLoader.Instance.SwitchToScene_Network(Scene.LOBBY);

                return;
            }

            // The client is being informed that it has been disconnected by the host.
            Disconnect();
        }

        #endregion
    }
}