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
    /// The <c>IConnectionManager</c> interface defines the methods necessary for a class that provides network connection between a host and client.
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>
        /// Sets the values of the game that is being started and its associated lobby, and starts the process of launching the host.
        /// </summary>
        /// <param name="lobbyName">The name of the lobby that should be created.</param>
        /// <param name="lobbyPassword">The optional password of the lobby that should be created.</param>
        /// <param name="gameSeed">The seed the created game should use to randomly generate the terrain and other game elements.</param>
        public void CreateLobby(string lobbyName, string lobbyPassword, string gameSeed);

        /// <summary>
        /// Gets all the active lobbies.
        /// </summary>
        /// <returns>A <c>Task</c> containing a list the <c>Lobby</c> instances of all active lobbies.</returns>
        public Task<Lobby[]> GetActiveLobbies();

        /// <summary>
        /// Triggers the client to attempt to join the given lobby.
        /// </summary>
        /// <param name="lobby">The <c>Lobby</c> the client wants to join.</param>
        /// <param name="password">The password entered by the client, empty string if no password is entered.</param>
        public void JoinGame(Lobby lobby, string password);

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
        /// Limits the amount of data that can be sent during the connection of a client to a server, so it provides light protection against DOS attacks.
        /// </summary>
        private const int MAX_CONNECTION_PAYLOAD = 1024;

        /// <summary>
        /// The lobby this client is currently in.
        /// </summary>
        private Lobby? m_CurrentLobby;

        /// <summary>
        /// The game data that has been entered by a user that wants to create a game.
        /// </summary>
        private (string name, string password, string seed) m_EnteredGameData;


        #region Event Functions

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

        private void Start()
        {
            SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
            SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
            SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
        }

        public override void OnDestroy()
        {
            Debug.Log("OnDestroy");
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
        public void CreateLobby(string lobbyName, string lobbyPassword, string gameSeed)
        {
            m_EnteredGameData = (lobbyName, lobbyPassword, gameSeed);

            ScreenFader.Instance.OnFadeOutComplete += StartHost;
            ScreenFader.Instance.FadeOut();
        }

        /// <summary>
        /// Launches the host and creates the lobby associated with the started game.
        /// </summary>
        private async void StartHost()
        {
            ScreenFader.Instance.OnFadeOutComplete -= StartHost;

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

            if (!NetworkManager.Singleton.StartHost())
            {
                Debug.LogError("Host Start Failed");
                m_EnteredGameData = ("", "", "");
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
                Debug.LogError("Lobby wasn't created");
                Disconnect();
                return;
            }

            lobby.SetData("name", m_EnteredGameData.name);
            lobby.SetData("password", m_EnteredGameData.password);
            lobby.SetData("seed", m_EnteredGameData.seed);
            lobby.SetData("owner", SteamClient.Name);
            lobby.SetData("isPopulous", "true");
            lobby.SetPublic();
            lobby.SetJoinable(true);

            int gameSeed = int.TryParse(m_EnteredGameData.seed, out int seed) ? seed : new Random().Next();
            GameData.Instance.Setup(lobby, gameSeed);

            SceneLoader.Instance.SwitchToScene_Network(Scene.LOBBY);
        }

        #endregion


        #region Starting a Client

        /// <inheritdoc />
        public async Task<Lobby[]> GetActiveLobbies() => await SteamMatchmaking.LobbyList.RequestAsync();

        /// <inheritdoc />
        public void JoinGame(Lobby lobby, string password)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(password);
            OnGameLobbyJoinRequested(lobby, lobby.Owner.Id);
        }

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
                Debug.LogError("Failed to enter lobby");
                NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes("");
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

            ScreenFader.Instance.OnFadeOutComplete += StartClient;
            ScreenFader.Instance.FadeOut();
        }

        /// <summary>
        /// Launches the client.
        /// </summary>
        private void StartClient()
        {
            ScreenFader.Instance.OnFadeOutComplete -= StartClient;

            if (!NetworkManager.Singleton.StartClient())
            {
                Debug.LogError("Client Start Failed");
                NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes("");
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
        public void StartGame()
        {
            ScreenFader.Instance.OnFadeOutComplete += OnGameStartReady;
            ScreenFader.Instance.FadeOut();
        }

        /// <summary>
        /// Called when the game is prepared to start, transports the players to the game scene.
        /// </summary>
        private void OnGameStartReady()
        {
            ScreenFader.Instance.OnFadeOutComplete -= OnGameStartReady;

            if (!IsHost) return;
            SceneLoader.Instance.SwitchToScene_Network(Scene.GAMEPLAY_SCENE);
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
            SceneLoader.Instance.SwitchToScene_Local(Scene.MAIN_MENU);

            Destroy(GameNetworkManager.Instance.gameObject);
            Destroy(gameObject);

            Debug.Log("Disconnected");
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
            Debug.Log("OnClientDisconnect: " + networkId);

            // The host is being informed that the client has disconnected.
            if (IsHost && networkId != NetworkManager.Singleton.LocalClientId)
            {
                Debug.Log("--- Client has disconnected");
                GameData.Instance.RemoveClientInfo();

                if (SceneLoader.Instance.GetCurrentScene() == Scene.GAMEPLAY_SCENE)
                    SceneLoader.Instance.SwitchToScene_Network(Scene.LOBBY);
            }

            // The client is being informed that it has been disconnected by the host.
            if (!IsHost)
            {
                Debug.Log("--- Host disconnected client");

                if (SceneLoader.Instance.GetCurrentScene() == Scene.MAIN_MENU)
                {
                    //if (NetworkManager.Singleton.DisconnectReason != "")
                    //    MainMenu.Instance.SetConnectionDeniedReason(NetworkManager.Singleton.DisconnectReason);

                    ScreenFader.Instance.FadeIn();
                    return;
                }

                Disconnect();
            }
        }

        #endregion
    }
}