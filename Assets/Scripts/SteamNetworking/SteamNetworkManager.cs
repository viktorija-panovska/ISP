using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using System.Text;


public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance { get; private set; }

    // Connection Information
    private const int MAX_PLAYERS = 2;
    private const int MAX_CONNECTION_PAYLOAD = 1024;
    private byte[] clientPayload;

    // Lobby Information
    public Lobby? CurrentLobby { get; private set; }
    private (string, string)[] lobbyData;

    // Game State
    private bool gameInProgress;



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
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        //SteamMatchmaking.OnLobbyMemberJoined += OnLobbyMemberJoined;
        //SteamMatchmaking.OnLobbyMemberLeave += OnLobbyMemberLeave;
        //SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
    }

    private void OnDestroy()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        //SteamMatchmaking.OnLobbyMemberJoined -= OnLobbyMemberJoined;
        //SteamMatchmaking.OnLobbyMemberLeave -= OnLobbyMemberLeave;
        //SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;

        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.ConnectionApprovalCallback -= ConnectionApprovalCallback;
        NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectCallback;
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }



    #region Scene Management

    private void OnSceneEvent(SceneEvent sceneEvent)
    {
        if (sceneEvent.SceneEventType != SceneEventType.LoadComplete)
            return;
    }

    private void LoadMainMenu()
    {
        if (SceneManager.GetActiveScene().name == "MainMenu") return;
        SceneManager.LoadScene("MainMenu");
    }

    private void LoadLobby()
    {
        if (SceneManager.GetActiveScene().name == "Lobby") return;
        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    private void LoadGameScene()
    {
        if (SceneManager.GetActiveScene().name == "GameScene") return;
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    #endregion



    #region Host a Game

    public async void StartHost(string serverName, string password = "", string mapSeed = "")
    {
        lobbyData = new (string, string)[]
        {
            ("isISP", "yes"),
            ("serverName", serverName),
            ("password", password),
            ("mapSeed", mapSeed)
        };

        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.ConnectionApprovalCallback += ConnectionApprovalCallback;

        NetworkManager.Singleton.StartHost();
        CurrentLobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
    }

    private void OnServerStarted()
    {
        if (!NetworkManager.Singleton.IsHost) return;
        Debug.Log("OnServerStarted");
        //LoadLobby();
    }

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK) return;
        Debug.Log("OnLobbyCreated");

        foreach ((string key, string value) in lobbyData)
            lobby.SetData(key, value);

        lobby.SetPublic();
        lobby.SetJoinable(true);
    }

    private void ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        Debug.Log("ConnectionApprovalCallback");
        ulong clientId = request.ClientNetworkId;

        // If there is no password, there is no need to check anything. Host can enter automatically
        if (clientId == NetworkManager.ServerClientId || string.IsNullOrEmpty(CurrentLobby.Value.GetData("password")))
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

        if (connectionPayload.password != CurrentLobby.Value.GetData("password"))
        {
            response.Approved = false;
            return;
        }

        if (!gameInProgress && CurrentLobby.Value.MemberCount < MAX_PLAYERS)
        {
            response.Approved = true;
            return;
        }

        KickClient(clientId);
    }

    #endregion



    #region Join a Game

    public async Task<Lobby[]> GetActiveLobbies()
        => await SteamMatchmaking.LobbyList.WithMaxResults(10).RequestAsync();


    public async void JoinLobby(SteamId lobbyId, string password)
    {
        Debug.Log("JoinLobby");
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectCallback;

        NetworkManager.Singleton.GetComponent<FacepunchTransport>().targetSteamId = lobbyId;
        Debug.Log("Joining room hosted by " + lobbyId);

        if (NetworkManager.Singleton.StartClient())
            Debug.Log("Client has started");
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        if (NetworkManager.Singleton.IsHost) return;
        Debug.Log("Lobby Entered");

        //SteamLobby.Instance.AddPlayer(SteamClient.Name);
    }

    public void OnClientConnectedCallback(ulong clientId)
    {
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        Debug.Log("Client Connected");
    }

    public void OnClientDisconnectCallback(ulong clientId)
    {

    }

    #endregion



    #region Leave a Game

    public void Disconnect()
    {
        //if (NetworkManager.Singleton == null)
        //    return;

        //if (NetworkManager.Singleton.IsHost && NetworkManager.Singleton.SceneManager != null)
        //    NetworkManager.Singleton.SceneManager.OnSceneEvent -= OnSceneEvent;

        //if (NetworkManager.Singleton.IsHost)
        //    OnHostDisconnectRequest();

        //else if (NetworkManager.Singleton.IsClient)
        //    OnClientDisconnectRequest();
    }


    public void KickClient(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsHost) return;

        NetworkManager.Singleton.DisconnectClient(clientId);
    }


    #endregion


    public void StartGame()
    {
        gameInProgress = true;
        LoadGameScene();
    }
}