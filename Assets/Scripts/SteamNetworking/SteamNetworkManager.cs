using Netcode.Transports.Facepunch;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;


public class SteamNetworkManager : MonoBehaviour
{
    public static SteamNetworkManager Instance { get; private set; }

    // Connection Information
    private const int MAX_PLAYERS = 2;
    private const int MAX_CONNECTION_PAYLOAD = 1024;

    // Lobby Information
    public Lobby? CurrentLobby { get; private set; }
    private (string, string)[] lobbyData;

    // Game State
    private bool gameInProgress;




    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(this);
    }

    private void Start()
    {
        NetworkManager.Singleton.OnServerStarted += OnServerStarted;
        NetworkManager.Singleton.ConnectionApprovalCallback += ConnectionApprovalCallback;

        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
    }

    private void OnDestroy()
    {
        NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
        NetworkManager.Singleton.ConnectionApprovalCallback -= ConnectionApprovalCallback;

        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
    }



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

        CurrentLobby = await SteamMatchmaking.CreateLobbyAsync(MAX_PLAYERS);
    }

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK) return;

        foreach ((string key, string value) in lobbyData)
            lobby.SetData(key, value);

        lobby.SetPublic();
        lobby.SetJoinable(true);

        NetworkManager.Singleton.StartHost();
    }

    private void OnServerStarted()
    {
        if (!NetworkManager.Singleton.IsHost) return;
        NetworkManager.Singleton.SceneManager.LoadScene("Lobby", LoadSceneMode.Single);
    }

    private void ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        // If there is no password, there is no need to check anything
        if (string.IsNullOrEmpty(CurrentLobby.Value.GetData("password")))
        {
            response.Approved = true;
            return;
        }

        byte[] connectionData = request.Payload;
        ulong clientId = request.ClientNetworkId;

        if (connectionData.Length > MAX_CONNECTION_PAYLOAD)
        {
            response.Approved = false;
            return;
        }

        // The host can enter automatically
        if (clientId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
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
        NetworkManager.Singleton.StartClient();
        await SteamMatchmaking.JoinLobbyAsync(lobbyId);
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        //SteamLobby.Instance.AddPlayer(SteamClient.Name);
    }

    #endregion



    #region Leave a Game

    public void KickClient(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsHost) return;

        NetworkManager.Singleton.DisconnectClient(clientId);
    }


    #endregion
}